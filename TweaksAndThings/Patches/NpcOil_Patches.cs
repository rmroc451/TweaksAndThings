using HarmonyLib;
using Model;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(Car), "get_EnableOiling")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcOil_Enabled_Patch
{
    private static bool Prefix(Car __instance, ref bool __result)
    {
        if (!ThroughTrafficGuard.IsGenerated(__instance)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Car), "OilUseForMovement")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcOil_Consumption_Patch
{
    private static bool Prefix(Car __instance, ref float __result)
    {
        if (!ThroughTrafficGuard.IsGenerated(__instance)) return true;
        __result = 0;
        return false;
    }
}

[HarmonyPatch(typeof(Car), "CheckForHotbox")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcOil_Hotbox_Patch
{
    private static bool Prefix(Car __instance) => !ThroughTrafficGuard.IsGenerated(__instance);
}
