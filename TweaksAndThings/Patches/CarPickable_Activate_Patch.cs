using HarmonyLib;
using Railloader;
using RollingStock;
using UnityEngine;


namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(CarPickable))]
[HarmonyPatch(nameof(CarPickable.Activate), typeof(PickableActivateEvent))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class CarPickable_Activate_Patch
{

    static float clicked = 0;
    static float clicktime = 0;
    static float clickdelay = 0.5f;

    private static bool Prefix(CarPickable __instance, PickableActivateEvent evt)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled) return true;

        if (OnPointerDown(evt))
        {
            CameraSelector.shared.FollowCar(__instance.car);
            return false;
        }

        return true;
    }

    public static bool OnPointerDown(PickableActivateEvent evt)
    {
        bool output = false;
        if (evt.Activation == PickableActivation.Primary)
        {
            clicked++;
            if (clicked == 1) clicktime = Time.time;

            if (clicked > 1 && Time.time - clicktime < clickdelay)
            {
                clicked = 0;
                clicktime = 0;
                output = true;

            }
            else if (clicked > 2 || Time.time - clicktime > 1) clicked = 0;
        }
        return output;
    }
}
