using System;
using System.Linq;
using Game.Messages;
using Game.State;
using Model;
using Model.AI;
using Model.Ops;
using Track;
using UI.EngineControls;

namespace RMROC451.TweaksAndThings;

// Runs once per restored service-state instance, on the authoritative host.
internal static class NpcLoadRecovery
{
    private static int recoveredGeneration = -1;
    private static int readyFrames;

    internal static void Update()
    {
        if (!StateManager.IsHost || RestoreNotifier.Shared == null || !RestoreNotifier.Shared.HasRestored ||
            TrainController.Shared == null || OpsController.Shared == null || Graph.Shared == null)
        { readyFrames = 0; return; }
        if (!NpcServiceStore.Load() || recoveredGeneration == NpcServiceStore.Generation) return;
        // Allow restored cars and components to finish their first frame.
        if (++readyFrames < 2) return;
        readyFrames = 0;
        recoveredGeneration = NpcServiceStore.Generation;
        int restored = 0;
        var assigned = NpcServiceStore.State.Services.SelectMany(s => s.PowerIds).ToHashSet();
        foreach (var service in NpcServiceStore.State.Services.ToList())
        {
            try
            {
                if (!TrainController.Shared.TryGetCarForId(service.LeadId, out var car) || car is not BaseLocomotive lead)
                { ModDiagnosticLog.Write("LOAD RECOVERY", service.Id + ": saved lead missing; service retained for recovery."); continue; }
                if (service.InterchangeId.Length > 0 && service.Phase == "Departing" && NpcServicePolicy.ShouldPoolPower(
                    service.Inbound.Count - service.SetoutDone, service.Outbound.Count - service.PickupDone,
                    service.Outbound.Count(p => p.Queued && !p.Missed)))
                {
                    service.Phase = "Awaiting repair parking";
                    service.WaitingReason = "Waiting for a clear repair track in the interchange game area";
                    service.NextTransfer = Game.TimeWeather.Now.TotalSeconds;
                }
                NpcTrainOperations.ConfigurePower(lead);
                // Trailing engines follow the lead through MU, not independent AE orders.
                foreach (var engine in lead.EnumerateCoupled().OfType<BaseLocomotive>().Where(e => e != lead && ThroughTrafficGuard.IsGenerated(e)))
                    NpcTrainOperations.Trusted(() => new AutoEngineerOrdersHelper(engine, new AutoEngineerPersistence(engine.KeyValueObject))
                        .SetOrdersValue(mode: AutoEngineerMode.Off, maxSpeedMph: 0));
                bool travel = service.Phase == "Approaching" || service.Phase == "Departing" || service.Phase == "Parking power";
                NpcTrainOperations.Hold(lead);
                // Existing state machines restore the saved destination and check
                // forward placement and clearances before permitting travel.
                service.Suspended = travel;
                if (travel) service.ArrivalHolding = true;
                else if (service.Phase == "Awaiting repair parking") service.NextTransfer = Game.TimeWeather.Now.TotalSeconds;
                restored++;
                ModDiagnosticLog.Write("LOAD RECOVERY", $"{service.Id}: restored power configuration; phase={service.Phase}; travel resumes through safety checks; transfer progress and deadlines preserved.");
            }
            catch (Exception ex) { TweaksAndThingsPlugin.LogException("NPC load recovery " + service.Id, ex); }
        }
        foreach (var pool in NpcServiceStore.State.PoolPower)
        foreach (var id in pool.PowerIds)
        {
            assigned.Add(id);
            if (!TrainController.Shared.TryGetCarForId(id, out var car)) continue;
            NpcTrainOperations.Trusted(() =>
            {
                car.SetHandbrake(true);
                if (car is not BaseLocomotive engine) return;
                new AutoEngineerOrdersHelper(engine, new AutoEngineerPersistence(engine.KeyValueObject)).SetOrdersValue(mode: AutoEngineerMode.Off, maxSpeedMph: 0);
                StateManager.ApplyLocal(new PropertyChange(engine.id, PropertyChange.Control.Throttle, 0f));
                StateManager.ApplyLocal(new PropertyChange(engine.id, PropertyChange.Control.Mu, false));
                StateManager.ApplyLocal(new PropertyChange(engine.id, PropertyChange.Control.CutOut, true));
            });
        }
        foreach (var engine in TrainController.Shared.Cars.OfType<BaseLocomotive>().Where(c => ThroughTrafficGuard.IsGenerated(c) && !assigned.Contains(c.id)))
        {
            NpcTrainOperations.Hold(engine);
            ModDiagnosticLog.Write("LOAD RECOVERY", engine.DisplayName + ": no saved service or pool assignment; held safely for manual recovery.");
        }
        NpcServiceStore.Save();
        ModDiagnosticLog.Write("LOAD RECOVERY", $"Completed save-load reconciliation: {restored} active services; {NpcServiceStore.State.PoolPower.Count} parked pool units.");
    }
}
