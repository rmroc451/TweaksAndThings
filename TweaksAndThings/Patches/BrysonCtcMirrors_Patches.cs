using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Track.Signals;

namespace RMROC451.TweaksAndThings.Patches;

//[HarmonyPatch(typeof(CTCPanelController), "OnEnableWithProperties")]
//[HarmonyPatchCategory("RMROC451TweaksAndThings")]
//internal static class BrysonCtcMirrors_Create_Patch
//{
//    private static void Postfix(CTCPanelController __instance) => BrysonCtcMirrors.Add(__instance);
//}

//[HarmonyPatch(typeof(CTCPanelController), "PanelGroupsForInterlockingId")]
//[HarmonyPatchCategory("RMROC451TweaksAndThings")]
//internal static class BrysonCtcMirrors_Code_Patch
//{
//    private static void Postfix(ref IEnumerable<CTCPanelGroup> __result) =>
//        __result = __result.Where(group => group.GetComponent<BrysonCtcMirror>() == null);
//}
