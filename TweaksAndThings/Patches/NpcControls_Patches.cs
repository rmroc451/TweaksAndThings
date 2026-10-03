using HarmonyLib;
using Model;
using UI.Builder;
using UI.CarInspector;
using UI.EngineControls;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(LocomotiveControlsUIAdapter), "UpdateOptionsDropdown")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcControls_Cog_Patch
{
    private static bool Prefix(LocomotiveControlsUIAdapter __instance) => !NpcRecoveryControls.Configure(__instance);
}

[HarmonyPatch(typeof(LocomotiveControlsUIAdapter), nameof(LocomotiveControlsUIAdapter.DropdownDidChange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcControls_Mode_Patch
{
    private static bool Prefix() => !ThroughTrafficGuard.IsGenerated(TrainController.Shared?.SelectedLocomotive);
}

[HarmonyPatch(typeof(CarInspector), "PopulatePanel")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcControls_Inspector_Patch
{
    private static bool Prefix(CarInspector __instance, UIPanelBuilder builder)
    {
        if (!ThroughTrafficGuard.IsGenerated(__instance._car)) return true;
        NpcRecoveryControls.Inspector(__instance._car, builder);
        return false;
    }
}
