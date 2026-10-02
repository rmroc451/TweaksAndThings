using Game.State;
using HarmonyLib;

namespace RMROC451.TweaksAndThings.Patches;

/// <summary>Final guard for local and replicated key updates to generated NPC cars.</summary>
[HarmonyPatch(typeof(StateManager))]
[HarmonyPatch(nameof(StateManager.PropagateSetValueLocal))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class StateManager_PropagateSetValueLocal_ThroughTraffic_Patch
{
    private static bool Prefix(string objectId, string key)
    {
        return !ThroughTrafficGuard.ShouldBlockPropertyChange(objectId, key);
    }
}
