using HarmonyLib;
using Game.State;
using Model.Ops;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(OpsController), nameof(OpsController.DayDidChange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcIndustryPlanning_DayDidChange_Patch
{
    private static void Prefix(OpsController __instance)
    {
        if (StateManager.IsHost) NpcIndustryPlanning.Begin(__instance);
    }
    private static void Postfix(OpsController __instance)
    {
        // All industries have rolled their contracts and completed daily payables.
        // The normal bounded planner update publishes the timetable afterwards.
        if (StateManager.IsHost) NpcIndustryPlanning.Complete(__instance);
    }
}

[HarmonyPatch(typeof(OpsController), nameof(OpsController.PostRestoreProperties))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcIndustryPlanning_PostRestoreProperties_Patch
{
    private static void Prefix(OpsController __instance)
    {
        if (StateManager.IsHost) NpcIndustryPlanning.Begin(__instance);
    }
    private static void Postfix(OpsController __instance)
    {
        // Restored saves can resume today's planning; pending initial contracts
        // remain blocked by the readiness check until the native day update.
        if (StateManager.IsHost) NpcIndustryPlanning.Complete(__instance);
    }
}
