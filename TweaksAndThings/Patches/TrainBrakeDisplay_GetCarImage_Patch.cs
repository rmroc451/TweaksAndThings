using HarmonyLib;
using Model;
using System;
using System.Collections.Generic;
using UI;
using UI.Tags;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(TrainBrakeDisplay))]
[HarmonyPatch(nameof(TrainBrakeDisplay.Update))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class TrainBrakeDisplay_Update_Patch()
{
    private static bool Prefix(TrainBrakeDisplay __instance)
    {

        TweaksAndThingsPlugin tweaksAndThings = TweaksAndThingsPlugin.Instance!;
        if (!tweaksAndThings.IsEnabled()) return true;

        TweakedOriginalMethods.TrainBrakeDisplay.Update(__instance);
        TrainBrakeDisplayRefresh.MarkUpdated(__instance);

        return false;
    }
}

[HarmonyPatch(typeof(TrainBrakeDisplay))]
[HarmonyPatch(nameof(TrainBrakeDisplay.ColorForCar))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class TrainBrakeDisplay_ColorForCar_Patch
{
    private static bool Prefix(TrainBrakeDisplay __instance, Car car, ref Color __result)
    {
        if (!TryGetDestinationColor(__instance, car, out __result)) return true;

        return false;
    }

    internal static bool TryGetDestinationColor(TrainBrakeDisplay __instance, Car car, out Color color)
    {
        TweaksAndThingsPlugin tweaksAndThings = TweaksAndThingsPlugin.Instance!;
        if (!tweaksAndThings.IsEnabled() || !tweaksAndThings.TrainBrakeDisplayShowsColorsInCalloutMode() ||
            TagController.Shared?.TagsVisible != true)
        {
            color = default;
            return false;
        }

        color = default;
        TweakedOriginalMethods.TrainBrakeDisplay.ColorForCar(__instance, car, ref color);
        return true;
    }
}

[HarmonyPatch(typeof(Car), nameof(Car.HandleCoupledChange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class TrainBrakeDisplay_CouplingRefresh_Patch
{
    private static void Postfix()
    {
        if (TweaksAndThingsPlugin.Instance?.IsEnabled != true) return;

        foreach (var display in UnityEngine.Object.FindObjectsOfType<TrainBrakeDisplay>())
            display.Update();
    }
}

// A visible brake display can miss Unity Update calls when its component is
// disabled. Refresh it from the mod host only if it missed its normal repaint.
internal static class TrainBrakeDisplayRefresh
{
    private static readonly HashSet<TrainBrakeDisplay> updatedThisFrame = new();
    private static TrainBrakeDisplay[] displays = Array.Empty<TrainBrakeDisplay>();
    private static float nextLookupTime;
    private static float nextFallbackLogTime;

    internal static void MarkUpdated(TrainBrakeDisplay display) => updatedThisFrame.Add(display);

    internal static void Tick()
    {
        if (TweaksAndThingsPlugin.Instance?.IsEnabled != true || !WorldPatchReadiness.Ready)
        {
            updatedThisFrame.Clear();
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (now >= nextLookupTime)
        {
            displays = UnityEngine.Object.FindObjectsByType<TrainBrakeDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            nextLookupTime = now + 1f;
        }

        foreach (TrainBrakeDisplay display in displays)
        {
            if (display == null || !display.gameObject.activeInHierarchy || updatedThisFrame.Contains(display)) continue;
            if (display._trainController?.SelectedCar == null) continue;

            TweakedOriginalMethods.TrainBrakeDisplay.Update(display);
            if (now >= nextFallbackLogTime)
            {
                ModDiagnosticLog.Write("TRAIN BRAKE REFRESH",
                    $"Fallback repaint used; componentEnabled={display.enabled}; activeInHierarchy={display.gameObject.activeInHierarchy}; selected={display._trainController.SelectedCar.id}.");
                nextFallbackLogTime = now + 30f;
            }
        }

        updatedThisFrame.Clear();
    }
}

