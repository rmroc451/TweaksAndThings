using Game.State;
using HarmonyLib;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(StateManager), nameof(StateManager.PopulateSnapshotForSave))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class TimetableHistory_Save_Patch
{
    private static void Prefix() => TimetableHistory.Flush();
}
