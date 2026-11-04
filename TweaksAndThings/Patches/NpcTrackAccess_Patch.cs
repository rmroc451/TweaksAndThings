using HarmonyLib;
using Game.State;
using System.Collections.Generic;
using System.Reflection;
using Model;
using Track;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(TrainController))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTrackAccess_Patch
{
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(TrainController), "CanPlaceAt",
        new[] { typeof(Location), typeof(IReadOnlyCollection<Car>), typeof(float), typeof(bool), typeof(string).MakeByRefType() });

    private static bool Prefix(Location location, ref bool allowUnavailableTrack, ref string failureReason, ref bool __result)
    {
        if (!StateManager.IsHost || !NpcTrainOperations.HasTrackAccess) return true;
        if (!location.IsValid || !location.segment.GroupEnabled)
        {
            failureReason = "Track not available.";
            __result = false;
            return false;
        }
        if (NpcTrackAccessPolicy.CanUse(location.segment.GroupEnabled, location.segment.Available, true))
            allowUnavailableTrack = true;
        return true;
    }
}
