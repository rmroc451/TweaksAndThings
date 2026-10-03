using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.State;
using KeyValue.Runtime;
using Model.Ops.Timetable;
using Newtonsoft.Json;
using UnityEngine;
using Timetable = Model.Ops.Timetable.Timetable;

namespace RMROC451.TweaksAndThings;

internal static class TimetableHistory
{
    private const string Key = "RMROC451.TweaksAndThings.TimetableHistory.v1";
    private const string PlanKey = "RMROC451.TweaksAndThings.TimetableHistoryPlans.v1";
    private static IKeyValueObject? storage;
    private static Dictionary<string, List<TimetableHistorySample>> history = new();
    private static Dictionary<string, string> plans = new();
    private static readonly Queue<Timetable.Train> work = new();
    private static float nextBatch, nextSave, nextRead;
    private static bool dirty;
    private static string? lastJson;

    internal static void Update()
    {
        if (StateManager.Shared == null || RestoreNotifier.Shared == null || !RestoreNotifier.Shared.HasRestored || TimetableController.Shared?.Current == null) return;
        var kv = StateManager.Shared.KeyValueObjectForId(GameStorage.ObjectId);
        if (kv == null) return;
        if (!ReferenceEquals(storage, kv)) {
            storage = kv; history.Clear(); plans.Clear(); work.Clear(); dirty = false; lastJson = null; nextRead = nextBatch = nextSave = 0;
        }
        if ((!StateManager.IsHost || lastJson == null) && Time.unscaledTime >= nextRead) {
            nextRead = Time.unscaledTime + 5;
            string? json = kv[Key].IsNull ? "{}" : kv[Key].StringValue;
            if (lastJson != json) {
                history = JsonConvert.DeserializeObject<Dictionary<string, List<TimetableHistorySample>>>(json!) ?? new(); lastJson = json;
                plans = kv[PlanKey].IsNull ? new() : JsonConvert.DeserializeObject<Dictionary<string, string>>(kv[PlanKey].StringValue) ?? new();
            }
        }
        if (!StateManager.IsHost) return;
        TimetableLiveForecast.Advance();
        if (work.Count == 0 && Time.unscaledTime >= nextBatch) {
            nextBatch = Time.unscaledTime + 2;
            foreach (var train in TimetableController.Shared.Current.ToAbsolute().Trains.Values) work.Enqueue(train);
        }
        // One train per frame; native route searches are already queued and limited
        // to one per rendered frame by the shared live-forecast cache.
        if (work.Count > 0) Record(work.Dequeue());
        if (dirty && Time.unscaledTime >= nextSave) Flush();
    }

    private static void Record(Timetable.Train train)
    {
        var forecast = TimetableLiveForecast.Get(train);
        if (forecast == null || !forecast.ShowPosition || forecast.Route.Meters <= 0) return;
        if (history.TryGetValue(train.Name, out var existing) && existing.Count > 0 &&
            forecast.Now >= existing[existing.Count - 1].Minute && forecast.Now - existing[existing.Count - 1].Minute < 1) return;
        var anchors = new List<(double meters, string code)>();
        foreach (var station in TimetableController.Shared.GetAllStations(includeDuplicates: false)) {
            if (forecast.Route.Project(TimetableLiveForecast.Station(station.code), out double meters)) anchors.Add((meters, station.code));
        }
        if (!anchors.Any(a => a.code == forecast.Route.FromCode)) anchors.Add((0, forecast.Route.FromCode));
        if (!anchors.Any(a => a.code == forecast.Route.ToCode)) anchors.Add((forecast.Route.Meters, forecast.Route.ToCode));
        anchors = anchors.OrderBy(a => a.meters).ToList();
        var before = anchors.LastOrDefault(a => a.meters <= forecast.Progress);
        var after = anchors.FirstOrDefault(a => a.meters >= forecast.Progress);
        if (before.code == null) before = anchors[0];
        if (after.code == null) after = anchors[anchors.Count - 1];
        int origin = train.Entries.FindIndex(e => e.Station == forecast.Route.FromCode);
        int destination = train.Entries.FindIndex(e => e.Station == forecast.Route.ToCode);
        if (origin < 0 || destination <= origin) return;
        double departure = TimetableForecastPolicy.ClockNear(train.Entries[origin].DepartureTime.Minutes, forecast.Now);
        double arrival = TimetableChartGeometry.Unwrap(train.Entries[destination].ArrivalTime?.Minutes ?? train.Entries[destination].DepartureTime.Minutes, departure);
        var sample = new TimetableHistorySample {
            Minute = forecast.Now, PlannedMinute = departure + (arrival - departure) * Math.Max(0, Math.Min(1, forecast.Progress / forecast.Route.Meters)),
            From = before.code, To = after.code, Vehicle = forecast.VehicleId,
            Fraction = after.meters <= before.meters ? 0 : (forecast.Progress - before.meters) / (after.meters - before.meters)
        };
        if (!history.TryGetValue(train.Name, out var samples)) history[train.Name] = samples = new();
        if (TimetableHistoryPolicy.Append(samples, sample)) {
            dirty = true;
            plans[train.Name] = TimetableWriter.Write(new Timetable(new Dictionary<string, Timetable.Train> { [train.Name] = train.Clone() }));
        }
    }

    internal static void IncludeArchived(Timetable timetable)
    {
        double day = Math.Floor(TimeWeather.Now.TotalSeconds / 86400) * 1440;
        foreach (var saved in plans) {
            if (timetable.Trains.ContainsKey(saved.Key) || !Samples(saved.Key).Any(s => s.Minute >= day && s.Minute < day + 1440)) continue;
            if (TimetableController.Shared.TryRead(saved.Value, out var parsed, null) && parsed.ToAbsolute().Trains.TryGetValue(saved.Key, out var train)) timetable.Trains[saved.Key] = train;
        }
    }

    internal static IReadOnlyList<TimetableHistorySample> Samples(string symbol) => history.TryGetValue(symbol, out var samples) ? samples : Array.Empty<TimetableHistorySample>();
    internal static string Status(string symbol)
    {
        var sample = Samples(symbol).LastOrDefault();
        return sample == null ? "No recorded actual running yet." : "Recorded " + TimetableChartGeometry.Clock(sample.Minute) + " · " + TimetableHistoryPolicy.VarianceText(sample.Variance);
    }
    internal static void Flush()
    {
        if (!dirty || storage == null || !StateManager.IsHost) return;
        double earliest = Math.Floor(TimeWeather.Now.TotalSeconds / 86400) * 1440 - 1440;
        foreach (var symbol in history.Keys.ToList()) {
            history[symbol].RemoveAll(s => s.Minute < earliest);
            if (history[symbol].Count == 0) { history.Remove(symbol); plans.Remove(symbol); }
        }
        // Bound total save/network size even on very busy timetables.
        int excess = history.Values.Sum(s => s.Count) - 20000;
        if (excess > 0) foreach (var item in history.SelectMany(p => p.Value.Select(s => (symbol: p.Key, sample: s))).OrderBy(p => p.sample.Minute).Take(excess).ToList()) history[item.symbol].Remove(item.sample);
        lastJson = JsonConvert.SerializeObject(history);
        storage[Key] = Value.String(lastJson); dirty = false; nextSave = Time.unscaledTime + 30;
        storage[PlanKey] = Value.String(JsonConvert.SerializeObject(plans));
    }
}
