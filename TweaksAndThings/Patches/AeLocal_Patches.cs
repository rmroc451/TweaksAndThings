using HarmonyLib;
using Model.AI;
using UI.EngineControls;
using Model;
using Track;
using Game.Messages;
using UnityEngine;
using Game.State;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(LocomotiveControlsUIAdapter), "OnEnable")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_ModeOptions_Patch
{
    private static void Postfix(LocomotiveControlsUIAdapter __instance)
    {
        if (__instance.modeDropdown.options.Count == 4) __instance.modeDropdown.AddOptions(new System.Collections.Generic.List<string> { "AE Local" });
    }
}

[HarmonyPatch(typeof(LocomotiveControlsUIAdapter), nameof(LocomotiveControlsUIAdapter.DropdownDidChange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_ModeChange_Patch
{
    private static bool Prefix(LocomotiveControlsUIAdapter __instance, object[] __args)
    {
        if (TweaksAndThingsPlugin.Instance?.IsEnabled != true) return true;
        int index = (int)__args[0];
        if (index == 4) { AeLocalService.Enter(LocomotiveControlsUIAdapter.Locomotive); return false; }
        var job = AeLocalService.Job(LocomotiveControlsUIAdapter.Locomotive);
        if (job != null) AeLocalService.Cancel(job);
        return true;
    }
}

[HarmonyPatch(typeof(LocomotiveControlsUIAdapter), "UpdateSelectedControlSet", new[] { typeof(Orders) })]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_ModeLabel_Patch
{
    private static void Postfix(LocomotiveControlsUIAdapter __instance)
    {
        var job = AeLocalService.Job(LocomotiveControlsUIAdapter.Locomotive);
        if (job != null && job.Phase != "Complete" && __instance.modeDropdown.options.Count > 4) __instance.modeDropdown.SetValueWithoutNotify(4);
    }
}

[HarmonyPatch(typeof(AutoEngineer), nameof(AutoEngineer.SetTargets))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_DispatchNotice_Patch
{
    private static void Postfix(AutoEngineer __instance, AutoEngineer.Targets targets)
    {
        AeLocalNotices.Observe(__instance.Locomotive, targets);
        NpcTrainOperations.ApplyRollingSpawn(__instance);
    }
}

// The native helper clears waypoints while setup is held in ROAD mode.
// Capture the pick before it is converted into an engineer movement command.
[HarmonyPatch(typeof(AutoEngineerOrdersHelper), nameof(AutoEngineerOrdersHelper.SetWaypoint))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_SetupWaypoint_Patch
{
    private static bool Prefix(Car ____locomotive, Location location) => !AeLocalService.CaptureSetupWaypoint(____locomotive, location);
}

[HarmonyPatch(typeof(AutoEngineerOrdersHelper), nameof(AutoEngineerOrdersHelper.SetOrdersValue))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_MaxSpeed_Patch
{
    private static bool Prefix(Car ____locomotive, AutoEngineerMode? mode, bool? forward, int? maxSpeedMph,
        float? distance, System.ValueTuple<Location, string>? maybeWaypoint) =>
        !maxSpeedMph.HasValue || mode.HasValue || forward.HasValue || distance.HasValue || maybeWaypoint.HasValue ||
        !AeLocalService.SetMaxSpeed(____locomotive, maxSpeedMph.Value);
}

// Display the saved limit while actual setup/paused orders remain at zero.
[HarmonyPatch(typeof(AutoEngineerWaypointControls), "OnOrdersDidChange")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_MaxSpeedDisplay_Patch
{
    private static void Prefix(AutoEngineerWaypointControls __instance, ref Orders orders)
    {
        var job = AeLocalService.Job(__instance.Locomotive);
        if (job != null && job.Phase != "Complete") orders = new Orders(orders.Mode, orders.Forward, job.MaxSpeedMph, orders.Waypoint);
    }
}

[HarmonyPatch(typeof(AutoEngineerRoadControls), "OnOrdersDidChange")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_RoadMaxSpeedDisplay_Patch
{
    private static void Prefix(AutoEngineerRoadControls __instance, ref Orders orders)
    {
        var job = AeLocalService.Job(__instance.Locomotive);
        if (job != null && job.Phase != "Complete") orders = new Orders(orders.Mode, orders.Forward, job.MaxSpeedMph, orders.Waypoint);
    }
}

internal static class AeLocal_SpeedControls
{
    internal static void Display(EngineControlSetBase controls, UI.CarControlSlider slider, TMPro.TMP_Text label)
    {
        var job = AeLocalService.Job(controls.Locomotive);
        if (job == null || job.Phase == "Complete") return;
        slider.enabled = true;
        slider.interactable = StateManager.IsHost;
        slider.minValue = 0;
        slider.maxValue = AutoEngineerMode.Waypoint.MaxSpeedMph() / 5f;
        slider.SetValueWithoutNotify(job.MaxSpeedMph / 5f);
        label.SetText("{0}", job.MaxSpeedMph);
    }
}

[HarmonyPatch(typeof(AutoEngineerRoadControls), "UpdateControls")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_RoadSpeedRefresh_Patch
{
    private static void Postfix(AutoEngineerRoadControls __instance) =>
        AeLocal_SpeedControls.Display(__instance, __instance.maxSpeedSlider, __instance.maxSpeedLabel);
}

[HarmonyPatch(typeof(AutoEngineerWaypointControls), "UpdateControls")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_WaypointSpeedRefresh_Patch
{
    private static void Postfix(AutoEngineerWaypointControls __instance) =>
        AeLocal_SpeedControls.Display(__instance, __instance.maxSpeedSlider, __instance.maxSpeedLabel);
}

[HarmonyPatch(typeof(AutoEngineerRoadControls), "HandleMaxSpeedDidChange")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_RoadSpeedInput_Patch
{
    private static bool Prefix(AutoEngineerRoadControls __instance, object[] __args) =>
        !AeLocalService.SetMaxSpeed(__instance.Locomotive, Mathf.RoundToInt((float)__args[0] * 5));
}

[HarmonyPatch(typeof(AutoEngineerWaypointControls), "HandleMaxSpeedDidChange")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_WaypointSpeedInput_Patch
{
    private static bool Prefix(AutoEngineerWaypointControls __instance, object[] __args) =>
        !AeLocalService.SetMaxSpeed(__instance.Locomotive, Mathf.RoundToInt((float)__args[0] * 5));
}
