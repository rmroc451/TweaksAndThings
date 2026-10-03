using HarmonyLib;
using Model.AI;
using Track;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(AutoEngineerPlanner), "UpdateTargets")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTrafficRouting_Recalculate_Patch
{
    private static void Prefix(AutoEngineerPlanner __instance) => NpcTrafficRouting.Prepare(__instance);
}

[HarmonyPatch(typeof(AutoEngineer), nameof(AutoEngineer.SetTargets))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTrafficRouting_Corridor_Patch
{
    private static void Prefix(AutoEngineer __instance, AutoEngineer.Targets targets) => NpcTrafficRouting.Protect(__instance, targets);
}

[HarmonyPatch(typeof(AutoEngineerPlanner), "TrySetSwitch")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTrafficRouting_SidingSwitch_Patch
{
    private static bool Prefix(AutoEngineerPlanner __instance, TrackNode node, ref AutoEngineerPlanner.SetSwitchResult __result)
    {
        if (NpcTrafficRouting.MayThrowSwitch(__instance, node)) return true;
        __result = AutoEngineerPlanner.SetSwitchResult.Occupied;
        return false;
    }
}

[HarmonyPatch(typeof(AutoEngineerPlanner), "SetSwitchThrown")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTrafficRouting_SidingRestore_Patch
{
    private static bool Prefix(AutoEngineerPlanner __instance, TrackNode node) => NpcTrafficRouting.MayThrowSwitch(__instance, node);
}
