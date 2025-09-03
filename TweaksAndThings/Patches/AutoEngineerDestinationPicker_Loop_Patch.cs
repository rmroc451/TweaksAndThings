using HarmonyLib;
using Helpers;
using Railloader;
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
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return true;

        __result = Loop(__instance);

        return false;
    }

    private static IEnumerator Loop(AutoEngineerDestinationPicker __instance)
    {
        Hit valueOrDefault;
        Location location;
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1/60);
        while (true)
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
                if (!currentOrdersGotoLocation.Equals(location) && __instance.MouseClicked)
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
        Log.Debug("DestinationPicker Hit: {hit} {car} {end}", valueOrDefault.Location, valueOrDefault.CarInfo?.car, valueOrDefault.CarInfo?.end);
        __instance._ordersHelper.SetWaypoint(location, valueOrDefault.CarInfo?.car.id);
        __instance.StopLoop();
    }
}
