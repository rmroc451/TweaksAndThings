using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.State;
using Model.Ops;
using Model.Ops.Timetable;
using Model;
using Model.Definition;

namespace RMROC451.TweaksAndThings;

internal static class NpcRandomThroughFreights
{
    internal static (int min, int max) Range(Settings settings) => NpcRandomTrafficPolicy.DailyRange(
        OpsController.Shared?.Areas.SelectMany(a => a.Industries).Distinct().Where(i => !i.ProgressionDisabled)
            .Sum(i => Math.Max(0, i.Contract?.Tier ?? 0)) ?? 0, settings.RandomThroughFreightMultiplier);

    private static List<int> Classes(Settings settings)
    {
        var classes = new List<int>();
        if (settings.RandomThroughFreightFirstClass) classes.Add(0);
        if (settings.RandomThroughFreightSecondClass) classes.Add(1);
        if (settings.RandomThroughFreightThirdClass) classes.Add(2);
        return classes;
    }

    internal static void Prepare(Settings settings, double now)
    {
        if (!settings.ThroughTrafficEnabled || !settings.RandomThroughFreightsEnabled || !StateManager.IsHost || !NpcServiceStore.Load() || TimetableController.Shared == null) return;
        var state = NpcServiceStore.State;
        int day = (int)Math.Floor(now / 86400);
        if (state.RandomScheduleDay >= day) return;
        var classes = Classes(settings);
        var stations = TimetableController.Shared.GetAllStations(includeDuplicates: false).Where(ThroughTrafficSpawner.IsInterchange).ToList();
        if (classes.Count == 0 || stations.Count < 2) return;
        var random = new Random();
        var range = Range(settings);
        int count = random.Next(range.min, range.max + 1);
        // A partial first day gets a proportional share; saved plans never reroll on reload.
        double end = (day + 1) * 86400d;
        count = (int)Math.Round(count * (end - now) / 86400);
        foreach (double time in NpcRandomTrafficPolicy.DepartureTimes(random, count, now + 1, end))
        {
            int from = random.Next(stations.Count), to = random.Next(stations.Count - 1);
            if (to >= from) to++;
            var departure = new NpcRandomDeparture { Symbol = NpcTimetableRegistry.NewSymbol("NR"),
                From = stations[from].code, To = stations[to].code, TrainClass = classes[random.Next(classes.Count)], Scheduled = time };
            PrepareConsist(departure, settings, random);
            state.RandomDepartures.Add(departure);
        }
        state.RandomScheduleDay = day;
        NpcServiceStore.Save();
        ModDiagnosticLog.Write("RANDOM TRAFFIC", $"game={TimeWeather.Now}; day={day}; tiers daily range={range.min}–{range.max}; planned={count}");
    }

    internal static void PrepareConsist(NpcRandomDeparture departure, Settings settings, Random random)
    {
        if (departure.FreightDefinitions.Count > 0 || TrainController.Shared?.PrefabStore == null) return;
        var catalog = TrainController.Shared.PrefabStore.AllCarDefinitionInfos.Where(i => i.Definition.VisibleInPlacer && i.Definition.Archetype.IsFreight()).ToList();
        if (catalog.Count == 0) return;
        int minimum = Math.Max(1, Math.Min(50, settings.RandomThroughFreightMinCars));
        int maximum = Math.Max(minimum, Math.Min(50, settings.RandomThroughFreightMaxCars));
        int count = random.Next(minimum, maximum + 1);
        for (int i = 0; i < count; i++) departure.FreightDefinitions.Add(catalog[random.Next(catalog.Count)].Identifier);
    }

    private static float nextAttempt;
    internal static void Tick(Settings settings, double now, bool force = false)
    {
        Prepare(settings, now);
        if (!settings.ThroughTrafficEnabled || !settings.RandomThroughFreightsEnabled || !force && UnityEngine.Time.realtimeSinceStartup < nextAttempt) return;
        nextAttempt = UnityEngine.Time.realtimeSinceStartup + 10;
        foreach (var departure in NpcServiceStore.State.RandomDepartures.Where(d => d.Scheduled <= now).OrderBy(d => d.Scheduled).ToList())
        {
            try
            {
                if (ThroughTrafficSpawner.TrySpawnRandom(departure, settings))
                {
                    var plan = NpcServiceStore.State.TrafficPlans.FirstOrDefault(p => p.Symbol == departure.Symbol);
                    if (plan != null) plan.Dispatched = true;
                    NpcServiceStore.State.RandomDepartures.Remove(departure);
                }
            }
            catch (Exception ex) { TweaksAndThingsPlugin.LogException("Random through freight " + departure.Symbol, ex); }
            // Retain blocked trains and their visible daily plan for retry.
        }
        NpcServiceStore.Save();
    }

    internal static double? WarpBoundary(Settings settings, double now, double end)
    {
        Prepare(settings, now);
        if (!settings.ThroughTrafficEnabled || !settings.RandomThroughFreightsEnabled) return null;
        var times = NpcServiceStore.State.RandomDepartures.Where(d => d.Scheduled > now && d.Scheduled <= end).Select(d => d.Scheduled).ToList();
        // Crossing midnight creates the next day's plan before a subsequent warp continues.
        double midnight = (Math.Floor(now / 86400) + 1) * 86400;
        if (midnight <= end) times.Add(midnight);
        return times.Count > 0 ? times.Min() : (double?)null;
    }
}
