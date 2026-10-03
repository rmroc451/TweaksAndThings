using System;
using System.Collections.Generic;
using System.Linq;
using Game.State;
using Model;
using Model.Ops;
using Track;
using UI;
using UI.Builder;
using UI.Common;
using UI.EngineControls;

namespace RMROC451.TweaksAndThings;

internal static class NpcRecoveryControls
{
    internal static List<DropdownMenu.RowData> Rows => new() { new("Jump to", null), new("Reestablish programming", null) };
    private static List<DropdownMenu.RowData> RowsFor(Car car) => NpcPoolPower.IsPooled(car) ? new() { new("Jump to", null) } : Rows;
    internal static void Action(Car car, int action)
    {
        if (action == 0) { CameraSelector.shared.ZoomToCar(car); return; }
        if (!StateManager.IsHost) { Toast.Present("Ask the host to reestablish this NPC service."); return; }
        if (!NpcServiceStore.Load()) return;
        var service = NpcServiceStore.State.Services.FirstOrDefault(s => s.PowerIds.Contains(car.id) || s.Inbound.Contains(car.id) || s.Outbound.Any(p => p.CarId == car.id));
        if (service == null || !TrainController.Shared.TryGetCarForId(service.LeadId, out var candidate) || !(candidate is BaseLocomotive lead))
        { Toast.Present("No saved service locomotive is available for this train."); return; }
        try
        {
            var ids = service.PowerIds.Concat(service.Inbound.Skip(service.SetoutDone)).Concat(service.Outbound.Take(service.PickupDone).Where(p => !p.Missed).Select(p => p.CarId)).Distinct().ToList();
            var owned = ids.Select(id => TrainController.Shared.CarForId(id)).Where(c => c != null && ThroughTrafficGuard.IsGenerated(c)).ToList();
            bool needsRerail = owned.Any(c => c.IsDerailed);
            if (lead.EnumerateCoupled().Count() != owned.Count || needsRerail)
            {
                var front = lead.LocationA.Clamped();
                if (!NpcDeparturePlacement.Fits(front, TrainController.ApproximateLength(owned.Select(c => c.Descriptor())), new HashSet<string>(ids)))
                { Toast.Present("Clear enough track around this NPC train before reestablishing it."); return; }
                NpcTrainOperations.Trusted(() =>
                {
                    foreach (var vehicle in owned.Where(c => c.IsDerailed))
                        StateManager.ApplyLocal(new Game.Messages.PropertyChange(vehicle.id, Game.Messages.PropertyChange.Control.Derailment, 0f));
                });
                NpcDeparturePlacement.MoveExisting(front, owned);
            }
            var destination = Graph.Shared.ResolveLocationString(service.Phase == "Departing" ? service.Spawn : service.Target);
            if ((service.Phase == "Approaching" || service.Phase == "Departing") && !NpcDeparturePlacement.FaceDeparture(service, destination))
            { Toast.Present("Clear the departure end before reestablishing this NPC train."); return; }
            NpcTrainOperations.Order(lead, destination, service.Phase == "Approaching" || service.Phase == "Departing");
            NpcTrafficRouting.Recalculate(lead);
            service.Suspended = false;
            NpcServiceStore.Save();
            ModDiagnosticLog.Write("RECOVERY", "Reestablished " + service.Id + "; phase=" + service.Phase + "; target=" + service.Target);
            Toast.Present("NPC programming reestablished.");
        }
        catch (Exception ex) { TweaksAndThingsPlugin.LogException("Reestablish NPC service " + service.Id, ex); Toast.Present("Unable to reestablish service; see the debug log."); }
    }

    internal static bool Configure(LocomotiveControlsUIAdapter adapter)
    {
        var loco = TrainController.Shared?.SelectedLocomotive;
        bool generated = ThroughTrafficGuard.IsGenerated(loco);
        adapter.modeDropdown.gameObject.SetActive(!generated);
        if (!generated) return false;
        foreach (var set in adapter._controlSets) set.gameObject.SetActive(false);
        adapter.optionsDropdown.Configure(RowsFor(loco!), action => Action(loco!, action));
        return true;
    }

    internal static void Inspector(Car car, UIPanelBuilder builder)
    {
        builder.AddLabel(car.DisplayName + " — AI traffic");
        builder.AddOptionsDropdown(RowsFor(car), action => Action(car, action));
        if (NpcPoolPower.IsPooled(car)) builder.AddLabel("Parked pool power: outer couplings, air hoses and anglecocks are usable. Internal power connections and locomotive controls are locked.");
        builder.AddField("Speed", () => $"{car.VelocityMphAbs:N1} mph", UIPanelBuilder.Frequency.Periodic);
        if (car.Waybill.HasValue) builder.AddField("Destination", car.Waybill.Value.Destination.DisplayName);
        if (NpcServiceStore.Load() && NpcServiceStore.State.HeldDeliveries.Any(h => h.CarId == car.id))
            builder.AddField("Held delivery", () => NpcHeldDeliveryTracking.CarBonusSummary(car.id), UIPanelBuilder.Frequency.Periodic);
        builder.AddLabel("NPC consists are controlled by the dispatcher in all game modes.");
    }
}
