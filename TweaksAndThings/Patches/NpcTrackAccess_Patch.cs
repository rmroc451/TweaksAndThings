using HarmonyLib;
using Game.State;
using Track;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(TrainController), "CanPlaceAt", new[] { typeof(Location) })]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTrackAccess_Patch
{
    private static bool Prefix(TrainController __instance, Location location, ref bool __result)
    {
        if (!StateManager.IsHost || !NpcTrainOperations.HasTrackAccess) return true;
        __result = location.IsValid && NpcTrackAccessPolicy.CanUse(location.segment.GroupEnabled, location.segment.Available, true) &&
            __instance.CheckForCarAtLocation(location) == null;
        return false;
    }
}
