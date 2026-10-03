using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Messages;
using Game.State;
using KeyValue.Runtime;
using Model;
using Model.Ops.Timetable;
using UI.Builder;
using UI.Common;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcSimulationSpeed
{
    private const string Key = "RMROC451.TweaksAndThings.SimulationRate";
    private static int rate = 1;
    private static float baseline = 1, applied = 1;
    private static double? until;
    private static string stopClock = "";
    private static string status = "Normal speed";
    private static bool selectedOnce;
    private static readonly HashSet<string> selected = new();
    private static readonly Dictionary<string, bool> watched = new();
    private static IKeyValueObject? storage;
    private static float nextBarrierCheck;
    private static double? barrier;
    private static string barrierName = "";

    internal static string Status => rate > 1 ? $"Simulation {rate}× · clock and all trains" + (until.HasValue ? " · stop " + NpcRandomTrafficPolicy.Clock(until.Value) : " · stop when a selected train stops") : status;
    private static List<BaseLocomotive> Running() => TrainController.Shared == null ? new List<BaseLocomotive>() :
        TrainController.Shared.Cars.OfType<BaseLocomotive>().Where(c => !c.IsInBardo && !c.IsMuEnabled && !ThroughTrafficGuard.IsGenerated(c) &&
            (c.AutoEngineerPlanner?._orders.Mode == AutoEngineerMode.Road || c.AutoEngineerPlanner?._orders.Mode == AutoEngineerMode.Waypoint)).ToList();

    internal static void Build(UIPanelBuilder builder)
    {
        builder.AddSection("Fast-forward simulation");
        builder.AddLabel("<style=Footnote>Clock and all movement accelerate together. Selected ROAD/WAYPOINT trains determine destination stops. Freight arrival–departure blocks are switching windows.");
        builder.AddLabel(() => Status, UIPanelBuilder.Frequency.Periodic);
        var engines = Running();
        if (!selectedOnce) { foreach (var engine in engines) selected.Add(engine.id); selectedOnce = true; }
        builder.HVScrollView(list =>
        {
            foreach (var engine in engines)
                list.AddFieldToggle(engine.DisplayName + " · " + engine.AutoEngineerPlanner._orders.Mode, () => selected.Contains(engine.id),
                    value => { if (value) selected.Add(engine.id); else selected.Remove(engine.id); });
            if (engines.Count == 0) list.AddLabel("No player trains in ROAD or WAYPOINT mode.");
        }).Height(100);
        builder.HStack(row =>
        {
            row.AddButtonCompact("Normal", () => Stop("Normal speed")).Disable(!StateManager.IsHost);
            foreach (int speed in new[] { 2, 4, 8 })
                row.AddButtonCompact(speed + "× to destination", () => Start(speed, null)).Disable(!StateManager.IsHost);
        });
        builder.AddField("Stop time (HH:mm)", builder.AddInputField(stopClock, value => stopClock = value));
        builder.AddButtonCompact("4× until stop time", () =>
        {
            var target = SimulationSpeedPolicy.StopClock(stopClock, TimeWeather.Now.TotalSeconds);
            if (!target.HasValue) { Toast.Present("Enter a stop time as HH:mm (00:00–23:59)."); return; }
            Start(4, target);
        }).Disable(!StateManager.IsHost);
        builder.AddLabel("<style=Footnote>Returns to normal at NPC spawns, switching windows, or the requested stop. A destination run also stops at signals or other AE stops. Higher rates require more physics work.");
    }

    private static void Start(int requested, double? stopAt)
    {
        if (!StateManager.IsHost || StateManager.Shared?.HasRestoredProperties != true || StateManager.Shared.IsWaiting ||
            !StateManager.CheckAuthorizedToSendMessage(default(WaitTime)) || Time.timeScale <= 0) return;
        var engines = Running().Where(e => selected.Contains(e.id)).ToList();
        if (engines.Count == 0) { Toast.Present("Select at least one ROAD/WAYPOINT train."); return; }
        double now = TimeWeather.Now.TotalSeconds;
        var window = SwitchingBoundary(now, out var name);
        if (window.HasValue && window.Value <= now + 1)
        { Toast.Present("Finish scheduled switching before fast-forward: " + name); return; }
        Stop("Normal speed");
        watched.Clear(); foreach (var engine in engines) watched[engine.id] = engine.VelocityMphAbs > 0.5f;
        until = stopAt; barrier = window; barrierName = name; nextBarrierCheck = 0;
        storage = StateManager.Shared.KeyValueObjectForId(GameStorage.ObjectId);
        if (storage == null) return;
        storage[Key] = Value.Float(SimulationSpeedPolicy.Rate(requested));
        Apply(SimulationSpeedPolicy.Rate(requested));
        ModDiagnosticLog.Write("FAST FORWARD", $"Started {rate}×; watching {string.Join(", ", engines.Select(e => e.DisplayName))}; stop time={stopAt?.ToString() ?? "first selected AE stop"}.");
    }

    private static void Apply(int requested)
    {
        if (requested == rate) return;
        if (rate > 1 && Mathf.Approximately(Time.timeScale, applied)) Time.timeScale = baseline;
        rate = requested;
        if (rate > 1 && Time.timeScale > 0)
        {
            baseline = Time.timeScale; applied = baseline * rate;
            // Keep the game's fixed timestep: more normal physics steps, rather
            // than larger/less stable integration steps. TimeWeather uses Time.time.
            Time.timeScale = applied;
        }
    }

    internal static void Stop(string reason)
    {
        if (StateManager.IsHost && storage != null && !StateManager.IsUnloading) storage[Key] = Value.Float(1);
        bool accelerated = rate > 1;
        Apply(1); until = null; watched.Clear(); status = reason;
        if (accelerated) ModDiagnosticLog.Write("FAST FORWARD", "Returned to normal: " + reason);
    }

    internal static void Reset()
    {
        Stop("Normal speed"); storage = null; selectedOnce = false; selected.Clear();
        if (StateManager.IsHost && StateManager.Shared?.HasRestoredProperties == true)
        {
            var kv = StateManager.Shared.KeyValueObjectForId(GameStorage.ObjectId);
            if (kv != null) kv[Key] = Value.Float(1);
        }
    }

    internal static void Update()
    {
        if (StateManager.Shared == null || RestoreNotifier.Shared == null || !RestoreNotifier.Shared.HasRestored || StateManager.IsUnloading)
        { if (rate > 1) Stop("Normal speed — railroad unloading"); storage = null; return; }
        var kv = StateManager.Shared.KeyValueObjectForId(GameStorage.ObjectId);
        if (kv == null) return;
        if (!ReferenceEquals(storage, kv))
        {
            Stop("Normal speed"); storage = kv;
            if (StateManager.IsHost) kv[Key] = Value.Float(1);
        }
        int requested = SimulationSpeedPolicy.Rate((int)(kv[Key].IsNull ? 1 : kv[Key].FloatValue));
        Apply(requested);
        if (rate <= 1 || !StateManager.IsHost) return;
        if (StateManager.Shared.IsWaiting || Time.timeScale <= 0 || !Mathf.Approximately(Time.timeScale, applied))
        { Stop("Normal speed — wait, pause or timing change"); return; }
        double now = TimeWeather.Now.TotalSeconds;
        double margin = Math.Max(1, Time.unscaledDeltaTime * rate * Math.Max(1, TimeWeather.TimeMultiplier) * 2);
        if (until.HasValue && now + margin >= until.Value) { Stop("Requested stop time reached"); return; }
        if (Time.realtimeSinceStartup >= nextBarrierCheck)
        { nextBarrierCheck = Time.realtimeSinceStartup + 0.25f; barrier = SwitchingBoundary(now, out barrierName); }
        if (barrier.HasValue && now + margin >= barrier.Value) { Stop("Scheduled switching: " + barrierName); Toast.Present(status); return; }
        if (until.HasValue) return;
        foreach (var id in watched.Keys.ToList())
        {
            if (!TrainController.Shared.TryGetCarForId(id, out var car) || !(car is BaseLocomotive engine)) { Stop("Selected train unavailable"); return; }
            var mode = engine.AutoEngineerPlanner?._orders.Mode ?? AutoEngineerMode.Off;
            if (mode != AutoEngineerMode.Road && mode != AutoEngineerMode.Waypoint) { Stop(engine.DisplayName + " left ROAD/WAYPOINT mode"); return; }
            if (engine.VelocityMphAbs > 0.5f) watched[id] = true;
            else if (watched[id]) { Stop(engine.DisplayName + " stopped — destination, signal or AE wait"); Toast.Present(status); return; }
        }
    }

    internal static double? SwitchingBoundary(double now, out string description)
    {
        description = ""; double? next = null;
        var controller = TimetableController.Shared;
        if (controller?.Current == null || StateManager.Shared?.PlayersManager == null) return null;
        var symbols = Running().Select(e => StateManager.Shared.PlayersManager.TrainCrews.FirstOrDefault(c => c.Id == e.trainCrewId)?.TimetableSymbol)
            .Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var timetable = controller.Current.ToAbsolute();
        foreach (var symbol in symbols)
        {
            if (!timetable.Trains.TryGetValue(symbol!, out var train) || train.TrainType != Timetable.TrainType.Freight) continue;
            // Examine yesterday as well, so a switching block spanning midnight
            // cannot be bypassed by starting a new fast-forward run after midnight.
            for (int day = -1; day <= 1; day++)
            {
                double dayStart = (Math.Floor(now / 86400) + day) * 1440;
                double previous = dayStart;
                foreach (var entry in train.Entries)
                {
                    double arrival = TimetableChartGeometry.Unwrap((entry.ArrivalTime?.Minutes ?? entry.DepartureTime.Minutes) % 1440 + dayStart, previous);
                    double departure = TimetableChartGeometry.Unwrap(entry.DepartureTime.Minutes % 1440 + dayStart, arrival); previous = departure;
                    if (!entry.ArrivalTime.HasValue) continue;
                    double? boundary = SimulationSpeedPolicy.SwitchingBoundary(now, arrival * 60, departure * 60);
                    if (boundary.HasValue && (!next.HasValue || boundary.Value < next.Value))
                    { next = boundary; description = train.Name + " at " + entry.Station; }
                }
            }
        }
        return next;
    }
}
