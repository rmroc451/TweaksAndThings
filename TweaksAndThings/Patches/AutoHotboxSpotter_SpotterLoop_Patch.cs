using HarmonyLib;
using Model.AI;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using Serilog;
using System.Collections;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(AutoHotboxSpotter))]
[HarmonyPatch(nameof(AutoHotboxSpotter.SpotterLoop))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoHotboxSpotter_SpotterLoop_Patch
{
    private static ILogger _log => Log.ForContext<AutoHotboxSpotter_SpotterLoop_Patch>();

    public static bool Prefix(AutoHotboxSpotter __instance, ref IEnumerator __result)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled) return true;
        bool buttonsHaveCost = tweaksAndThings.EndGearHelpersRequirePayment();
        bool cabooseRequired = tweaksAndThings.RequireConsistCabooseForOilerAndHotboxSpotter();

        if (buttonsHaveCost) __result = __instance.MrocAutoHotboxSpotterLoop(_log, cabooseRequired);
        return !buttonsHaveCost; //only hit this if !buttonsHaveCost, since Loop is a coroutine
    }
}
