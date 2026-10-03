using HarmonyLib;
using Model;
using Model.Ops;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(IndustryContext), "CreateCarDescriptorForOrder")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcInboundDescriptor_Patch
{
    private static bool Prefix(Order order, ref CarDescriptor __result) =>
        NpcInboundPlan.Active == null || !NpcInboundPlan.Active.TryTake(order, out __result);
}
