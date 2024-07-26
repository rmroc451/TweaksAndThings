using HarmonyLib;
using Model.AI;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using Serilog;
using System.Collections;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(AutoOiler))]
[HarmonyPatch(nameof(AutoOiler.Loop))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoOiler_Loop_Patch
{
    private static ILogger _log => Log.ForContext<AutoOiler_Loop_Patch>();

    public static bool Prefix(AutoOiler __instance, ref IEnumerator __result)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled) return true;
        bool buttonsHaveCost = tweaksAndThings.EndGearHelpersRequirePayment();
        bool cabooseRequired = tweaksAndThings.RequireConsistCabooseForOilerAndHotboxSpotter();

        if (buttonsHaveCost) __result = __instance.MrocAutoOilerLoop(_log, cabooseRequired);
        return !buttonsHaveCost; //only hit this if !buttonsHaveCost, since Loop is a coroutine
    }
}
