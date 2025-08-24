using HarmonyLib;
using Model;
using Railloader;
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

        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return true;

        TweakedOriginalMethods.TrainBrakeDisplay.Update(__instance);

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
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled() || !tweaksAndThings.TrainBrakeDisplayShowsColorsInCalloutMode() || !TagController.Shared.TagsVisible) return true;

        TweakedOriginalMethods.TrainBrakeDisplay.ColorForCar(__instance, car, ref __result);

        return false;
    }
}

