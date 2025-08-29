using Game;
using Game.State;
using HarmonyLib;
using Model;
using Model.Definition.Data;
using Model.Ops.Definition;
using Model.Ops;
using Network;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using RollingStock;
using Serilog;
using System;
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
        bool output = load.id == OpsController_AnnounceCoalescedPayments_Patch.CrewLoadHours.id;
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
        Load load = OpsController_AnnounceCoalescedPayments_Patch.CrewLoadHours;
        if (loadId == load.id) __result = load;

        return __result == null;
    }
}

[HarmonyPatch(typeof(OpsController))]
[HarmonyPatch(nameof(OpsController.AnnounceCoalescedPayments))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class OpsController_AnnounceCoalescedPayments_Patch
{
    static Dictionary<string, (bool spotted, bool filling)> CrewCarDict = new();

    public static (bool spotted, bool filling) CrewCarStatus(Car car)
    {
        bool found = CrewCarDict.TryGetValue(car.id, out (bool spotted, bool filling) val);

        if (!found) CrewCarDict.Add(car.id, (false, false));

        return val;
    }

    static GameDateTime dateTime = TimeWeather.Now;
    static readonly IEnumerable<Type> refillLocations = 
        new List<Type>() {
        typeof(PassengerStop),
        typeof(SimplePassengerStop),
        typeof(TeamTrack),
        typeof(RepairTrack)
        };

    private static Load _crewLoadHours;
    internal static Load CrewLoadHours => _crewLoadHours ?? CrewHoursLoad();
    private static Load CrewHoursLoad()
    {
        Load load = (Load)ScriptableObject.CreateInstance(typeof(Load));
        load.name = "crew-hours";
        load.description = "Crew";
        load.units = LoadUnits.Quantity;

        return load;
    }

    private static void CarLoadCrewHelper(Car car, float deltaTime)
    {
        float rate2 = 24 * 2 * 8;// carLoadRate = 8 crew hours in 30 min loading; (24h * 2 to get half hour chunks * 8 hours to load in those chunks)
        float num2 = 99999999; //QuantityInStorage for crew-hours (infinite where crew can be shuffling about)
        float quantityToLoad = Mathf.Min(num2, IndustryComponent.RateToValue(rate2, deltaTime));
        if (car.IsCaboose() && !CrewCarStatus(car).spotted)
        {
            CrewCarDict[car.id] = (true, CrewCarDict[car.id].filling);
        }
        if (car.IsCabooseAndStoppedForLoadRefresh(new OpsCarAdapter(car, OpsController.Shared).IsFull(CrewHoursLoad())))
        {
            if (!CrewCarDict[car.id].filling) Multiplayer.Broadcast($"{Hyperlink.To(car)}: \"Topping off caboose crew.\"");
            CrewCarDict[car.id] = (CrewCarDict[car.id].spotted, true);
            var data = car.QuantityCapacityOfLoad(CrewHoursLoad());
            if ((data.quantity + quantityToLoad > data.capacity) && data.quantity < data.capacity)
            {
                quantityToLoad = data.capacity; //ensure topping off
                Multiplayer.Broadcast($"{Hyperlink.To(car)}: \"Caboose crew topped off.\"");
                CrewCarDict[car.id] = (CrewCarDict[car.id].spotted, false);
            }
            new OpsCarAdapter(car, OpsController.Shared).Load(CrewHoursLoad(), quantityToLoad);
        }
    }

    public static bool Prefix(IndustryComponent __instance)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!StateManager.IsHost || !tweaksAndThings.IsEnabled() || !tweaksAndThings.EndGearHelpersRequirePayment() || tweaksAndThings.DayLoadCrewHours()) return true;

        TrainController tc = UnityEngine.Object.FindAnyObjectByType<TrainController>();
        try {

            var passengerStops = OpsController.Shared.AllIndustries
                .SelectMany(i => i.TrackDisplayables.Where(t => refillLocations.Contains(t.GetType())));
            //Log.Debug($"{nameof(OpsController_AnnounceCoalescedPayments_Patch)} => Caboose Helper => PassengerStops => {string.Join(",", passengerStops)}");

            var cabeese = passengerStops
                .SelectMany(t => t.TrackSpans?.Select(s => (tc.CarsOnSpan(s) ?? Enumerable.Empty<Car>()).Where(c => c.IsCaboose()))?.SelectMany(c => c?.Select(c2 => (t, c2))));
            //Log.Debug($"{nameof(OpsController_AnnounceCoalescedPayments_Patch)} => Caboose Helper => PassengerStops Cabeese => {string.Join(",", cabeese?.Select(c => $"{c.t} : {c.c2}") ?? [])}");

            CrewCarDict = CrewCarDict.Where(kvp => cabeese.Select(c => c.c2.id).Contains(kvp.Key)).ToDictionary(k => k.Key, v => v.Value);

            var deltaTime = (float)(TimeWeather.Now.TotalSeconds - dateTime.TotalSeconds);
            foreach (var caboose in cabeese)
            {
                //Log.Debug($"{nameof(OpsController_AnnounceCoalescedPayments_Patch)} => Caboose Helper ({deltaTime}) => {caboose.t} : {caboose.c2}");
                CarLoadCrewHelper(caboose.c2, deltaTime);
            }
            dateTime = TimeWeather.Now;
        } catch (System.Exception ex)
        {
            Log.Error(ex, "error with announce caboose helper");
        }

        return true;
    }
}