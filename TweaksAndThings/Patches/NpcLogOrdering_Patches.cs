using HarmonyLib;
using Model.Ops;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(IndustryContext), nameof(IndustryContext.PayWaybill))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcLogOrdering_Delivery_Patch
{
    private static void Postfix(IndustryContext __instance, IOpsCar car, Waybill waybill) =>
        NpcLogOrdering.Delivered(__instance, car, waybill);
}
[HarmonyPatch(typeof(OpsController), nameof(OpsController.DayDidChange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcLogOrdering_Settlement_Patch
{
    private static void Postfix() => NpcLogOrdering.Settle();
}
