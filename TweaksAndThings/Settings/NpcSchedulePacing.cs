using System;
using System.Linq;
using Game;
using Game.State;
using Model;
using Model.AI;
using Model.Ops;
using Model.Ops.Timetable;

namespace RMROC451.TweaksAndThings;

internal static class NpcSchedulePacing
{
    internal static void Apply(AutoEngineer engineer, ref AutoEngineer.Targets targets)
    {
        var lead = engineer.Locomotive;
        if (!StateManager.IsHost || TweaksAndThingsPlugin.Instance?.IsEnabled != true ||
            !ThroughTrafficGuard.IsGenerated(lead) || !NpcServiceStore.Load()) return;
        var service = NpcServiceStore.State.Services.FirstOrDefault(s => s.LeadId == lead.id && s.InterchangeId.Length > 0);
        if (service == null || service.Phase != "Approaching" && service.Phase != "Departing") return;
        double allowed = Math.Abs(targets.MaxSpeedMph);
        if (allowed <= 0) return;
        var planner = lead.AutoEngineerPlanner;
        var route = planner._route?.Skip(planner._startStepIndex).ToList();
        if (route == null || route.Count == 0) return;
        double now = TimeWeather.Now.TotalSeconds, due = service.Scheduled;
        if (service.Phase == "Departing")
        {
            string symbol = service.ReturnTrainSymbol.Length > 0 ? service.ReturnTrainSymbol : service.TrainSymbol;
            if (!NpcServiceStore.State.ProtectedTimetables.TryGetValue(symbol, out var source) ||
                !TimetableController.Shared.TryRead(source, out var saved, null) || !saved.Trains.TryGetValue(symbol, out var train) ||
                !train.TryGetAbsoluteTimeForEntry(train.Entries.Count - 1, Model.Ops.Timetable.TimetableTimeType.Departure, out int minutes)) return;
            due = TimetableForecastPolicy.ClockNear(minutes, now / 60) * 60;
        }
        double travel = 0, meters = 0;
        var position = planner.StartLocation();
        for (int i = 0; i < route.Count; i++)
        {
            var step = route[i];
            double distance = step.Distance;
            if (i == 0 && step.Location.segment == position.segment)
                distance = Math.Max(0, step.Location.distance - position.WithEnd(step.Location.end).distance);
            meters += distance;
            travel += distance / (Math.Max(1, Math.Min(allowed, step.Location.segment.GetExpectedSpeedLimit())) * 0.44704);
        }
        double speed = NpcTimetablePacingPolicy.Speed(allowed, travel, due - now, meters);
        if (speed >= allowed) return;
        targets = new AutoEngineer.Targets((float)(Math.Sign(targets.MaxSpeedMph) * speed), targets.AllTargets,
            targets.AverageGradeUnder, targets.AverageGradeAhead, targets.ChangeDirection, targets.Mode,
            targets.StopAnnounce, targets.NextSignal);
        ModDiagnosticLog.Write("PACING", $"{service.TrainSymbol}: pacing at up to {speed:N0} mph; " +
            $"estimated travel {travel / 60:N1} min, scheduled time in {(due - now) / 60:N1} min; signals and track limits remain authoritative.", "pacing:" + service.Id, 60);
    }
}
