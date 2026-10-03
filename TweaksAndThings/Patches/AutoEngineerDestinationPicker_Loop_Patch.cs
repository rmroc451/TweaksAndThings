using HarmonyLib;
using Helpers;
using Serilog;
using System.Collections;
using Track;
using UI;
using UnityEngine;
using static UI.AutoEngineerDestinationPicker;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(AutoEngineerDestinationPicker))]
[HarmonyPatch(nameof(AutoEngineerDestinationPicker.Loop))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoEngineerDestinationPicker_Loop_Patch
{
    static bool Prefix(AutoEngineerDestinationPicker __instance, ref IEnumerator __result)
    {
        TweaksAndThingsPlugin tweaksAndThings = TweaksAndThingsPlugin.Instance!;
        if (!tweaksAndThings.IsEnabled()) return true;

        __result = Loop(__instance);

        return false;
    }

    private static IEnumerator Loop(AutoEngineerDestinationPicker __instance)
    {
        Hit valueOrDefault = default;
        Location location = default;
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1f / 60f);
        while (!__instance.DidEscape())
        {
            Location? currentOrdersGotoLocation = __instance.GetCurrentOrdersGotoLocation();
            Hit? hit = __instance.HitLocation();
            if (hit.HasValue)
            {
                valueOrDefault = hit.GetValueOrDefault();
                location = valueOrDefault.Location;
                Graph.PositionRotation positionRotation = __instance._graph.GetPositionRotation(location);
                __instance.destinationMarker.position = WorldTransformer.GameToWorld(positionRotation.Position);
                __instance.destinationMarker.rotation = positionRotation.Rotation;
                __instance.destinationMarker.gameObject.SetActive(value: true);
                if (FeaturePolicies.ShouldAcceptWaypointPickerHit(
                    __instance.MouseClicked,
                    escapePressed: __instance.DidEscape(),
                    locationChanged: !currentOrdersGotoLocation.Equals(location)))
                {
                    break;
                }
            }
            else
            {
                __instance.destinationMarker.gameObject.SetActive(value: false);
            }
            yield return wait;
        }
        if (__instance.DidEscape() || !__instance.MouseClicked)
        {
            __instance.StopLoop();
            yield break;
        }

        Log.Debug("DestinationPicker Hit: {hit} {car} {end}", valueOrDefault.Location, valueOrDefault.CarInfo?.car, valueOrDefault.CarInfo?.end);
        __instance._ordersHelper.SetWaypoint(location, valueOrDefault.CarInfo?.car.id);
        __instance.StopLoop();
    }
}
