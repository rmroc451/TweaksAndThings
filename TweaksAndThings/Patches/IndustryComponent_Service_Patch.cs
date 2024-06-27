using Game.State;
using HarmonyLib;
using Model;
using Model.Definition.Data;
using Model.Ops.Definition;
using Model.OpsNew;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(CarExtensions))]
[HarmonyPatch(nameof(CarExtensions.LoadString), typeof(CarLoadInfo), typeof(Load))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class CarExtensions_LoadString_Patch
{
    public static bool Prefix(CarLoadInfo info, Load load, ref string __result)
    {
        bool output = load.id == IndustryComponent_Service_Patch.CrewHoursLoad().id;
        if (output) __result = info.Quantity.FormatCrewHours(load.description);

        return !output;
    }
}

[HarmonyPatch(typeof(CarPrototypeLibrary))]
[HarmonyPatch(nameof(CarPrototypeLibrary.LoadForId), typeof(string))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class CarPrototypeLibrary_LoadForId_Patch
{
    public static bool Prefix(string loadId, ref Load __result)
    {
        Load load = IndustryComponent_Service_Patch.CrewHoursLoad();
        if (loadId == load.id) __result = load;

        return __result == null;
    }
}

[HarmonyPatch(typeof(TeamTrack))]
[HarmonyPatch(nameof(TeamTrack.Service), typeof(IIndustryContext))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class TeamTrack_Service_Patch
{
    public static bool Prefix(IndustryComponent __instance, IIndustryContext ctx)
    {
        //Log.Information($"{nameof(SimplePassengerStop_Service_Patch)} => {((IndustryContext)ctx)._industry.name}");
        return IndustryComponent_Service_Patch.Prefix(__instance, ctx);
    }
}

[HarmonyPatch(typeof(RepairTrack))]
[HarmonyPatch(nameof(RepairTrack.Service), typeof(IIndustryContext))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class RepairTrack_Service_Patch
{
    public static bool Prefix(IndustryComponent __instance, IIndustryContext ctx)
    {
        //Log.Information($"{nameof(SimplePassengerStop_Service_Patch)} => {((IndustryContext)ctx)._industry.name}");
        return IndustryComponent_Service_Patch.Prefix(__instance, ctx);
    }
}

[HarmonyPatch(typeof(SimplePassengerStop))]
[HarmonyPatch(nameof(SimplePassengerStop.Service), typeof(IIndustryContext))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class SimplePassengerStop_Service_Patch
{
    public static bool Prefix(IndustryComponent __instance, IIndustryContext ctx)
    {
        Log.Information($"{nameof(SimplePassengerStop_Service_Patch)} => {((IndustryContext)ctx)._industry.name}");
        return IndustryComponent_Service_Patch.Prefix(__instance, ctx);
    }
}

internal static class IndustryComponent_Service_Patch
{
    public static Load CrewHoursLoad()
    {
        Load load = (Load)ScriptableObject.CreateInstance(typeof(Load));
        load.name = "crew-hours";
        load.description = "Crew";
        load.units = LoadUnits.Quantity;

        return load;
    }

    public static bool Prefix(IndustryComponent __instance, IIndustryContext ctx)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!StateManager.IsHost || !tweaksAndThings.IsEnabled || !(tweaksAndThings?.settings?.EndGearHelpersRequirePayment ?? false)) return true;

        Load load = CrewHoursLoad();

        float rate2 = 24 * 2 * 8;// carLoadRate = 8 crew hours in 30 min loading; (24h * 2 to get half hour chunks * 8 hours to load in those chunks)
        float num2 = 99999999; //QuantityInStorage for crew-hours (infinite where crew can be shuffling about)
        float quantityToLoad = Mathf.Min(num2, IndustryComponent.RateToValue(rate2, ctx.DeltaTime));

        var carsAtPosition = ctx.CarsAtPosition();

        var cabeese = from car in carsAtPosition.Where(c => c.CarType == "NE")
                      where car.IsEmptyOrContains(load)
                      orderby car.QuantityOfLoad(load).quantity descending
                      select car;

        foreach (IOpsCar item in cabeese)
        {
            TrainController tc = UnityEngine.Object.FindAnyObjectByType<TrainController>();
            if (tc.TryGetCarForId(item.Id, out Car car))
            {
                List<LoadSlot> loadSlots = car.Definition.LoadSlots;
                float quantity = 0f;
                float max = 0f;
                for (int i = 0; i < loadSlots.Count; i++)
                {
                    LoadSlot loadSlot = loadSlots[i];
                    if (loadSlot.LoadRequirementsMatch(load) && loadSlot.LoadUnits == load.units)
                    {
                        CarLoadInfo? loadInfo = car.GetLoadInfo(i);

                        quantity = loadInfo.HasValue ? loadInfo.Value.Quantity : 0f;
                        max = loadSlots[i].MaximumCapacity;
                        break;
                    }
                }
                //Log.Information($"{nameof(IndustryComponent_Service_Patch)} {car} => {car.StoppedDuration} => {quantityToLoad} => {quantity}/{max}");
                if (car.StoppedDuration > 30) item.Load(load, quantityToLoad);
            }

            //todo:crew refresh message?
        }

        return true;
    }
}