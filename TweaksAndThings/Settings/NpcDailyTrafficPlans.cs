using System;
using System.Linq;
using System.Collections.Generic;
using Game;
using Game.State;
using Model.Ops;
using Model.Ops.Timetable;
using Track;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcDailyTrafficPlans
{
    private static float nextCheck;
    private static readonly Dictionary<string, float> retry = new();
    private static readonly Dictionary<string, double> journeys = new();
    private static readonly Dictionary<string, float> routeRetry = new();
    private static Graph? cachedGraph;
    private static int cachedDay = -1;
    internal static void Reset()
    { nextCheck = 0; cachedGraph = null; cachedDay = -1; retry.Clear(); journeys.Clear(); routeRetry.Clear(); NpcDispatchBoardMarkers.Reset(); }

    internal static bool IsPending(string symbol) => NpcServiceStore.Load() &&
        NpcServiceStore.State.TrafficPlans.Any(p => !p.Dispatched && (p.Symbol == symbol || p.ReturnSymbol == symbol));

    internal static string Description(string symbol)
    {
        if (!NpcServiceStore.Load()) return "";
        var plan = NpcServiceStore.State.TrafficPlans.FirstOrDefault(p => p.Symbol == symbol || p.ReturnSymbol == symbol);
        if (plan == null) return "";
        return plan.Dispatched ? "NPC service in progress · return times estimated" :
            plan.InterchangeId.Length > 0 ? "Planned interchange service · demand required · return times estimated" : "Planned NPC through freight";
    }

    internal static void Tick(Settings settings, double now, bool force = false)
    {
        if (!StateManager.IsHost || StateManager.Shared?.HasRestoredProperties != true || StateManager.IsUnloading ||
            TimetableController.Shared == null || Graph.Shared == null || OpsController.Shared == null || !NpcServiceStore.Load() ||
            !force && Time.realtimeSinceStartup < nextCheck) return;
        nextCheck = Time.realtimeSinceStartup + 0.25f;
        var state = NpcServiceStore.State;
        if (state.TimetablePlanVersion < 2) {
            // Upgrade undispatched schedules once. Active trains keep their
            // current targets and stop indices until their next planned trip.
            var old = state.ProtectedTimetables.ToList();
            var rebuilt = new List<Timetable.Train>();
            foreach (var item in old.Where(p => IsPending(p.Key))) {
                if (!TimetableController.Shared.TryRead(item.Value, out var saved, null) || !saved.Trains.TryGetValue(item.Key, out var train) || train.Entries.Count < 2) continue;
                var minutes = NpcTimetablePlanning.Minutes(train);
                rebuilt.Add(NpcTimetableRegistry.MakeTrain(train.Name, train.Entries[0].Station, train.Entries.Last().Station, (int)train.TrainClass,
                    minutes[0] * 60d, minutes.Last() * 60d));
                state.ProtectedTimetables.Remove(item.Key);
            }
            foreach (var train in rebuilt.OrderBy(t => NpcTimetablePlanning.Minutes(t)[0])) NpcTimetableRegistry.Reserve(train);
            state.TimetablePlanVersion = 2;
            NpcServiceStore.Save(); NpcTimetableRegistry.Restore();
        }
        double start = Math.Floor(now / 86400) * 86400;
        int day = (int)Math.Floor(now / 86400);
        if (cachedDay != day || cachedGraph != Graph.Shared)
        {
            cachedDay = day; cachedGraph = Graph.Shared; journeys.Clear(); routeRetry.Clear(); retry.Clear();
            foreach (string key in state.SkippedTrafficPlans.ToList())
            {
                int split = key.LastIndexOf(':');
                if (split >= 0 && double.TryParse(key.Substring(split + 1), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double due) && due < start) state.SkippedTrafficPlans.Remove(key);
            }
        }
        foreach (var plan in state.TrafficPlans.Where(p => !p.Dispatched).ToList())
        {
            bool obsolete;
            if (plan.InterchangeId.Length == 0)
                obsolete = !settings.ThroughTrafficEnabled || !settings.RandomThroughFreightsEnabled || !state.RandomDepartures.Any(d => d.Symbol == plan.Symbol);
            else
            {
                var interchange = OpsController.Shared.EnabledInterchanges.FirstOrDefault(i => i.Identifier == plan.InterchangeId);
                obsolete = settings.InterchangeService != InterchangeServiceMode.Simulated || interchange == null || plan.ServiceTime < start ||
                    (interchange.LastServiced.HasValue && interchange.LastServiced.Value.TotalSeconds >= plan.ServiceTime - 1) ||
                    plan.Extra && (!interchange.TryGetExtraScheduled(out var extra) || Math.Abs(extra.TotalSeconds - plan.ServiceTime) > 2);
            }
            if (obsolete) Cancel(plan);
        }
        // Cache journeys shared by many trains and spread new route searches
        // across frames. Publishing the remaining same-route trains is cheap.
        NpcRandomThroughFreights.Prepare(settings, now);
        int countBefore = state.ProtectedTimetables.Count;
        long began = System.Diagnostics.Stopwatch.GetTimestamp();
        if (settings.ThroughTrafficEnabled && settings.RandomThroughFreightsEnabled)
        {
            foreach (var departure in state.RandomDepartures.Where(d => !state.TrafficPlans.Any(p => p.Symbol == d.Symbol) &&
                (!retry.TryGetValue(d.Symbol, out var until) || Time.realtimeSinceStartup >= until)).ToList())
            {
                retry[departure.Symbol] = Time.realtimeSinceStartup + 30;
                NpcRandomThroughFreights.PrepareConsist(departure, settings, new System.Random());
                PlanRandom(departure);
                if ((System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000d / System.Diagnostics.Stopwatch.Frequency >= 2) break;
            }
        }
        if (settings.InterchangeService == InterchangeServiceMode.Simulated)
        {
            // Approach searches already yield across frames under the global planning budget.
            foreach (var interchange in OpsController.Shared.EnabledInterchanges)
            {
                if (!NpcTrainOperations.TryApproach(interchange, settings, out var spawn, out var target, out double travel)) continue;
                var daily = interchange.GetNextServiceTime(new GameDateTime((float)start), out _, dailyOnly: true);
                PlanInterchange(interchange, spawn, target, travel, daily.TotalSeconds, false, now);
                // Also publish an early departure for the following day's service.
                PlanInterchange(interchange, spawn, target, travel, daily.TotalSeconds + 86400, false, now);
                if (interchange.TryGetExtraScheduled(out var extra))
                    PlanInterchange(interchange, spawn, target, travel, extra.TotalSeconds, true, now);
            }
        }
        NpcServiceStore.Save();
        if (countBefore != state.ProtectedTimetables.Count) NpcTimetableRegistry.Restore();
        NpcDispatchBoardMarkers.Tick(settings, now);
    }

    private static void PlanRandom(NpcRandomDeparture departure)
    {
        var controller = TimetableController.Shared;
        string key = departure.From + ":" + departure.To + ":" + departure.TrainClass;
        if (!journeys.TryGetValue(key, out double travel))
        {
            if (routeRetry.TryGetValue(key, out var until) && Time.realtimeSinceStartup < until) return;
            if (!controller.TryGetStation(departure.From, out var from) || !controller.TryGetStation(departure.To, out var to) ||
                !ThroughTrafficSpawner.TryGetStationLocation(from, out var a) || !ThroughTrafficSpawner.TryGetStationLocation(to, out var b) ||
                !NpcTrainOperations.Route(a, b, out var route, out _))
            { routeRetry[key] = Time.realtimeSinceStartup + 30; return; }
            travel = journeys[key] = NpcTrainOperations.TravelSeconds(route, departure.TrainClass == 0);
        }
        var train = NpcTimetableRegistry.MakeTrain(departure.Symbol, departure.From, departure.To, departure.TrainClass, departure.Scheduled,
            departure.Scheduled + travel);
        NpcServiceStore.State.TrafficPlans.Add(new NpcTrafficPlan { Symbol = departure.Symbol, Dispatch = departure.Scheduled });
        NpcTimetableRegistry.Reserve(train);
        ModDiagnosticLog.Write("DAY PLAN", $"Published hidden through freight {departure.Symbol}: {departure.From} → {departure.To}, departure {NpcRandomTrafficPolicy.Clock(departure.Scheduled)}.");
    }

    private static void PlanInterchange(Interchange interchange, Location spawn, Location target, double travel, double due, bool extra, double now)
    {
        if (!NpcTimetableRegistry.InterchangeStations(spawn, target, out var from, out var to)) return;
        travel = Math.Max(travel, NpcTimetableRegistry.EstimatedTravelSeconds(from, to, 2, travel));
        double dispatch = NpcServicePolicy.DispatchTime(due, travel);
        var state = NpcServiceStore.State;
        string key = NpcTrafficPlanPolicy.ServiceKey(interchange.Identifier, due);
        if (state.SkippedTrafficPlans.Contains(key))
        {
            if (!NpcServicePolicy.HasServiceDemand(interchange.Orders.Sum(o => o.CarCount), SimulatedInterchangeService.PickupCars(interchange).Count())) return;
            state.SkippedTrafficPlans.Remove(key);
        }
        if (!NpcTrafficPlanPolicy.InPlanningDay(dispatch, due, now) ||
            state.TrafficPlans.Any(p => p.InterchangeId == interchange.Identifier && Math.Abs(p.ServiceTime - due) <= 2) ||
            state.Services.Any(s => s.InterchangeId == interchange.Identifier && Math.Abs(s.Scheduled - due) <= 2)) return;
        string symbol = NpcTimetableRegistry.NewSymbol("NI");
        string returnSymbol = symbol + "R";
        // Reserve the return symbol against player timetable names as well.
        if (TimetableController.Shared.CurrentRaw?.Trains.ContainsKey(returnSymbol) == true) returnSymbol = NpcTimetableRegistry.NewSymbol("NIR");
        int cars = interchange.Orders.Sum(o => Math.Max(0, o.CarCount)) + SimulatedInterchangeService.PickupCars(interchange).Count();
        double working = Math.Max(NpcServicePolicy.SecondsPerCar, cars * NpcServicePolicy.SecondsPerCar);
        state.TrafficPlans.Add(new NpcTrafficPlan { Symbol = symbol, ReturnSymbol = returnSymbol, InterchangeId = interchange.Identifier,
            Dispatch = dispatch, ServiceTime = due, Extra = extra });
        NpcTimetableRegistry.Reserve(NpcTimetableRegistry.MakeTrain(symbol, from, to, 2, dispatch, due));
        NpcTimetableRegistry.Reserve(NpcTimetableRegistry.MakeTrain(returnSymbol, to, from, 2, due + working, due + working + travel));
        ModDiagnosticLog.Write("DAY PLAN", $"Published hidden interchange {symbol}/{returnSymbol}: {interchange.DisplayName}, dispatch {NpcRandomTrafficPolicy.Clock(dispatch)}, service {NpcRandomTrafficPolicy.Clock(due)}; return estimated. No cars or crews allocated.");
    }

    internal static void Cancel(NpcTrafficPlan plan)
    {
        NpcServiceStore.State.TrafficPlans.Remove(plan);
        NpcTimetableRegistry.Remove(new NpcServiceRecord { TrainSymbol = plan.Symbol, ReturnTrainSymbol = plan.ReturnSymbol });
    }

    internal static NpcTrafficPlan? Claim(Interchange interchange, double due)
    {
        var plan = NpcTrafficPlanPolicy.ForInterchange(NpcServiceStore.State, interchange.Identifier, due);
        if (plan != null) plan.Dispatched = true;
        return plan;
    }

    internal static void Skip(Interchange interchange, double due)
    {
        NpcServiceStore.State.SkippedTrafficPlans.Add(NpcTrafficPlanPolicy.ServiceKey(interchange.Identifier, due));
        var plan = NpcTrafficPlanPolicy.ForInterchange(NpcServiceStore.State, interchange.Identifier, due);
        if (plan != null) Cancel(plan);
    }
}
