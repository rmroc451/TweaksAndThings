using HarmonyLib;
using Model;
using Model.AI;
using RollingStock;
using System.Linq;
using Track;
using UI;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(Car))]
[HarmonyPatch(nameof(Car.HandleCouplerClick))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_Car_CouplerClick_Patch
{
    private static bool Prefix(Car __instance) => !ThroughTrafficGuard.BlockInteraction(__instance);
}

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatch(nameof(TrainController.IntegrationSetDidCouple))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_TrainController_Couple_Patch
{
    private static bool Prefix(object[] __args) => !ThroughTrafficGuard.ShouldBlockConsistMutation(__args);
}

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatch(nameof(TrainController.IntegrationSetRequestsBreakConnections))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_TrainController_Decouple_Patch
{
    private static bool Prefix(object[] __args) => !ThroughTrafficGuard.ShouldBlockConsistMutation(__args);
}

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatch(nameof(TrainController.IntegrationSetRequestsReconnect))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_TrainController_Reconnect_Patch
{
    private static bool Prefix(object[] __args) => !ThroughTrafficGuard.ShouldBlockConsistMutation(__args);
}

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatch(nameof(TrainController.MoveCarCoupleTo))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_TrainController_MoveAndCouple_Patch
{
    private static bool Prefix(object[] __args) => !ThroughTrafficGuard.ShouldBlockConsistMutation(__args);
}

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatch(nameof(TrainController.HandleManualMoveCar))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_TrainController_ManualMove_Patch
{
    private static bool Prefix(string carId) => !ThroughTrafficGuard.BlockCarIdInteraction(carId);
}

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatch(nameof(TrainController.HandleRerail))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_TrainController_Rerail_Patch
{
    private static bool Prefix(string[] carIds)
    {
        if (ThroughTrafficGuard.IsSandbox) return true;
        foreach (string carId in carIds)
            if (ThroughTrafficGuard.BlockCarIdInteraction(carId)) return false;
        return true;
    }
}

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatch(nameof(TrainController.HandleSetIdent))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_TrainController_Ident_Patch
{
    private static bool Prefix(string carId) => !ThroughTrafficGuard.BlockCarIdInteraction(carId);
}

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatch(nameof(TrainController.HandleSetCarTrainCrew))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_TrainController_Crew_Patch
{
    private static bool Prefix(string carId) => !ThroughTrafficGuard.BlockCarIdInteraction(carId);
}

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatch(nameof(TrainController.HandleSetBardo))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_TrainController_Bardo_Patch
{
    private static bool Prefix(string carId) => !ThroughTrafficGuard.BlockCarIdInteraction(carId);
}

[HarmonyPatch(typeof(AutoEngineerPlanner))]
[HarmonyPatch(nameof(AutoEngineerPlanner.ApplyMovement))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_AutoEngineer_ApplyMovement_Patch
{
    private static void Prefix() => ThroughTrafficGuard.BeginTrustedMutation();
    private static System.Exception? Finalizer(System.Exception? __exception)
    {
        ThroughTrafficGuard.EndTrustedMutation();
        return __exception;
    }
}

[HarmonyPatch(typeof(AutoEngineerPlanner))]
[HarmonyPatch(nameof(AutoEngineerPlanner.UpdateTargets))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ThroughTraffic_AutoEngineer_UpdateTargets_Patch
{
    private static void Prefix() => ThroughTrafficGuard.BeginTrustedMutation();
    private static System.Exception? Finalizer(System.Exception? __exception)
    {
        ThroughTrafficGuard.EndTrustedMutation();
        return __exception;
    }
}
