using HarmonyLib;
using Helpers;
using Model.Database;
using Model.Definition;
using Model.Definition.Data;
using Model.Ops;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(PrefabStoreExtensions))]
[HarmonyPatch(nameof(PrefabStoreExtensions.Random))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class PrefabStoreExtensions_Random_Patch
{
    public static bool Prefix(IPrefabStore prefabStore, CarTypeFilter carTypeFilter, IndustryContext.CarSizePreference sizePreference, Random rnd, ref TypedContainerItem<CarDefinition> __result)
    {
        List<TypedContainerItem<CarDefinition>> list = 
            (from p in prefabStore.AllCarDefinitionInfos.ToList().FindAll((TypedContainerItem<CarDefinition> p) => 
                carTypeFilter.Matches(p.Definition.CarType) && !p.Metadata.Tags.Any(t => t.Equals("deprecated")))
            orderby p.Definition.WeightEmpty
            select p).ToList();
        if (list.Count == 0)
        {
            Log.Error($"Couldn't find car for condition: {carTypeFilter}");
            __result = null;
        }
        __result = list.RandomElementUsingNormalDistribution(sizePreference switch
        {
            IndustryContext.CarSizePreference.Small => 0.2f,
            IndustryContext.CarSizePreference.Medium => 0.4f,
            IndustryContext.CarSizePreference.Large => 0.6f,
            IndustryContext.CarSizePreference.ExtraLarge => 0.8f,
            _ => 0.5f,
        }, rnd);

        return false;
    }
}
