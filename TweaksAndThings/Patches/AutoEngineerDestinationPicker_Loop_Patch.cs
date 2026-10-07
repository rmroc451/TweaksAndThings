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
        bool acceptedClick = false;
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1f / 60f);
        while (!__instance.DidEscape())
        {
            Hit? hit = __instance.HitLocation();
            bool mouseClicked = __instance.MouseClicked;
            if (hit.HasValue)
            {
                valueOrDefault = hit.GetValueOrDefault();
                location = valueOrDefault.Location;
                Graph.PositionRotation positionRotation = __instance._graph.GetPositionRotation(location);
                __instance.destinationMarker.position = WorldTransformer.GameToWorld(positionRotation.Position);
                __instance.destinationMarker.rotation = positionRotation.Rotation;
                __instance.destinationMarker.gameObject.SetActive(value: true);
                if (mouseClicked && !__instance.DidEscape())
                {
                    acceptedClick = true;
                    ModLog.Information("Waypoint picker accepted track click at {Location}", location);
                    break;
                }
            }
            else
            {
                __instance.destinationMarker.gameObject.SetActive(value: false);
                if (mouseClicked)
                    ModLog.Warning("Waypoint picker received a click but found no track location under the cursor");
            }
            yield return wait;
        }
        if (__instance.DidEscape() || !acceptedClick)
        {
            __instance.StopLoop();
            yield break;
        }

        ModLog.Debug("DestinationPicker Hit: {hit} {car} {end}", valueOrDefault.Location, valueOrDefault.CarInfo?.car, valueOrDefault.CarInfo?.end);
        __instance._ordersHelper.SetWaypoint(location, valueOrDefault.CarInfo?.car.id);
        __instance.StopLoop();
    }
}
