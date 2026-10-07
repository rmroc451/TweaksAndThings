using System;
using System.Collections.Generic;
using System.Linq;
using Game.Messages;
using Game.State;
using KeyValue.Runtime;
using Model;
using Model.AI;
using Model.Ops;
using RMROC451.TweaksAndThings.Extensions;
using Track;
using UI;
using UI.Common;
using UI.EngineControls;
using Helpers;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class AeLocalService
{
    internal const string Marker = "RMROC451.TweaksAndThings.AeLocal";
    private static float nextTick;
    private static int loadedGeneration = -1;
    internal static bool CaptureSetupCommand(BaseLocomotive lead, AutoEngineerCommand command)
    {
        if (!StateManager.IsHost || ThroughTrafficGuard.IsTrustedMutation) return false;
        var job = Job(lead);
        if (job == null || job.PickingTarget.Length == 0 || string.IsNullOrEmpty(command.WaypointLocationString)) return false;
        AcceptWaypoint(job, lead, command.WaypointLocationString);
        return true;
    }
    internal static bool CaptureSetupWaypoint(Car car, Location point)
    {
        if (!StateManager.IsHost || ThroughTrafficGuard.IsTrustedMutation || car is not BaseLocomotive lead) return false;
        var job = Job(lead);
        if (job == null || job.Phase != "Setup" || job.PickingTarget.Length == 0) return false;
        AcceptWaypoint(job, lead, Graph.Shared.LocationToString(point));
        return true;
    }
    internal static bool SetMaxSpeed(Car car, int speed)
    {
        if (!StateManager.IsHost || ThroughTrafficGuard.IsTrustedMutation || car is not BaseLocomotive lead) return false;
        var job = Job(lead);
        if (job == null || job.Phase == "Complete") return false;
        job.MaxSpeedMph = Math.Max(0, Math.Min(AutoEngineerMode.Waypoint.MaxSpeedMph(), speed));
        NpcServiceStore.Save();
        if (job.Phase == "Moving" && !job.Paused) SendOrder(job, lead);
        else NpcTrainOperations.Hold(lead);
        return true;
    }
    private static void AcceptWaypoint(AeLocalJob job, BaseLocomotive lead, string encoded)
    {
        var point = Graph.Shared.ResolveLocationString(encoded);
        if (!point.IsValid) { FinishWaypointSelection(job, lead, "Invalid track target; select another waypoint"); return; }
        if (job.PickingTarget == "yard")
        {
            var area = OpsController.Shared.Areas.FirstOrDefault(a => a.identifier == job.HomeArea);
            if (area == null || !area.Contains(point.GetPosition())) { FinishWaypointSelection(job, lead, "Yard target must be in the originating area"); return; }
            var span = OpsController.Shared.AllIndustryComponents.SelectMany(c => c.TrackSpans).FirstOrDefault(s => s.Contains(point));
            if (span == null) { FinishWaypointSelection(job, lead, "Select a named operational yard track for returns"); return; }
            job.YardPoint = encoded;
            job.YardSpan = span.id;
        }
        else job.ParkingPoint = encoded;
        FinishWaypointSelection(job, lead, "Waypoint saved; finish setup and start");
    }
    private static void FinishWaypointSelection(AeLocalJob job, BaseLocomotive lead, string status)
    {
        job.PickingTarget = "";
        NpcTrainOperations.Hold(lead);
        job.Status = status;
        NpcServiceStore.Save(); AeLocalWindow.Show(job);
    }
    internal static bool Owns(Car? car) => car != null && !car.KeyValueObject[Marker].IsNull && car.KeyValueObject[Marker].StringValue.Length > 0;
    internal static AeLocalJob? Job(Car? car) => car == null || !NpcServiceStore.Load() ? null :
        NpcServiceStore.State.LocalJobs.FirstOrDefault(j => j.OwnedIds.Contains(car.id));
    internal static Car? CarFor(string id) => TrainController.Shared.TryGetCarForId(id, out var car) ? car : null;
    internal static void Claim(AeLocalJob job, Car car)
    {
        if (!job.OwnedIds.Contains(car.id) && !string.IsNullOrEmpty(car.trainCrewId) && car.trainCrewId != job.CrewId)
            job.OriginalCrews[car.id] = car.trainCrewId;
        if (!job.OwnedIds.Contains(car.id)) job.OwnedIds.Add(car.id);
        NpcTrainOperations.Trusted(() => car.KeyValueObject[Marker] = Value.String(job.Id));
        if (job.CrewId.Length > 0) NpcTimetableRegistry.SetCrew(car, job.CrewId);
    }
    private static void Release(AeLocalJob job, Car car)
    {
        NpcTrainOperations.Trusted(() => car.KeyValueObject[Marker] = Value.String(string.Empty));
        job.OwnedIds.Remove(car.id);
        if (job.OriginalCrews.TryGetValue(car.id, out string crew) && StateManager.Shared.PlayersManager.TrainCrewForId(crew, out _))
            NpcTimetableRegistry.SetCrew(car, crew);
        job.OriginalCrews.Remove(car.id);
        ThroughTrafficGuard.ForgetNotice(car.id);
    }
    internal static void Enter(BaseLocomotive lead)
    {
        if (!StateManager.IsHost) { Toast.Present("AE Local setup is controlled by the host."); return; }
        if (!NpcServiceStore.Load()) return;
        if (Job(lead) is AeLocalJob existing) { AeLocalWindow.Show(existing); return; }
        var cars = lead.EnumerateCoupled().ToList();
        if (!lead.IsStopped() || cars.Any(c => ThroughTrafficGuard.IsGenerated(c) || Owns(c) || c.IsDerailed))
        { Toast.Present("Stop a player consist with no NPC or already assigned cars before entering AE Local."); return; }
        var job = new AeLocalJob { LeadId = lead.id, HomeArea = OpsController.Shared.ClosestArea(lead)?.identifier ?? "",
            HomePoint = Graph.Shared.LocationToString(lead.LocationF), PowerIds = cars.Where(c => c.MotivePower()).Select(c => c.id).ToList() };
        NpcServiceStore.State.LocalJobs.Add(job);
        foreach (var car in cars) Claim(job, car);
        NpcTrainOperations.Hold(lead);
        NpcServiceStore.Save();
        foreach (var adapter in UnityEngine.Resources.FindObjectsOfTypeAll<LocomotiveControlsUIAdapter>().Where(a => a.isActiveAndEnabled)) adapter.UpdateSelectedControlSet();
        AeLocalWindow.Show(job);
    }
    internal static void Cancel(AeLocalJob job)
    {
        if (!StateManager.IsHost) return;
        Patches.AutoEngineerRunaround.Abort(job.LeadId);
        if (CarFor(job.LeadId) is BaseLocomotive lead) NpcTrainOperations.Hold(lead);
        foreach (var car in job.OwnedIds.ToList().Select(CarFor).Where(c => c != null)) Release(job, car!);
        NpcServiceStore.State.LocalJobs.Remove(job);
        NpcServiceStore.Save();
        foreach (var adapter in UnityEngine.Resources.FindObjectsOfTypeAll<LocomotiveControlsUIAdapter>().Where(a => a.isActiveAndEnabled)) adapter.UpdateSelectedControlSet();
        ModDiagnosticLog.Write("AE LOCAL", job.Id + ": cancelled; equipment returned to player control.");
    }
    internal static IEnumerable<Industry> Industries() => OpsController.Shared.AllIndustries.Where(i => !i.ProgressionDisabled &&
        i.Components.Any(c => !c.ProgressionDisabled && c.TrackSpans.Any()) && !OpsController.Shared.AllInterchanges.Any(x => x.Industry == i));
    private static bool Selected(AeLocalJob job, OpsCarPosition destination) =>
        job.IndustryIds.Any(id => destination.Identifier == id || destination.Identifier.StartsWith(id + ".", StringComparison.Ordinal));
    private static bool Deliver(AeLocalJob job, AeLocalTask task) => !task.Relocation && AeLocalPolicy.DeliverToWaybill(task.Pickup, task.DirectDelivery,
        Selected(job, OpsController.Shared.ResolveOpsCarPosition(task.Destination)));
    internal static string DestinationLabel(AeLocalTask task) => task.Relocation ? "Temporary home-yard storage" : OpsController.Shared.ResolveOpsCarPosition(task.Destination).DisplayName;
    private static bool InSelectedIndustry(AeLocalJob job, Car car) => Industries().Where(i => job.IndustryIds.Contains(i.identifier))
        .SelectMany(i => i.Components).SelectMany(c => c.TrackSpans).Any(s => s.Contains(car.OpsLocation));
    internal static string Summary(AeLocalJob job)
    {
        int setouts = job.Tasks.Count(t => Deliver(job, t));
        int delivered = job.Tasks.Count(t => t.Done && Deliver(job, t));
        return $"{AeLocalPolicy.Progress(job.Tasks.Count(t => t.Done), job.Tasks.Count)}% S{delivered}/{setouts} · P{job.Tasks.Count(t => t.Pickup && t.Collected)}/{job.Tasks.Count(t => t.Pickup)} {job.Phase} · {job.Status}";
    }
    internal static void Start(AeLocalJob job)
    {
        if (!StateManager.IsHost || CarFor(job.LeadId) is not BaseLocomotive lead) return;
        AeLocalCrew.Ensure(job, lead);
        foreach (var id in job.OwnedIds) if (CarFor(id) is Car assigned) NpcTimetableRegistry.SetCrew(assigned, job.CrewId);
        if (job.YardPoint.Length == 0 || job.ParkingPoint.Length == 0)
        { Toast.Present("Select the yard return track and a final parking waypoint."); return; }
        var yard = Graph.Shared.ResolveLocationString(job.YardPoint);
        var home = OpsController.Shared.Areas.FirstOrDefault(a => a.identifier == job.HomeArea);
        if (!yard.IsValid || home == null || !home.Contains(yard.GetPosition()))
        { Toast.Present("Select a yard return track in the area where this job originated."); return; }
        if (job.YardSpan.Length == 0) { Toast.Present("The yard waypoint must be on a named yard/industry track span, clear of the mainline."); return; }
        job.Tasks.Clear();
        foreach (var car in lead.EnumerateCoupled().Where(c => !c.MotivePower() && c.Waybill.HasValue && !c.Waybill.Value.Completed))
            job.Tasks.Add(new AeLocalTask { CarId = car.id, Destination = car.Waybill!.Value.Destination.Identifier, DirectDelivery = true });
        foreach (var car in lead.EnumerateCoupled().Where(c => !c.MotivePower() && !job.Tasks.Any(t => t.CarId == c.id)))
            job.Tasks.Add(new AeLocalTask { CarId = car.id, Destination = job.YardSpan, Pickup = true, Collected = true, Relocation = true });
        foreach (var car in TrainController.Shared.Cars.Where(c => !c.IsInBardo && !c.IsLocomotive && !c.MotivePower() && !c.IsCaboose() && c.IsStopped() &&
            !ThroughTrafficGuard.IsGenerated(c) && !Owns(c) && c.Waybill.HasValue))
        {
            var wb = car.Waybill!.Value;
            var current = OpsController.Shared.PositionForCar(car);
            bool ready = current.HasValue && AeLocalPolicy.ReadyPickup(current.Value.Identifier, wb.Destination.Identifier, wb.Completed);
            bool nearby = job.IncludeNearby && !wb.Completed && wb.Destination.Spans.Any() && home.Contains(car.OpsLocation.GetPosition()) &&
                !wb.Destination.Spans.Any(s => s.Contains(car.LocationF) && s.Contains(car.LocationR));
            if (!ready && !nearby || !InSelectedIndustry(job, car) && !nearby) continue;
            job.Tasks.Add(new AeLocalTask { CarId = car.id, Destination = wb.Destination.Identifier, Pickup = true,
                DirectDelivery = nearby, Optional = nearby && !InSelectedIndustry(job, car) });
        }
        if (job.Tasks.Count == 0) { job.Status = "No pending waybill work found; select pickup industries or nearby cars"; Toast.Present(job.Status); return; }
        job.Phase = "Planning"; job.Status = "Sorting the assigned consist into physical delivery cuts";
        job.Paused = false;
        NpcServiceStore.Save();
        ModDiagnosticLog.Write("AE LOCAL", $"{job.Id}: started with {job.Tasks.Count(t => !t.Pickup)} setouts and {job.Tasks.Count(t => t.Pickup)} pickup candidates; industries={string.Join(",", job.IndustryIds)}; nearby={job.IncludeNearby}.");
        ModDiagnosticLog.Write("AE LOCAL MANIFEST", job.Id + ": " + string.Join("; ", job.Tasks.Select(t =>
            (CarFor(t.CarId)?.DisplayName ?? t.CarId) + " -> " + DestinationLabel(t) + (Deliver(job, t) ? " [deliver]" : " [return yard]"))));
    }
    internal static void Tick()
    {
        if (!StateManager.IsHost || RestoreNotifier.Shared == null || !RestoreNotifier.Shared.HasRestored ||
            TrainController.Shared == null || OpsController.Shared == null || Graph.Shared == null || !NpcServiceStore.Load()) return;
        if (loadedGeneration != NpcServiceStore.Generation)
        {
            loadedGeneration = NpcServiceStore.Generation;
            foreach (var job in NpcServiceStore.State.LocalJobs) job.RestoreOrders = true;
        }
        if (Time.realtimeSinceStartup < nextTick) return;
        nextTick = Time.realtimeSinceStartup + 1;
        foreach (var job in NpcServiceStore.State.LocalJobs.ToList())
        {
            try { Advance(job); if (job.Phase != "Setup") AeLocalCrew.Sync(job); }
            catch (Exception ex) { job.Paused = true; job.Status = "Switching interrupted; inspect the debug log, then retry"; if (CarFor(job.LeadId) is BaseLocomotive lead) NpcTrainOperations.Hold(lead); TweaksAndThingsPlugin.LogException("AE Local " + job.Id, ex); }
        }
        NpcServiceStore.Save();
    }
    private static void Advance(AeLocalJob job)
    {
        if (CarFor(job.LeadId) is not BaseLocomotive lead) { job.Status = "Assigned locomotive missing"; return; }
        string engineerStatus = new AutoEngineerPersistence(lead.KeyValueObject).PlannerStatus ?? "";
        if (engineerStatus != job.NativeStatus)
        {
            job.NativeStatus = engineerStatus;
            if (job.Phase != "Setup" && job.Phase != "Complete")
                ModDiagnosticLog.Write("AE LOCAL ENGINEER", $"{lead.DisplayName}: {engineerStatus}; work={job.Status}.", "local-engineer:" + job.Id, 10);
        }
        if (job.PickingTarget.Length > 0)
        {
            var wp = new AutoEngineerPersistence(lead.KeyValueObject).Orders.Waypoint;
            if (!wp.HasValue) return;
            AcceptWaypoint(job, lead, wp.Value.LocationString); return;
        }
        if (job.Phase == "Setup" || job.Phase == "Complete" || job.Paused) return;
        if (job.Phase == "Waiting capacity")
        {
            if (Game.TimeWeather.Now.TotalSeconds < job.RetryAt) return;
            job.Phase = "Planning";
        }
        if (job.Phase == "Runaround" && !Patches.AutoEngineerRunaround.InProgress(lead.id))
        {
            if (job.CoupleTo.Length > 0 && !lead.EnumerateCoupled().Any(c => c.id == job.CoupleTo))
            { Move(job, lead, Graph.Shared.ResolveLocationString(job.Target), "Recoupled", "Resuming saved runaround coupling", job.CoupleTo); return; }
            NpcTrainOperations.Hold(lead); job.Phase = "Planning"; job.Status = "Runaround coupled; checking the next delivery cut";
        }
        if (lead.EnumerateCoupled().Any(c => !job.OwnedIds.Contains(c.id))) { Block(job, lead, "Unassigned cars coupled to this job; cancel and configure again"); return; }
        if (lead.EnumerateCoupled().Any(c => c.IsDerailed)) { Block(job, lead, "Assigned equipment is derailed"); return; }
        if (job.RestoreOrders)
        {
            job.RestoreOrders = false;
            if (job.Phase == "Moving") { SendOrder(job, lead); return; }
            NpcTrainOperations.Hold(lead);
        }
        if (job.Phase == "Separating cut")
        {
            if (!lead.IsStopped() || !lead.EnumerateCoupled().Select(c => c.id).ToHashSet().SetEquals(job.MoveIds)) return;
            job.Phase = "Moving"; SendOrder(job, lead); return;
        }
        if (job.Phase == "Moving")
        {
            if (job.CoupleTo.Length > 0)
            {
                if (!lead.EnumerateCoupled().Any(c => c.id == job.CoupleTo) || !lead.IsStopped())
                {
                    if (lead.IsStopped() && Game.TimeWeather.Now.TotalSeconds - job.MoveStarted >= 30) TryClearRoute(job, lead);
                    return;
                }
            }
            else if (!lead.IsStopped() || !MoveReached(job, lead))
            {
                if (lead.IsStopped() && Game.TimeWeather.Now.TotalSeconds - job.MoveStarted >= 30) TryClearRoute(job, lead);
                return;
            }
            NpcTrainOperations.Hold(lead);
            job.Phase = job.AfterMove;
        }
        if (!lead.IsStopped()) return;
        if (job.Phase == "Stage cut")
        {
            var staged = job.StageDeferredIds.Select(CarFor).Where(c => c != null).Cast<Car>().ToList();
            var span = UnityEngine.Resources.FindObjectsOfTypeAll<TrackSpan>().FirstOrDefault(s => s.id == job.StageSpan && s.gameObject.scene.IsValid());
            if (staged.Count != job.StageDeferredIds.Count || span == null || staged.Any(c => !span.Contains(c.LocationF) || !span.Contains(c.LocationR)) || !ClearOfSwitches(staged))
            { Block(job, lead, "Staging move has not cleared the turnout or storage-track bounds"); return; }
            Split(job, lead, job.StageKeepIds.ToHashSet());
            if (lead.EnumerateCoupled().Any(c => job.StageDeferredIds.Contains(c.id)))
            { job.Status = "Securing deferred cut and waiting for physical separation"; return; }
            job.YardRemainder = AeLocalPolicy.DeferredCars(job.YardRemainder, job.StageDeferredIds, Array.Empty<string>());
            PlanNote(job, $"Parked {staged.Count} deferred cars on {span.id}; {job.Tasks.Count(t => !t.Done)} tasks retained; working the connected half next");
            job.StageKeepIds.Clear(); job.StageDeferredIds.Clear(); job.StageSpan = "";
            job.Phase = "Planning"; return;
        }
        if (job.Phase == "Deliver")
        {
            var cars = job.WorkingCut.Select(CarFor).Where(c => c != null).Cast<Car>().ToList();
            var destination = OpsController.Shared.ResolveOpsCarPosition(job.Tasks.First(t => job.WorkingCut.Contains(t.CarId)).Destination);
            if (cars.Count != job.WorkingCut.Count || cars.Any(c => !destination.Spans.Any(s => s.Contains(c.LocationF) && s.Contains(c.LocationR))) || !ClearOfSwitches(cars))
            { Block(job, lead, "Delivery cut has not cleared the industry's switches/track bounds"); return; }
            Split(job, lead, job.PowerIds.ToHashSet());
            if (lead.EnumerateCoupled().Any(c => job.WorkingCut.Contains(c.id)))
            { job.Status = "Waiting for delivery couplers to separate"; return; }
            foreach (var car in cars) { job.Tasks.First(t => t.CarId == car.id && !t.Done).Done = true; Release(job, car); }
            job.WorkingCut.Clear(); job.RunaroundAttempted = false; job.Phase = "Planning"; job.Status = "Setout secured; planning next task"; return;
        }
        if (job.Phase == "Collected")
        {
            foreach (var task in job.Tasks.Where(t => t.Pickup && !t.Done && lead.EnumerateCoupled().Any(c => c.id == t.CarId))) task.Collected = true;
            AirAndBrakes(lead);
            job.Phase = "Planning"; job.Status = "Pickup coupled; sorting its destination"; return;
        }
        if (job.Phase == "Yard drop")
        {
            var span = OpsController.Shared.AllIndustryComponents.SelectMany(c => c.TrackSpans).FirstOrDefault(s => s.id == job.YardSpan);
            var freight = job.WorkingCut.Select(CarFor).Where(c => c != null).Cast<Car>().ToList();
            if (freight.Count == 0) freight = lead.EnumerateCoupled().Where(c => !job.PowerIds.Contains(c.id)).ToList();
            if (span == null || freight.Any(c => !span.Contains(c.LocationF) || !span.Contains(c.LocationR)) || !ClearOfSwitches(freight))
            { Block(job, lead, "Return cut is not wholly inside the selected yard track; adjust the yard target"); return; }
            Split(job, lead, job.PowerIds.ToHashSet());
            if (lead.EnumerateCoupled().Any(c => !job.PowerIds.Contains(c.id)))
            { job.Status = "Waiting for yard cut couplers to separate"; return; }
            var retained = freight.Where(c => job.Tasks.Any(t => t.CarId == c.id && !t.Done && Deliver(job, t))).ToList();
            NpcTrainOperations.Trusted(() =>
            {
                foreach (var car in retained)
                foreach (var end in new[] { Car.LogicalEnd.A, Car.LogicalEnd.B })
                    if (car.CoupledTo(end) is Car adjacent && !retained.Contains(adjacent)) Open(car, end);
            });
            foreach (var car in freight)
            {
                var task = job.Tasks.FirstOrDefault(t => t.CarId == car.id && !t.Done);
                if (task != null && task.Pickup && !Deliver(job, task)) task.Done = true;
                if (!retained.Contains(car)) Release(job, car);
            }
            job.YardRemainder = job.YardRemainder.Concat(retained.Select(c => c.id)).Distinct().Where(id => Owns(CarFor(id))).ToList();
            job.ClearanceReturn = false; job.WorkingCut.Clear();
            job.Phase = "Planning"; return;
        }
        if (job.Phase == "Parked")
        {
            if (!ClearOfSwitches(lead.EnumerateCoupled().ToList()))
            { Block(job, lead, "Final parking position fouls a switch; choose a safe parking target before restarting the local"); return; }
            NpcTrainOperations.Trusted(() => { foreach (var car in lead.EnumerateCoupled()) car.SetHandbrake(true); });
            job.Phase = "Complete"; job.Status = "Local complete; pickups returned and power parked";
            foreach (var car in job.OwnedIds.ToList().Select(CarFor).Where(c => c != null)) Release(job, car!);
            ModDiagnosticLog.Write("AE LOCAL", job.Id + ": completed; equipment released to player control."); return;
        }
        if (job.Phase == "At yard" || job.Phase == "Recoupled") job.Phase = "Planning";
        if (job.Phase == "Planning") Plan(job, lead);
    }
    private static void Plan(AeLocalJob job, BaseLocomotive lead)
    {
        var coupled = AeLocalSwitchingPlan.PowerFirst(job, lead);
        var freight = coupled.Where(c => !job.PowerIds.Contains(c.id)).ToList();
        job.PlanningPass++;
        PlanNote(job, $"Plan {job.PlanningPass}: {freight.Count} coupled freight, {job.YardRemainder.Count} deferred, {job.Tasks.Count(t => !t.Done)} tasks; analyzing accessible destination groups");
        if (job.PendingObstruction.Length > 0)
        {
            if (freight.Count > 0)
            { MoveHome(job, lead, "Yard drop", "Securing the current freight before power goes to clear the obstruction"); return; }
            var obstruction = CarFor(job.PendingObstruction);
            job.PendingObstruction = "";
            if (obstruction != null && TryClearCut(job, lead, obstruction)) return;
        }
        if (job.ClearanceReturn && freight.Count > 0)
        { MoveHome(job, lead, "Yard drop", "Storing the obstructing cut clear of switches at the home yard"); return; }
        var pending = freight.Where(c => job.Tasks.Any(t => t.CarId == c.id && !t.Done && (!t.Pickup || t.Collected) &&
            Deliver(job, t))).ToList();
        if (pending.Count > 0)
        {
            // Keep all motive power in a contiguous end unit. Sorting proceeds
            // through real cuts, with the remainder secured at the home yard.
            if (!coupled.Take(job.PowerIds.Count).All(c => job.PowerIds.Contains(c.id)))
            {
                bool started = false;
                if (!job.RunaroundAttempted)
                {
                    job.RunaroundAttempted = true;
                    NpcTrainOperations.Trusted(() => started = Patches.AutoEngineerRunaround.TryStart(lead, Graph.Shared.ResolveLocationString(job.YardPoint), ""));
                }
                if (started)
                {
                    var tail = freight.SelectMany(c => new[] { Car.LogicalEnd.A, Car.LogicalEnd.B }
                        .Where(e => c.CoupledTo(e) == null).Select(e => (car: c, end: e))).LastOrDefault();
                    if (tail.car != null) { job.CoupleTo = tail.car.id; job.Target = Graph.Shared.LocationToString(tail.car.LocationFor(tail.end)); }
                    job.Phase = "Runaround"; job.Status = "Running power around the secured consist to reach its delivery end"; return;
                }
                Block(job, lead, "Power must be a contiguous unit at the end of the consist; no safe automatic runaround was available"); return;
            }
            if (ConsiderOtherEnd(job, lead, coupled, freight)) return;
            var cutIds = AeLocalPolicy.LeadingCut(freight.Select(c => (c.id,
                job.Tasks.FirstOrDefault(t => t.CarId == c.id && !t.Done && (!t.Pickup || t.Collected))?.Destination ?? "yard")));
            var firstTask = job.Tasks.FirstOrDefault(t => t.CarId == cutIds[0] && !t.Done && (!t.Pickup || t.Collected));
            var cut = cutIds.Select(CarFor).Cast<Car>().ToList();
            var remainder = freight.Where(c => !cutIds.Contains(c.id)).ToList();
            if (remainder.Count > 0)
            {
                var yard = OpsController.Shared.AllIndustryComponents.SelectMany(c => c.TrackSpans).FirstOrDefault(s => s.id == job.YardSpan);
                if (!SafeStorage(job, remainder))
                { if (StageBatch(job, lead, coupled, "Sorting requires switch-clear storage for the rest of the cut")) return;
                  MoveHome(job, lead, "At yard", "Returning to the yard to sort the remaining delivery cuts"); return; }
            }
            if (firstTask == null || !Deliver(job, firstTask))
            {
                // Peel off retained cars at the home yard, then return for the
                // delivery cars behind them, instead of teleporting a sort.
                job.YardRemainder = AeLocalPolicy.DeferredCars(job.YardRemainder, remainder.Select(c => c.id), Array.Empty<string>());
                Split(job, lead, job.PowerIds.Concat(cutIds).ToHashSet());
                job.MoveIds = job.PowerIds.Concat(cutIds).ToList();
                MoveHome(job, lead, "Yard drop", "Sorting retained cars onto the home yard track"); return;
            }
            var destination = OpsController.Shared.ResolveOpsCarPosition(firstTask.Destination);
            var moving = coupled.Where(c => job.PowerIds.Contains(c.id) || cutIds.Contains(c.id)).ToList();
            Location front = Location.Invalid;
            bool fit = false;
            foreach (int count in AeLocalPolicy.BinaryCuts(cutIds.Count))
            {
                cutIds = cutIds.Take(count).ToList();
                cut = cutIds.Select(CarFor).Cast<Car>().ToList();
                moving = coupled.Where(c => job.PowerIds.Contains(c.id) || cutIds.Contains(c.id)).ToList();
                bool berth = Berth(job, lead, destination.Spans, moving, out front, freightOnly: true);
                bool power = berth && EnoughPower(lead, moving, front);
                PlanNote(job, $"Testing {count}-car group for {destination.DisplayName}: berth={(berth ? "fits" : "no clear spot")}, power={(power ? "adequate" : "not yet feasible")}");
                if (power) { fit = true; break; }
            }
            if (!fit)
            {
                var obstacle = destination.Spans.SelectMany(s => TrainController.Shared.CarsOnSpan(s)).Distinct()
                    .Where(c => !job.OwnedIds.Contains(c.id)).OrderBy(c => PickupDistance(lead, c)).FirstOrDefault();
                if (obstacle != null && TryClearCut(job, lead, obstacle)) return;
                if (TryClearDestinationRoute(job, lead, destination)) return;
                if (TryRunaroundForNextGroup(job, lead, coupled, firstTask.Destination)) return;
                if (DeferBlockedPrefix(job, lead, coupled, AeLocalPolicy.LeadingCut(freight.Select(c => (c.id,
                    job.Tasks.FirstOrDefault(t => t.CarId == c.id && !t.Done)?.Destination ?? "yard"))), firstTask.Destination)) return;
                Block(job, lead, $"Even one car cannot be safely spotted at {destination.DisplayName}; pull/shove berths, power, relocation and alternate end checked. {job.PlanDetail}"); return;
            }
            remainder = freight.Where(c => !cutIds.Contains(c.id)).ToList();
            if (remainder.Count > 0 && !SafeStorage(job, remainder))
            { if (StageBatch(job, lead, coupled, "Destination/power requires a smaller connected batch")) return;
              MoveHome(job, lead, "At yard", "Returning to sort smaller cuts that fit the industry"); return; }
            if (!EnoughPower(lead, moving, front)) { Block(job, lead, "Insufficient tractive effort for this delivery cut and route"); return; }
            job.WorkingCut = cutIds;
            if (remainder.Count > 0)
            {
                job.YardRemainder = AeLocalPolicy.DeferredCars(job.YardRemainder, remainder.Select(c => c.id), Array.Empty<string>());
                Split(job, lead, moving.Select(c => c.id).ToHashSet());
                job.MoveIds = moving.Select(c => c.id).ToList();
            }
            Move(job, lead, front, "Deliver", $"{(job.SpotShove ? "Shoving" : "Pulling")} {cut.Count} cars to {destination.DisplayName}; {job.PlanDetail}"); return;
        }
        if (freight.Count > 0)
        { MoveHome(job, lead, "Yard drop", $"Returning {freight.Count} pickup/retained cars to the home yard"); return; }
        job.YardRemainder.RemoveAll(id => CarFor(id) == null || !Owns(CarFor(id)));
        if (job.YardRemainder.Count > 0 && job.YardRemainder.Select(CarFor).Where(c => c != null).Any(c =>
            c!.EnumerateCoupled().Any(item => !job.Tasks.Any(t => t.CarId == item.id) || job.Tasks.Any(t => t.CarId == item.id && !t.Done && !RetryLater(job, t.Destination)))))
        {
            var tail = job.YardRemainder.Select(CarFor).Where(c => c != null &&
                (c.CoupledTo(Car.LogicalEnd.A) == null || c.CoupledTo(Car.LogicalEnd.B) == null))
                .OrderBy(c => DeferredCost(job, lead, c!)).FirstOrDefault();
            if (tail == null) { Block(job, lead, "Deferred cuts have no exposed coupler; inspect their physical connections"); return; }
            var retrieved = tail.EnumerateCoupled().Select(c => c.id).ToHashSet();
            if (!Couple(job, lead, tail, "Recoupled", "Returning to the secured yard cut for the next deliveries")) return;
            job.YardRemainder = AeLocalPolicy.DeferredCars(job.YardRemainder, Array.Empty<string>(), retrieved); return;
        }
        var pickup = job.Tasks.Where(t => t.Pickup && !t.Collected && !t.Done)
            .Where(t => !RetryLater(job, t.Destination))
            .OrderBy(t => PickupDistance(lead, CarFor(t.CarId))).FirstOrDefault();
        if (pickup != null)
        {
            var car = CarFor(pickup.CarId);
            if (car == null || car.IsInBardo || !car.IsStopped() || ThroughTrafficGuard.IsGenerated(car) || Owns(car) && !job.OwnedIds.Contains(car.id))
            { Block(job, lead, "Reserved pickup is missing, moving, or belongs to another train"); return; }
            if (!car.Waybill.HasValue || car.Waybill.Value.Destination.Identifier != pickup.Destination || car.Waybill.Value.Completed)
            { pickup.Done = true; pickup.Skipped = true; job.Status = "Pickup waybill changed; skipped stale request"; return; }
            var wholePickup = car.EnumerateCoupled().ToList();
            var exposed = wholePickup.Where(c => c.CoupledTo(Car.LogicalEnd.A) == null || c.CoupledTo(Car.LogicalEnd.B) == null)
                .OrderBy(c => PickupDistance(lead, c)).FirstOrDefault();
            if (exposed == null) { Block(job, lead, "Pickup string has no exposed coupler"); return; }
            if (wholePickup.Any(c => c.IsLocomotive || ThroughTrafficGuard.IsGenerated(c) || Owns(c) && !job.OwnedIds.Contains(c.id) || !c.IsStopped()))
            { Block(job, lead, "Pickup is coupled to protected or moving equipment"); return; }
            var orderedPickup = AeLocalSwitchingPlan.FromEnd(exposed);
            List<Car>? pickupCut = null;
            foreach (int count in AeLocalPolicy.BinaryCuts(orderedPickup.Count))
            {
                var candidate = orderedPickup.Take(count).ToList();
                if (!EnoughPower(lead, coupled.Concat(candidate).Distinct().ToList(), Graph.Shared.ResolveLocationString(job.YardPoint), exposed.LocationF)) continue;
                pickupCut = candidate; break;
            }
            if (pickupCut == null)
            {
                if (pickup.Optional) { pickup.Done = true; pickup.Skipped = true; job.Status = "Optional nearby car left in place: insufficient power"; return; }
                Block(job, lead, "Insufficient tractive effort to return this pickup; cancel or provide more power"); return;
            }
            if (pickupCut.Count < wholePickup.Count)
            {
                var selectedIds = pickupCut.Select(c => c.id).ToHashSet();
                NpcTrainOperations.Trusted(() =>
                {
                    NpcSetoutPlacement.SecureFreight(wholePickup);
                    foreach (var selectedCar in pickupCut)
                    foreach (var end in new[] { Car.LogicalEnd.A, Car.LogicalEnd.B })
                        if (selectedCar.CoupledTo(end) is Car other && !selectedIds.Contains(other.id)) Open(selectedCar, end);
                });
                PlanNote(job, $"Pickup binary split: taking accessible {pickupCut.Count}/{wholePickup.Count}; {wholePickup.Count - pickupCut.Count} cars remain for a later trip");
            }
            foreach (var item in pickupCut)
            {
                Claim(job, item);
                var task = job.Tasks.FirstOrDefault(t => t.CarId == item.id && !t.Done);
                if (task == null && item.Waybill.HasValue && !item.Waybill.Value.Completed)
                    job.Tasks.Add(new AeLocalTask { CarId = item.id, Destination = item.Waybill.Value.Destination.Identifier,
                        Pickup = true, DirectDelivery = true });
            }
            NpcTrainOperations.Trusted(() =>
            {
                NpcSetoutPlacement.SecureFreight(pickupCut);
            });
            Couple(job, lead, exposed, "Collected", $"Collecting {pickupCut.Count}-car string including {car.DisplayName}; sort at the yard before delivery"); return;
        }
        if (job.Tasks.Any(t => !t.Done))
        {
            var retries = job.Tasks.Where(t => !t.Done && RetryLater(job, t.Destination)).Select(t => job.DestinationRetry[t.Destination]).ToList();
            if (retries.Count > 0)
            {
                job.RetryAt = retries.Min(); job.Phase = "Waiting capacity";
                PlanNote(job, $"Other accessible work finished; {job.Tasks.Count(t => !t.Done)} cars retained, rechecking unavailable spots in {Math.Ceiling((job.RetryAt - Game.TimeWeather.Now.TotalSeconds) / 60)} min");
                NpcTrainOperations.Hold(lead); return;
            }
            Block(job, lead, "Undelivered assigned cars are no longer coupled or in the secured yard cut; inspect the job before retrying"); return;
        }
        Move(job, lead, Graph.Shared.ResolveLocationString(job.ParkingPoint), "Parked", "All freight tasks finished; moving to final parking");
    }
    private static float PickupDistance(BaseLocomotive lead, Car? car)
    {
        if (car == null) return float.MaxValue;
        return new[] { Car.LogicalEnd.A, Car.LogicalEnd.B }.Where(e => car.CoupledTo(e) == null)
            .Select(e => NpcTrainOperations.Route(lead.LocationF, car.LocationFor(e), out _, out float distance) ? distance : float.MaxValue)
            .DefaultIfEmpty(float.MaxValue).Min();
    }
    private static void TryClearRoute(AeLocalJob job, BaseLocomotive lead)
    {
        var targets = lead.AutoEngineerPlanner._engineer._targets.AllTargets;
        if (targets != null && targets.Any(t => t.SpeedMph == 0 && (t.Reason ?? "").IndexOf("signal", StringComparison.OrdinalIgnoreCase) >= 0)) return;
        var target = Graph.Shared.ResolveLocationString(job.Target);
        if (!NpcTrainOperations.Route(lead.LocationF, target, out var route, out _)) return;
        var obstacle = TrainController.Shared.Cars.Where(c => !c.IsInBardo && !job.OwnedIds.Contains(c.id) &&
            Vector3.Distance(c.OpsLocation.GetPosition(), lead.LocationF.GetPosition()) < 100 && route.Any(s =>
                new[] { c.LocationF, c.LocationR, c.OpsLocation }.Any(p => p.segment == s.Location.segment &&
                    p.WithEnd(s.Location.end).distance <= s.Location.distance + 2 &&
                    p.WithEnd(s.Location.end).distance >= s.Location.distance - s.Distance - 2)))
            .OrderBy(c => Vector3.Distance(c.OpsLocation.GetPosition(), lead.LocationF.GetPosition())).FirstOrDefault();
        if (obstacle != null)
        {
            if (TryClearCut(job, lead, obstacle)) return;
            if (job.CoupleTo.Length > 0)
            {
                var task = job.Tasks.FirstOrDefault(t => t.CarId == job.CoupleTo && !t.Done);
                if (task != null) job.DestinationRetry[task.Destination] = Game.TimeWeather.Now.TotalSeconds + 600;
            }
            job.RejectedSpots[job.Target] = Game.TimeWeather.Now.TotalSeconds + 600;
            NpcTrainOperations.Hold(lead); job.Phase = "Planning"; return;
        }
        bool stoppedShort = (job.AfterMove == "Deliver" || job.AfterMove == "Stage cut" || job.AfterMove == "Yard drop" || job.AfterMove == "At yard") &&
            lead.AutoEngineerPlanner._manualStopDistance.HasValue && lead.AutoEngineerPlanner._manualStopDistance.Value <= 0.5 &&
            job.NativeStatus.IndexOf("brake", StringComparison.OrdinalIgnoreCase) < 0 && !SpotSatisfied(job, lead);
        if (job.CoupleTo.Length == 0 && Game.TimeWeather.Now.TotalSeconds - job.MoveStarted >= 60 &&
            (stoppedShort || job.NativeStatus.IndexOf("long", StringComparison.OrdinalIgnoreCase) >= 0 ||
             job.NativeStatus.IndexOf("blocked", StringComparison.OrdinalIgnoreCase) >= 0 ||
             job.NativeStatus.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            job.RejectedSpots[job.Target] = Game.TimeWeather.Now.TotalSeconds + 600;
            PlanNote(job, "Native engineer cannot complete this route; trying a different berth or smaller physical batch: " + job.NativeStatus);
            NpcTrainOperations.Hold(lead);
            if (!StageBatch(job, lead, AeLocalSwitchingPlan.PowerFirst(job, lead), "Native route/length limits require a smaller batch"))
                job.Phase = "Planning";
            return;
        }
        if (job.CoupleTo.Length > 0 && Game.TimeWeather.Now.TotalSeconds - job.MoveStarted >= 60 &&
            Graph.Shared.CheckSameRoute(lead.LocationF, target, 5))
            Block(job, lead, "Stopped beside the pickup coupler without coupling; inspect the cut/knuckles and retry. Native status: " +
                new AutoEngineerPersistence(lead.KeyValueObject).PlannerStatus);
    }
    private static bool TryClearCut(AeLocalJob job, BaseLocomotive lead, Car obstacle)
    {
        var cut = obstacle.EnumerateCoupled().ToList();
        if (cut.Any(c => c.IsLocomotive || c.MotivePower() || !c.IsStopped() || c.IsDerailed || ThroughTrafficGuard.IsGenerated(c) ||
            Owns(c) && !job.OwnedIds.Contains(c.id)))
        { PlanNote(job, "Cannot relocate this obstruction: locomotive, moving train or other job; checking alternate work"); return false; }
        int completed = job.Tasks.Count(t => t.Done);
        if (cut.Any(c => job.RelocationProgress.TryGetValue(c.id, out int progress) && progress == completed))
        { PlanNote(job, "Skipping a repeated relocation with no completed work; checking another cut/end"); return false; }
        if (lead.EnumerateCoupled().Any(c => !job.PowerIds.Contains(c.id)))
        {
            job.PendingObstruction = obstacle.id;
            MoveHome(job, lead, "Yard drop", "Staging current cars before collecting the obstruction with power only");
            return true;
        }
        var exposed = cut.Where(c => c.CoupledTo(Car.LogicalEnd.A) == null || c.CoupledTo(Car.LogicalEnd.B) == null)
            .OrderBy(c => PickupDistance(lead, c)).FirstOrDefault();
        if (exposed == null) { PlanNote(job, "Obstructing string has no reachable exposed coupler; checking alternative work"); return false; }
        var whole = AeLocalSwitchingPlan.FromEnd(exposed);
        List<Car>? selected = null;
        foreach (int count in AeLocalPolicy.BinaryCuts(whole.Count))
        {
            var candidate = whole.Take(count).ToList();
            var haul = AeLocalSwitchingPlan.PowerFirst(job, lead).Concat(candidate).Distinct().ToList();
            if (!StagingSpans(job).Any(s => Berth(job, lead, new[] { s }, haul, out var target, freightOnly: true,
                    containedIds: candidate.Select(c => c.id).ToHashSet()) && EnoughPower(lead, haul, target, exposed.LocationF))) continue;
            selected = candidate; break;
        }
        if (selected == null) { PlanNote(job, "No accessible obstruction batch fits power and clear storage, even one car; checking alternative work"); return false; }
        cut = selected;
        if (cut.Count < whole.Count)
        {
            var selectedIds = cut.Select(c => c.id).ToHashSet();
            NpcTrainOperations.Trusted(() =>
            {
                NpcSetoutPlacement.SecureFreight(whole);
                foreach (var member in cut)
                foreach (var end in new[] { Car.LogicalEnd.A, Car.LogicalEnd.B })
                    if (member.CoupledTo(end) is Car other && !selectedIds.Contains(other.id)) Open(member, end);
            });
            PlanNote(job, $"Clearing obstruction in binary batches: {cut.Count}/{whole.Count} now, remainder stays secured");
        }
        foreach (var car in cut)
        {
            Claim(job, car); job.ClearedObstacles.Add(car.id);
            job.RelocationProgress[car.id] = completed;
            if (job.Tasks.Any(t => t.CarId == car.id && !t.Done)) continue;
            bool pending = car.Waybill.HasValue && !car.Waybill.Value.Completed;
            job.Tasks.Add(new AeLocalTask { CarId = car.id, Pickup = true, DirectDelivery = pending, Relocation = !pending,
                Destination = pending ? car.Waybill!.Value.Destination.Identifier : job.YardSpan });
        }
        NpcSetoutPlacement.SecureFreight(cut);
        job.ClearanceReturn = true;
        ModDiagnosticLog.Write("AE LOCAL CLEARANCE", $"{job.Id}: collecting obstructing {cut.Count}-car string for switch-clear home-yard storage; cars={string.Join(",", cut.Select(c => c.DisplayName))}.");
        Couple(job, lead, exposed, "Collected", $"Clearing {cut.Count} obstructing cars; then storing/sorting at the home yard");
        return true;
    }
    private static bool EnoughPower(BaseLocomotive lead, List<Car> cars, Location destination, Location? origin = null)
    {
        if (!NpcTrainOperations.Route(origin ?? lead.LocationF, destination, out var route, out _)) return false;
        return NpcTrainOperations.TryCatalogPower(cars.Where(c => !c.MotivePower()).Sum(c => c.Weight), route, null, out _,
            installedPower: cars.Where(c => c.MotivePower()).ToList(), utilization: 0.9,
            trainLength: TrainController.ApproximateLength(cars.Select(c => c.Descriptor())));
    }
    private static void MoveHome(AeLocalJob job, BaseLocomotive lead, string after, string status)
    {
        var span = OpsController.Shared.AllIndustryComponents.SelectMany(c => c.TrackSpans).FirstOrDefault(s => s.id == job.YardSpan);
        if (span == null || !Berth(job, lead, new[] { span }, lead.EnumerateCoupled().ToList(), out var front, freightOnly: true))
        { if (StageBatch(job, lead, AeLocalSwitchingPlan.PowerFirst(job, lead), "Home track cannot fit the full cut")) return;
          Block(job, lead, "No switch-clear home berth or nearby staging pocket fits even a smaller cut; " + job.PlanDetail); return; }
        if (after == "Yard drop") job.WorkingCut = lead.EnumerateCoupled().Where(c => !job.PowerIds.Contains(c.id)).Select(c => c.id).ToList();
        Move(job, lead, front, after, status);
    }
    private static void PlanNote(AeLocalJob job, string text)
    {
        job.Status = text;
        ModDiagnosticLog.Write("AE LOCAL PLAN", job.Id + ": " + text, "local-plan:" + job.Id + ":" + text, 30);
    }
    private static List<TrackSpan> StagingSpans(AeLocalJob job)
    {
        var home = OpsController.Shared.Areas.FirstOrDefault(a => a.identifier == job.HomeArea);
        var interchange = OpsController.Shared.AllInterchanges.SelectMany(i => i.TrackSpans).ToHashSet();
        var repairs = UnityEngine.Resources.FindObjectsOfTypeAll<RepairTrack>().Where(r => r.gameObject.scene.IsValid())
            .SelectMany(r => r.TrackSpans).ToHashSet();
        return UnityEngine.Resources.FindObjectsOfTypeAll<TrackSpan>().Where(s => s.gameObject.scene.IsValid() && s.IsValid &&
            s.lower.HasValue && s.GetSegments().All(seg => seg.GroupEnabled) && (s.id == job.YardSpan || !repairs.Contains(s)) &&
            (s.id == job.YardSpan || home != null && home.Contains(s.lower.Value.GetPosition()) &&
                (interchange.Contains(s) || (s.id + " " + s.name).IndexOf("yard", StringComparison.OrdinalIgnoreCase) >= 0)))
            .OrderBy(s => s.id == job.YardSpan ? 0 : 1).Take(12).ToList();
    }
    private static bool SafeStorage(AeLocalJob job, List<Car> cars) => ClearOfSwitches(cars) &&
        StagingSpans(job).Any(s => cars.All(c => s.Contains(c.LocationF) && s.Contains(c.LocationR)));
    private static bool StageBatch(AeLocalJob job, BaseLocomotive lead, List<Car> ordered, string reason)
    {
        var freight = ordered.Where(c => !job.PowerIds.Contains(c.id)).ToList();
        if (freight.Count < 2 || !ordered.Take(job.PowerIds.Count).All(c => job.PowerIds.Contains(c.id))) return false;
        var spans = StagingSpans(job);
        foreach (int count in AeLocalPolicy.BinaryCuts((freight.Count + 1) / 2))
        {
            var deferred = freight.Skip(freight.Count - count).ToList();
            foreach (var span in spans)
            {
                if (!Berth(job, lead, new[] { span }, ordered, out var target, freightOnly: true,
                    containedIds: deferred.Select(c => c.id).ToHashSet())) continue;
                if (!EnoughPower(lead, ordered, target)) continue;
                job.StageDeferredIds = deferred.Select(c => c.id).ToList();
                job.StageKeepIds = ordered.Where(c => !job.StageDeferredIds.Contains(c.id)).Select(c => c.id).ToList();
                job.StageSpan = span.id;
                PlanNote(job, $"{reason}: binary split {freight.Count - count}+{count}; staging tail on {span.id}; all tasks remain assigned");
                Move(job, lead, target, "Stage cut", $"Parking {count} deferred cars on {span.id}; keep {freight.Count - count} connected to power");
                return true;
            }
        }
        job.PlanDetail = $"Checked {spans.Count} storage tracks and binary tail cuts down to one car; no reachable clear pocket";
        return false;
    }
    private static double DeferredCost(AeLocalJob job, BaseLocomotive lead, Car car)
    {
        double cost = PickupDistance(lead, car);
        var first = job.Tasks.FirstOrDefault(t => t.CarId == car.id && !t.Done);
        if (first == null || !Deliver(job, first)) return cost + 10000;
        var destination = OpsController.Shared.ResolveOpsCarPosition(first.Destination);
        return cost + (RetryLater(job, first.Destination) ? 1000000 : 0) + destination.Spans.Where(s => s.lower.HasValue).Select(s =>
            AeLocalSwitchingPlan.RouteCost(car.LocationF, s.lower!.Value, out _)).DefaultIfEmpty(10000).Min();
    }
    private static bool RetryLater(AeLocalJob job, string destination) => job.DestinationRetry.TryGetValue(destination, out double retry) && retry > Game.TimeWeather.Now.TotalSeconds;
    private static bool TryClearDestinationRoute(AeLocalJob job, BaseLocomotive lead, OpsCarPosition destination)
    {
        var attached = lead.EnumerateCoupled().Select(c => c.id).ToHashSet();
        foreach (var span in destination.Spans.Take(4))
        {
            if (!span.lower.HasValue || !NpcTrainOperations.Route(lead.LocationF, span.lower.Value, out var route, out _)) continue;
            var obstacle = TrainController.Shared.Cars.Where(c => !c.IsInBardo && !attached.Contains(c.id) && route.Any(s =>
                new[] { c.LocationF, c.LocationR, c.OpsLocation }.Any(p => p.segment == s.Location.segment &&
                    p.WithEnd(s.Location.end).distance <= s.Location.distance + 2 &&
                    p.WithEnd(s.Location.end).distance >= s.Location.distance - s.Distance - 2)))
                .OrderBy(c => Vector3.Distance(c.OpsLocation.GetPosition(), lead.LocationF.GetPosition())).FirstOrDefault();
            if (obstacle != null) return TryClearCut(job, lead, obstacle);
        }
        return false;
    }
    private static bool ConsiderOtherEnd(AeLocalJob job, BaseLocomotive lead, List<Car> ordered, List<Car> freight)
    {
        if (freight.Count < 2 || job.RunaroundAttempted) return false;
        var first = job.Tasks.FirstOrDefault(t => t.CarId == freight[0].id && !t.Done && Deliver(job, t));
        var last = job.Tasks.FirstOrDefault(t => t.CarId == freight[freight.Count - 1].id && !t.Done && Deliver(job, t));
        if (first == null || last == null || first.Destination == last.Destination) return false;
        double Cost(AeLocalTask task) => OpsController.Shared.ResolveOpsCarPosition(task.Destination).Spans.Where(s => s.lower.HasValue)
            .Select(s => AeLocalSwitchingPlan.RouteCost(lead.LocationF, s.lower!.Value, out _)).DefaultIfEmpty(double.PositiveInfinity).Min();
        double near = Cost(first), far = Cost(last);
        double runaround = TrainController.ApproximateLength(ordered.Select(c => c.Descriptor())) * 2 + 150;
        PlanNote(job, $"Accessible end groups: {DestinationLabel(first)} cost {near:N0}, {DestinationLabel(last)} cost {far:N0} + {runaround:N0} runaround; weighing track distance and reversals");
        return far + runaround < near && TryRunaroundForNextGroup(job, lead, ordered, first.Destination);
    }
    private static bool DeferBlockedPrefix(AeLocalJob job, BaseLocomotive lead, List<Car> ordered, List<string> prefix, string destination)
    {
        var remainder = ordered.Where(c => !job.PowerIds.Contains(c.id) && !prefix.Contains(c.id)).ToList();
        if (remainder.Count == 0) return false;
        if (!SafeStorage(job, remainder)) return StageBatch(job, lead, ordered, "Making room to peel an unavailable destination group off the working end");
        var moving = ordered.Where(c => job.PowerIds.Contains(c.id) || prefix.Contains(c.id)).ToList();
        foreach (var span in StagingSpans(job))
        {
            if (!Berth(job, lead, new[] { span }, moving, out var target, freightOnly: true) || !EnoughPower(lead, moving, target)) continue;
            job.YardRemainder = AeLocalPolicy.DeferredCars(job.YardRemainder, remainder.Select(c => c.id), Array.Empty<string>());
            job.StageDeferredIds = prefix.ToList(); job.StageKeepIds = job.PowerIds.ToList(); job.StageSpan = span.id;
            job.DestinationRetry[destination] = Game.TimeWeather.Now.TotalSeconds + 600;
            Split(job, lead, moving.Select(c => c.id).ToHashSet());
            job.MoveIds = moving.Select(c => c.id).ToList();
            PlanNote(job, $"Peeling {prefix.Count} unavailable cars into {span.id} to reach {remainder.Count} other cars; retry destination after other work");
            Move(job, lead, target, "Stage cut", $"Staging unavailable destination group; then work the remaining cut");
            return true;
        }
        return false;
    }
    private static bool TryRunaroundForNextGroup(AeLocalJob job, BaseLocomotive lead, List<Car> ordered, string blockedDestination)
    {
        if (job.RunaroundAttempted) return false;
        var tail = ordered.LastOrDefault(c => !job.PowerIds.Contains(c.id));
        if (tail == null || tail.CoupledTo(Car.LogicalEnd.A) != null && tail.CoupledTo(Car.LogicalEnd.B) != null) return false;
        var task = job.Tasks.FirstOrDefault(t => t.CarId == tail.id && !t.Done && t.Destination != blockedDestination && Deliver(job, t));
        if (task == null) return false;
        var spans = OpsController.Shared.ResolveOpsCarPosition(task.Destination).Spans;
        double cost = spans.Where(s => s.lower.HasValue).Select(s => AeLocalSwitchingPlan.RouteCost(tail.LocationF, s.lower!.Value, out _))
            .DefaultIfEmpty(double.PositiveInfinity).Min();
        if (double.IsInfinity(cost)) return false;
        job.RunaroundAttempted = true;
        bool started = false;
        NpcTrainOperations.Trusted(() => started = Patches.AutoEngineerRunaround.TryStart(lead, Graph.Shared.ResolveLocationString(job.YardPoint), ""));
        if (!started) { PlanNote(job, "Alternate destination group found, but no clear runaround route available"); return false; }
        var end = tail.CoupledTo(Car.LogicalEnd.A) == null ? Car.LogicalEnd.A : Car.LogicalEnd.B;
        job.CoupleTo = tail.id; job.Target = Graph.Shared.LocationToString(tail.LocationFor(end));
        job.Phase = "Runaround";
        PlanNote(job, $"Running around to work {DestinationLabel(task)} from the other end; blocked group stays on the job");
        return true;
    }
    private static bool ClearOfSwitches(List<Car> cars)
    {
        var nodes = cars.SelectMany(c => new[] { c.LocationF.segment, c.LocationR.segment, c.OpsLocation.segment })
            .SelectMany(s => new[] { s.a, s.b }).Distinct()
            .Select(n => (node: n, clearance: NpcTrackClearance.Fouling(n))).Where(n => n.clearance > 0).ToList();
        return cars.All(c => nodes.All(n => new[] { c.LocationF, c.LocationR, c.OpsLocation }
            .All(p => Vector3.Distance(p.GetPosition(), n.node.transform.GamePosition()) >= n.clearance + 3)));
    }
    private static bool Berth(AeLocalJob job, BaseLocomotive lead, IEnumerable<TrackSpan> spans, List<Car> cars, out Location front, bool freightOnly = false,
        HashSet<string>? containedIds = null)
    {
        front = Location.Invalid;
        double best = double.PositiveInfinity;
        string bestDetail = "No pull-in or shove-in footprint";
        bool bestShove = false;
        var own = cars.Select(c => c.id).ToHashSet();
        var contained = containedIds ?? cars.Where(c => !freightOnly || !job.PowerIds.Contains(c.id)).Select(c => c.id).ToHashSet();
        if (contained.Count == 0) contained = own;
        foreach (bool shove in new[] { false, true })
        {
            var ordered = shove ? cars.AsEnumerable().Reverse().ToList() : cars;
            int first = ordered.FindIndex(c => contained.Contains(c.id));
            int last = ordered.FindLastIndex(c => contained.Contains(c.id));
            if (first < 0 || ordered.Skip(first).Take(last - first + 1).Any(c => !contained.Contains(c.id))) continue;
            float length = TrainController.ApproximateLength(ordered.Select(c => c.Descriptor()));
            float offsetBefore = first == 0 ? 0 : TrainController.ApproximateLength(ordered.Take(first).Select(c => c.Descriptor()));
            float containedLength = TrainController.ApproximateLength(ordered.Skip(first).Take(last - first + 1).Select(c => c.Descriptor()));
            foreach (var span in spans)
            foreach (var boundary in new[] { span.lower, span.upper })
            {
                if (!boundary.HasValue || !boundary.Value.IsValid || span.Length < containedLength + 2) continue;
                try
                {
                    var inward = boundary.Value;
                    var test = Graph.Shared.LocationByMoving(inward, 1, false);
                    if (!test.IsValid || !span.Contains(test)) inward = inward.Flipped();
                    var nodes = span.GetSegments().SelectMany(s => new[] { s.a, s.b }).Distinct()
                        .Select(n => (node: n, clearance: NpcTrackClearance.Fouling(n))).Where(n => n.clearance > 0).ToList();
                    float step = Math.Max(2, (span.Length - containedLength) / 128);
                    for (float offset = containedLength + 1 + offsetBefore; offset <= span.Length - 1 + offsetBefore; offset += step)
                    {
                        var point = Graph.Shared.LocationByMoving(inward, offset, false);
                        if (job.RejectedSpots.TryGetValue(Graph.Shared.LocationToString(point), out double retry) && retry > Game.TimeWeather.Now.TotalSeconds) continue;
                        bool fits = true;
                        var found = new HashSet<Car>();
                        for (float d = 0; d <= length + 1; d += 1)
                        {
                            var p = Graph.Shared.LocationByMoving(point, -d, false);
                            if (!p.IsValid || !p.segment.GroupEnabled) { fits = false; break; }
                            TrainController.Shared.CheckForCarsAtPoint(p.GetPosition(), 1, found, p);
                            if (found.Any(c => !own.Contains(c.id))) { fits = false; break; }
                            if (d < offsetBefore || d > offsetBefore + containedLength) continue;
                            if (!span.Contains(p) || nodes.Any(n => Vector3.Distance(p.GetPosition(), n.node.transform.GamePosition()) < n.clearance + 3))
                            { fits = false; break; }
                        }
                        if (!fits) continue;
                        double score = AeLocalSwitchingPlan.RouteCost(lead.LocationF, point, out var detail) + (shove ? 15 : 0);
                        if (double.IsInfinity(score)) continue;
                        var route = new List<Track.Search.RouteSearch.Step>();
                        float clearance = nodes.Select(n => n.clearance).DefaultIfEmpty(0).Max();
                        if (!Track.Search.RouteSearch.FindRoute(Graph.Shared, lead.LocationF, point, Track.Search.HeuristicCosts.AutoEngineer,
                            route, out _, checkForCars: true, trainLength: length + clearance + 8, trainMomentum: 0, maxIterations: 5000,
                            checkForCarsIgnored: cars.ToHashSet(), checkForCarsImpasse: null, limitSwitchIds: null, enableLogging: false)) continue;
                        if (score >= best) break;
                        best = score; front = point; bestShove = shove;
                        bestDetail = $"{(shove ? "shove-in" : "pull-in")} spot on {span.id}; {detail}";
                        // One feasible pocket per entrance and orientation.
                        break;
                    }
                }
                catch (EndOfTrack) { }
                catch (SwitchAgainstMovement) { }
            }
        }
        job.PlanDetail = bestDetail; job.SpotShove = bestShove;
        return front.IsValid;
    }
    private static bool Couple(AeLocalJob job, BaseLocomotive lead, Car target, string after, string status)
    {
        foreach (var end in new[] { Car.LogicalEnd.A, Car.LogicalEnd.B }.OrderBy(e =>
            NpcTrainOperations.Route(lead.LocationF, target.LocationFor(e), out _, out float distance) ? distance : float.MaxValue))
        {
            if (target.CoupledTo(end) != null) continue;
            var point = target.LocationFor(end);
            if (!NpcTrainOperations.Route(lead.LocationF, point, out _, out _)) continue;
            NpcTrainOperations.Trusted(() =>
            {
                target.ApplyEndGearChange(end, Car.EndGearStateKey.CutLever, 0f);
                foreach (var car in lead.EnumerateCoupled())
                foreach (var exposedEnd in new[] { Car.LogicalEnd.A, Car.LogicalEnd.B })
                    if (car.CoupledTo(exposedEnd) == null) car.ApplyEndGearChange(exposedEnd, Car.EndGearStateKey.CutLever, 0f);
            });
            Move(job, lead, point, after, status, target.id); return true;
        }
        Block(job, lead, "No reachable exposed pickup coupler; a safe runaround is required"); return false;
    }
    private static void Move(AeLocalJob job, BaseLocomotive lead, Location target, string after, string status, string couple = "")
    {
        if (!target.IsValid || !NpcTrainOperations.Route(lead.LocationF, target, out _, out _)) { Block(job, lead, "No connected route to the next task"); return; }
        job.Target = Graph.Shared.LocationToString(target); job.CoupleTo = couple; job.AfterMove = after; job.Phase = "Moving"; job.Status = status;
        job.MoveStarted = Game.TimeWeather.Now.TotalSeconds;
        NpcServiceStore.Save(); SendOrder(job, lead);
        ModDiagnosticLog.Write("AE LOCAL", $"{job.Id}: {status}; target={job.Target}; couple={couple}.");
    }
    private static void SendOrder(AeLocalJob job, BaseLocomotive lead)
    {
        if (job.MoveIds.Count > 0 && !lead.EnumerateCoupled().Select(c => c.id).ToHashSet().SetEquals(job.MoveIds))
        { NpcTrainOperations.Hold(lead); job.Phase = "Separating cut"; job.Status = "Waiting for secured yard-cut couplers to separate"; return; }
        job.MoveIds.Clear();
        var target = Graph.Shared.ResolveLocationString(job.Target);
        bool forward = NpcTrainOperations.FacingRoute(lead.LocationF, target).end == lead.LocationF.end;
        if (job.CoupleTo.Length == 0 && (job.AfterMove == "Deliver" || job.AfterMove == "Stage cut" || job.AfterMove == "Yard drop" || job.AfterMove == "At yard"))
        {
            var ordered = AeLocalSwitchingPlan.PowerFirst(job, lead);
            var leading = job.SpotShove ? ordered.Last() : ordered.First();
            forward = lead.EnumerateCoupled(lead.EndToLogical(Car.End.F)).First().id == leading.id;
        }
        AirAndBrakes(lead);
        NpcTrainOperations.Trusted(() =>
        {
            foreach (var engine in lead.EnumerateCoupled().OfType<BaseLocomotive>())
            {
                bool trailing = engine != lead;
                StateManager.ApplyLocal(new PropertyChange(engine.id, PropertyChange.Control.Mu, trailing));
                StateManager.ApplyLocal(new PropertyChange(engine.id, PropertyChange.Control.CutOut, trailing));
                if (trailing) new AutoEngineerOrdersHelper(engine, new AutoEngineerPersistence(engine.KeyValueObject)).SetOrdersValue(mode: AutoEngineerMode.Off, maxSpeedMph: 0);
            }
            new AutoEngineerOrdersHelper(lead, new AutoEngineerPersistence(lead.KeyValueObject))
                .SetOrdersValue(mode: AutoEngineerMode.Waypoint, forward: forward, maxSpeedMph: AeLocalPolicy.MoveSpeed(job.MaxSpeedMph, job.CoupleTo.Length > 0, true),
                    maybeWaypoint: (target, job.CoupleTo.Length > 0 ? job.CoupleTo : null!));
        });
    }
    private static bool MoveReached(AeLocalJob job, BaseLocomotive lead)
    {
        if (job.AfterMove == "Deliver" || job.AfterMove == "Stage cut" || job.AfterMove == "Yard drop" || job.AfterMove == "At yard")
            return SpotSatisfied(job, lead);
        if (Graph.Shared.CheckSameRoute(lead.AutoEngineerPlanner.StartLocation(), Graph.Shared.ResolveLocationString(job.Target), 10)) return true;
        // Native AE can satisfy a waypoint under a car rather than precisely
        // at the lead coupler. Delivery/yard completion uses actual car bounds.
        if (job.AfterMove == "Deliver" && job.WorkingCut.Count > 0)
        {
            var task = job.Tasks.First(t => job.WorkingCut.Contains(t.CarId));
            var spans = OpsController.Shared.ResolveOpsCarPosition(task.Destination).Spans;
            return job.WorkingCut.Select(CarFor).All(c => c != null && spans.Any(s => s.Contains(c.LocationF) && s.Contains(c.LocationR)));
        }
        if (job.AfterMove == "Yard drop" || job.AfterMove == "At yard")
        {
            var span = OpsController.Shared.AllIndustryComponents.SelectMany(c => c.TrackSpans).FirstOrDefault(s => s.id == job.YardSpan);
            var freight = lead.EnumerateCoupled().Where(c => !job.PowerIds.Contains(c.id)).ToList();
            return span != null && freight.Count > 0 && freight.All(c => span.Contains(c.LocationF) && span.Contains(c.LocationR));
        }
        return false;
    }
    internal static bool SpotSatisfied(AeLocalJob job, BaseLocomotive lead)
    {
        List<Car> cars;
        IEnumerable<TrackSpan> spans;
        if (job.AfterMove == "Stage cut")
        {
            cars = job.StageDeferredIds.Select(CarFor).Where(c => c != null).Cast<Car>().ToList();
            spans = UnityEngine.Resources.FindObjectsOfTypeAll<TrackSpan>().Where(s => s.id == job.StageSpan && s.gameObject.scene.IsValid());
            if (cars.Count != job.StageDeferredIds.Count) return false;
        }
        else if (job.AfterMove == "Deliver" && job.WorkingCut.Count > 0)
        {
            cars = job.WorkingCut.Select(CarFor).Where(c => c != null).Cast<Car>().ToList();
            var task = job.Tasks.First(t => job.WorkingCut.Contains(t.CarId));
            spans = OpsController.Shared.ResolveOpsCarPosition(task.Destination).Spans;
            if (cars.Count != job.WorkingCut.Count) return false;
        }
        else
        {
            cars = lead.EnumerateCoupled().Where(c => !job.PowerIds.Contains(c.id)).ToList();
            spans = UnityEngine.Resources.FindObjectsOfTypeAll<TrackSpan>().Where(s => s.id == job.YardSpan && s.gameObject.scene.IsValid());
        }
        return cars.Count > 0 && ClearOfSwitches(cars) && spans.Any(s => cars.All(c => s.Contains(c.LocationF) && s.Contains(c.LocationR)));
    }
    private static void AirAndBrakes(BaseLocomotive lead) => NpcTrainOperations.Trusted(() =>
    {
        foreach (var car in lead.EnumerateCoupled()) { Patches.CarInspector_PopulateCarPanel_Patch.CarEndAirUpdate(car); car.SetHandbrake(false); }
    });
    private static void Open(Car car, Car.LogicalEnd end)
    {
        var other = car.CoupledTo(end);
        if (other == null) return;
        car.ApplyEndGearChange(end, Car.EndGearStateKey.Anglecock, 0f);
        foreach (var otherEnd in new[] { Car.LogicalEnd.A, Car.LogicalEnd.B })
            if (other.CoupledTo(otherEnd) == car) other.ApplyEndGearChange(otherEnd, Car.EndGearStateKey.Anglecock, 0f);
        car.HandleOpenCoupler(end);
    }
    private static void Split(AeLocalJob job, BaseLocomotive lead, HashSet<string> keep)
    {
        NpcTrainOperations.Trusted(() =>
        {
            var deferred = lead.EnumerateCoupled().Where(c => !keep.Contains(c.id)).ToList();
            if (deferred.Count > 0) NpcSetoutPlacement.SecureFreight(deferred);
            foreach (var car in lead.EnumerateCoupled().Where(c => keep.Contains(c.id)).ToList())
            foreach (var end in new[] { Car.LogicalEnd.A, Car.LogicalEnd.B })
                if (car.CoupledTo(end) is Car other && !keep.Contains(other.id)) Open(car, end);
        });
    }
    private static void Block(AeLocalJob job, BaseLocomotive lead, string reason)
    {
        NpcTrainOperations.Hold(lead); job.Paused = true; job.Status = reason;
        ModDiagnosticLog.Write("AE LOCAL HOLD", job.Id + ": " + reason);
    }
}
