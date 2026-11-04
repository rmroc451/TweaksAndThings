using HarmonyLib;
using Model;
using Model.Ops;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(IndustryContext), nameof(IndustryContext.TryCreateCarDescriptorForOrder))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcInboundDescriptor_Patch
{
    private static bool Prefix(Order order, ref CarDescriptor descriptor, ref float amount) =>
        NpcInboundPlan.Active == null || !NpcInboundPlan.Active.TryTake(order, out descriptor, out amount);
}
