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
    private static bool Prefix(LocomotiveControlsUIAdapter __instance) => !NpcRecoveryControls.Configure(__instance) && !AeLocalWindow.Configure(__instance);
}

[HarmonyPatch(typeof(LocomotiveControlsUIAdapter), nameof(LocomotiveControlsUIAdapter.DropdownDidChange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcControls_Mode_Patch
{
    private static bool Prefix() => !ThroughTrafficGuard.IsRestricted(TrainController.Shared?.SelectedLocomotive);
}

[HarmonyPatch(typeof(CarInspector), "PopulatePanel")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcControls_Inspector_Patch
{
    private static bool Prefix(CarInspector __instance, UIPanelBuilder builder)
    {
        if (!ThroughTrafficGuard.IsRestricted(__instance._car)) return true;
        if (ThroughTrafficGuard.IsGenerated(__instance._car))
            NpcRecoveryControls.Inspector(__instance._car, builder);
        else
            AeLocalWindow.Inspector(__instance._car, builder);
        return false;
    }
}
