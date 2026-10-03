using System.Collections.Generic;
using System.Linq;
using Game;
using HarmonyLib;
using Model;
using Model.Ops;
using Track;
using Network;

namespace RMROC451.TweaksAndThings.Patches;

// Native messages describe instantaneous yard delivery. NPC loading is remote and
// can retry; its progress and placement blockers are reported in the traffic UI/log.
[HarmonyPatch(typeof(Multiplayer), nameof(Multiplayer.Broadcast))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulatedInterchange_LoadingMessage_Patch
{
    private static bool Prefix(string message)
    {
        var service = SimulatedInterchangeService.Loading ?? SimulatedInterchangeService.Collecting;
        if (service == null || !message.Contains(" received ")) return true;
        NpcTrafficDiagnostics.Detail("backend-message:" + service.Id, "Native NPC backend message suppressed: " + message);
        return false;
    }
}

[HarmonyPatch(typeof(Interchange), nameof(Interchange.ServeInterchange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulatedInterchange_Serve_Patch
{
    private static bool Prefix(Interchange __instance) =>
        SimulatedInterchangeService.Loading != null || SimulatedInterchangeService.Collecting != null || !SimulatedInterchangeService.Owns(__instance);
}

[HarmonyPatch(typeof(Interchange), nameof(Interchange.PrepareToService))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulatedInterchange_Prepare_Patch
{
    private static bool Prefix(Interchange __instance) => SimulatedInterchangeService.Loading != null || !SimulatedInterchangeService.HasInboundService(__instance);
}

[HarmonyPatch(typeof(Interchange), nameof(Interchange.GetNextServiceTime))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulatedInterchange_NextService_Patch
{
    private static void Postfix(Interchange __instance, GameDateTime now, ref GameDateTime __result)
    {
        if (SimulatedInterchangeService.Loading == null && SimulatedInterchangeService.Collecting == null &&
            SimulatedInterchangeService.Owns(__instance) && __result <= now) __result = now.AddingDays(1);
    }
}

[HarmonyPatch(typeof(InterchangedIndustryLoader), nameof(InterchangedIndustryLoader.ServeInterchange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulatedInterchange_Loaders_Patch
{
    private static bool Prefix(ref IIndustryContext ctx)
    {
        if (SimulatedInterchangeService.Loading != null) return false;
        if (SimulatedInterchangeService.Collecting != null) ctx = new NpcInterchangeContext(ctx, false);
        return true;
    }
}

[HarmonyPatch(typeof(TrainPlacementHelper), nameof(TrainPlacementHelper.PlaceTrain))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulatedInterchange_Inbound_Patch
{
    private static bool Prefix(List<CarDescriptor> descriptors, List<string> carIds, ref bool __result) =>
        SimulatedInterchangeService.LoadInbound(descriptors, carIds, ref __result);
}

[HarmonyPatch(typeof(TrainController), nameof(TrainController.RemoveCarSmart))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulatedInterchange_Sell_Patch
{
    private static bool Prefix(string carId) => !SimulatedInterchangeService.QueuePickup(carId);
}

[HarmonyPatch(typeof(TrainController), nameof(TrainController.MoveToBardo))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulatedInterchange_Bardo_Patch
{
    private static bool Prefix(string carId, string senderId) => !SimulatedInterchangeService.QueuePickup(carId, senderId);
}

[HarmonyPatch(typeof(IndustryContext), nameof(IndustryContext.CarsAtPosition))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulatedInterchange_CollectCars_Patch
{
    private static void Postfix(ref IEnumerable<IOpsCar> __result)
    {
        if (SimulatedInterchangeService.Collecting != null)
            __result = __result.Where(c => !ThroughTrafficGuard.IsGeneratedCarId(c.Id));
    }
}
