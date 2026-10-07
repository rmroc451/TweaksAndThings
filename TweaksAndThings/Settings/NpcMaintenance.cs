using Game.Messages;
using Game.State;
using Model;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcMaintenance
{
    private static float nextCheck;
    internal static void ClearHotbox(Car car)
    {
        if (!StateManager.IsHost || !car.HasHotbox) return;
        NpcTrainOperations.Trusted(() => StateManager.ApplyLocal(new PropertyChange(car.id,
            PropertyChange.Control.Hotbox, 0f)));
        ModDiagnosticLog.Write("NPC MAINTENANCE", $"Cleared existing hotbox on NPC car {car.id}; oil level preserved.");
    }
    internal static void Update()
    {
        if (!StateManager.IsHost || TrainController.Shared == null || Time.realtimeSinceStartup < nextCheck) return;
        nextCheck = Time.realtimeSinceStartup + 5;
        // Recover NPC cars in older saves as well as cars accepted for pickup.
        foreach (var car in TrainController.Shared.Cars)
            if (ThroughTrafficGuard.IsGenerated(car) && car.HasHotbox) ClearHotbox(car);
    }
}
