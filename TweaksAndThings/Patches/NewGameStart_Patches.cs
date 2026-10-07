using HarmonyLib;
using Game.Progression;
using Game.State;
using UI.Builder;
using UI.Common;
using UI.Menu;
using Track;
using System.Collections;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(NewGameMenu), nameof(NewGameMenu.BuildMapSelect))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NewGameStart_Menu_Patch
{
    private static void Postfix(NewGameMenu __instance, UIPanelBuilder builder) => NewGameStartOptions.Build(__instance, builder);
}
[HarmonyPatch(typeof(NewGameMenu), nameof(NewGameMenu.StartButtonClicked))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NewGameStart_Create_Patch
{
    private static bool Prefix(NewGameMenu __instance, out string __state)
    {
        __state = __instance._setupId;
        if ((int)__instance._gameMode != 1) return true;
        if (!NewGameStartOptions.ValidateSelection(__instance, out var error))
        { ModalAlertController.PresentOkay("Starting area unavailable", error); return false; }
        var choice = NewGameStartOptions.Selection(__instance);
        if (choice.ChooseAfterMapLoad || choice.Origin != StartingProgressionPolicy.OriginalOrigin || choice.Unlocked.Count > 0)
            __instance._setupId = NewGameStartOptions.Encode(choice);
        return true;
    }
    private static void Postfix(NewGameMenu __instance, string __state) => __instance._setupId = __state;
}
[HarmonyPatch(typeof(StateManager), nameof(StateManager.ApplyNewGameSetup))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NewGameStart_Presets_Patch
{
    private static void Prefix(StateManager __instance, NewGameSetup setup) => NewGameStartRuntime.ApplyNewGame(__instance, setup);
}
[HarmonyPatch(typeof(StateManager), nameof(StateManager.GetSetupDescriptor))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NewGameStart_Equipment_Patch
{
    private static bool Prefix(StateManager __instance, ref SetupDescriptor __result)
    {
        if (!NewGameStartOptions.TryDecode(__instance.Storage.SetupId, out _)) return true;
        __result = NewGameStartRuntime.GetSetup(__instance.Storage.SetupId)!;
        __instance._setupDescriptor = __result;
        return false;
    }
}
[HarmonyPatch(typeof(Progression), nameof(Progression.Configure))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NewGameStart_Configure_Patch
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(Progression __instance) => NewGameStartRuntime.Configure(__instance);
}
[HarmonyPatch(typeof(Progression), nameof(Progression.Unconfigure))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NewGameStart_Unconfigure_Patch
{
    private static void Postfix(Progression __instance) => NewGameStartRuntime.Restore(__instance);
}
[HarmonyPatch(typeof(Graph), nameof(Graph.RebuildCollections))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NewGameStart_GraphChanged_Patch
{
    private static void Postfix() => WorldPatchReadiness.Changed();
}

[HarmonyPatch(typeof(CompanyModeSetup), nameof(CompanyModeSetup.Setup))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NewGameStart_Picker_Patch
{
    private static void Postfix(TrainController trainController, ref IEnumerator __result)
    {
        if (!NewGameStartPicker.Placing && NewGameStartOptions.TryDecode(StateManager.Shared?.Storage.SetupId, out var choice) &&
            choice?.ChooseAfterMapLoad == true)
            __result = NewGameStartPicker.Wrap(__result, trainController);
    }
}

[HarmonyPatch(typeof(Game.HostManager), "HandlePendingRequestActive")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NewGameStart_Joining_Patch
{
    private static bool Prefix() => !NewGameStartPicker.Choosing;
}
