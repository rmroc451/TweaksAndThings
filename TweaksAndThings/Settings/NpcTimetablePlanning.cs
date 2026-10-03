using System;
using System.Collections.Generic;
using System.Linq;
using Model.Ops.Timetable;
using Track;
using UnityEngine;
using Helpers;

namespace RMROC451.TweaksAndThings;

internal static class NpcTimetablePlanning
{
    private static Graph? cachedGraph;
    private static readonly Dictionary<string, (float expires, bool usable)> passing = new();
    internal static List<int> Minutes(Timetable.Train train)
    {
        double reference = NpcServiceStore.State.TrafficPlans.FirstOrDefault(p => p.Symbol == train.Name || p.ReturnSymbol == train.Name)?.Dispatch / 60
            ?? Game.TimeWeather.Now.TotalSeconds / 60;
        var result = new List<int>();
        double last = reference;
        for (int i = 0; i < train.Entries.Count; i++) {
            train.TryGetAbsoluteTimeForEntry(i, TimetableTimeType.Departure, out int minute);
            last = i == 0 ? TimetableForecastPolicy.ClockNear(minute, reference) : TimetableChartGeometry.Unwrap(minute, last);
            result.Add((int)last);
        }
        return result;
    }

    internal static void PlanMeets(Timetable.Train train)
    {
        var controller = TimetableController.Shared;
        var usable = new Dictionary<string, bool>();
        var trains = controller.CurrentRaw?.Trains.ToDictionary(p => p.Key, p => p.Value.Clone()) ?? new Dictionary<string, Timetable.Train>();
        foreach (var snapshot in NpcServiceStore.State.ProtectedTimetables.ToList())
            if (controller.TryRead(snapshot.Value, out var timetable, null) && timetable.Trains.TryGetValue(snapshot.Key, out var saved)) trains[snapshot.Key] = saved;
        foreach (var other in trains.Values.Where(t => t.Name != train.Name && t.Entries.Count >= 2)) {
            var (opposingCodes, partner) = PassingTimes(other);
            var common = train.Entries.Select(e => e.Station).Where(partner.ContainsKey).ToList();
            if (common.Count < 2 || opposingCodes.IndexOf(common[0]) < opposingCodes.IndexOf(common.Last())) continue;
            var times = Minutes(train);
            int meet = NpcTimetablePlanPolicy.Meet(train.Entries.Select(e => e.Station).ToList(), times, partner, code => {
                if (!usable.TryGetValue(code, out bool value)) usable[code] = value = HasPassingTrack(train, code);
                return value;
            });
            if (meet < 0) continue;
            string station = train.Entries[meet].Station;
            int hold = Math.Max(0, partner[station] + 5 - times[meet]);
            var entry = train.Entries[meet];
            train.Entries[meet] = new(station, TimetableTime.Absolute(times[meet]), TimetableTime.Relative(hold), entry.Meets.Concat(new[] { other.Name }).Distinct().ToArray());
            // Relative later entries retain their travel time and inherit the allowance.
            int index = other.Entries.FindIndex(e => e.Station == station);
            if (index >= 0 && NpcServiceStore.State.ProtectedTimetables.ContainsKey(other.Name)) {
                var opposite = other.Entries[index];
                other.Entries[index] = new(station, opposite.ArrivalTime, opposite.DepartureTime, opposite.Meets.Concat(new[] { train.Name }).Distinct().ToArray());
                NpcTimetableRegistry.SaveTrain(other);
            }
            ModDiagnosticLog.Write("DAY PLAN", $"{train.Name}: planned meet with {other.Name} at {station}; allowance {hold} min including 5 min clearance. Live signaling remains authoritative.");
        }
    }

    // Expand sparse human schedules for comparison only. Their source, dwell,
    // crews and edit permissions are never changed or claimed by the mod.
    private static (List<string> codes, Dictionary<string, int> times) PassingTimes(Timetable.Train train)
    {
        var minutes = Minutes(train);
        var codes = new List<string> { train.Entries[0].Station };
        var result = new Dictionary<string, int> { [codes[0]] = minutes[0] };
        for (int i = 1; i < train.Entries.Count; i++) {
            int arrival = minutes[i];
            if (train.Entries[i].ArrivalTime.HasValue && train.TryGetAbsoluteTimeForEntry(i, TimetableTimeType.Arrival, out int clock))
                arrival = (int)TimetableForecastPolicy.ClockNear(clock, minutes[i]);
            var leg = NpcTimetableRegistry.MakeTrain("comparison", train.Entries[i - 1].Station, train.Entries[i].Station,
                (int)train.TrainClass, 0, (arrival - minutes[i - 1]) * 60d);
            int total = leg.Entries.Skip(1).Sum(e => e.DepartureTime.Minutes), elapsed = 0;
            for (int j = 1; j < leg.Entries.Count; j++) {
                elapsed += leg.Entries[j].DepartureTime.Minutes;
                string code = leg.Entries[j].Station;
                if (!codes.Contains(code)) codes.Add(code);
                result[code] = NpcTimetablePlanPolicy.Interpolate(minutes[i - 1], arrival, elapsed, total);
            }
            result[train.Entries[i].Station] = minutes[i];
        }
        return (codes, result);
    }

    private static bool HasPassingTrack(Timetable.Train train, string code)
    {
        if (cachedGraph != Graph.Shared) { cachedGraph = Graph.Shared; passing.Clear(); }
        int index = train.Entries.FindIndex(e => e.Station == code);
        if (index <= 0 || index >= train.Entries.Count - 1) return false;
        string key = train.Entries[index - 1].Station + ":" + code + ":" + train.Entries[index + 1].Station;
        if (passing.TryGetValue(key, out var cached) && cached.expires > Time.realtimeSinceStartup) return cached.usable;
        var station = TimetableLiveForecast.Station(code);
        if (!station.IsValid || !NpcTrainOperations.Route(TimetableLiveForecast.Station(train.Entries[index - 1].Station),
            TimetableLiveForecast.Station(train.Entries[index + 1].Station), out var route, out _)) return false;
        var segments = Graph.Shared.Segments.Where(s => s.GroupEnabled && s.turntable == null).ToDictionary(s => s.id);
        var nodes = segments.Values.SelectMany(s => new[] { s.a, s.b }).Distinct().ToDictionary(n => n.id);
        bool found = false;
        // Preliminary day plans do not have physical consists yet. Require at
        // least 400 m of usable siding; live routing rechecks the actual train.
        NpcPassingSidingPolicy.Longest(segments.Values.Select(s => new NpcPassingSidingPolicy.Edge(s.id, s.a.id, s.b.id, s.GetLength())),
            route.Select(s => s.Location.segment.id).ToHashSet(),
            (n, a, b) => nodes[n].SegmentCanReachSegment(segments[a], segments[b]),
            junctions: route.Where(s => s.Node != null && Graph.Shared.DecodeSwitchAt(s.Node, out _, out _, out _)).Select(s => s.Node.id).ToHashSet(),
            foulingClearance: (a, b) => Math.Max(20, Graph.Shared.CalculateFoulingDistance(nodes[a]) + Graph.Shared.CalculateFoulingDistance(nodes[b])),
            found: (a, b, length) => { if (length >= 400 && Math.Min(Vector3.Distance(station.GetPosition(), nodes[a].transform.GamePosition()), Vector3.Distance(station.GetPosition(), nodes[b].transform.GamePosition())) < 800) found = true; });
        if (passing.Count > 128) passing.Clear();
        passing[key] = (Time.realtimeSinceStartup + 60, found);
        return found;
    }
}
