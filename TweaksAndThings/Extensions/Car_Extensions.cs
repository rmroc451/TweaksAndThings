using Game.Messages;
using Helpers;
using Model;
using Model.Definition;
using Model.Definition.Data;
using Model.Ops;
using Model.Ops.Timetable;
using Railloader;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Extensions;

public static class Car_Extensions
{
    private static bool EndGearIssue(this Car car, Car.LogicalEnd end) =>
            (!car[end].IsCoupled && car[end].IsAnglecockOpen) ||
            (car[end].IsCoupled && !car[end].IsAirConnectedAndOpen);

    public static bool EndAirSystemIssue(this Car car)
    {
        bool AEndAirSystemIssue = car.EndGearIssue(Car.LogicalEnd.A);
        bool BEndAirSystemIssue = car.EndGearIssue(Car.LogicalEnd.B);
        bool EndAirSystemIssue = AEndAirSystemIssue || BEndAirSystemIssue;
        return EndAirSystemIssue;
    }

    public static bool HandbrakeApplied(this Model.Car car) =>
        car.air.handbrakeApplied;

    public static bool CarOrEndGearIssue(this Model.Car car) =>
        car.EndAirSystemIssue() || car.HandbrakeApplied();

    public static bool CarAndEndGearIssue(this Model.Car car) =>
        car.EndAirSystemIssue() && car.HandbrakeApplied();

    public static Car? DetermineFuelCar(this Car engine, bool returnEngineIfNull = false)
    {
        if (engine == null) return null;
        Car? car;
        if (engine is SteamLocomotive steamLocomotive && new Func<Car>(steamLocomotive.FuelCar) != null)
        {
            car = steamLocomotive.FuelCar();
        }
        else
        {
            car = engine is DieselLocomotive ? engine : null;
            if (returnEngineIfNull && car == null) car = engine;
        }
        return car;
    }

    public static bool MotivePower(this Car car) => car is BaseLocomotive || car.Archetype == Model.Definition.CarArchetype.Tender;

    /// <summary>
    /// For every car in the consist, cost 1 minute of AI Engineer time.
    /// </summary>
    /// <param name="consist"></param>
    public static int CalculateCostForAutoEngineerEndGearSetting(this IEnumerable<Car> consist) =>
        consist.Count() * 60;

    public static bool IsCaboose(this Car car) => car.Archetype == Model.Definition.CarArchetype.Caboose;

    public static bool IsCabooseAndStoppedForLoadRefresh(this Car car, bool isFull) => car.IsCaboose() && car.IsStopped(30f) && !isFull;

    public static Car? CabooseInConsist(this IEnumerable<Car> input) => input.FirstOrDefault(IsCaboose);

    public static bool ConsistNoFreight(this IEnumerable<Car> input) =>
        input.Where(c => !c.MotivePower()).Any() &&
        input
        .Where(c => !c.MotivePower())
        .All(c => !c.MrocIsFreight());

    public static bool ConsistFreight(this IEnumerable<Car> input) =>
        input.Where(c => !c.MotivePower()).Any() &&
        input
        .Where(c => !c.MotivePower())
        .All(c => c.MrocIsFreight());

    public static bool MrocIsFreight(this Car c) =>
        c.Archetype.IsFreight() || c.Archetype switch
    {
            CarArchetype.LocomotiveDiesel => false,
            CarArchetype.LocomotiveSteam => false,
            CarArchetype.Coach => false,
            CarArchetype.Baggage => false,
            _ => true
        };


    public static bool SelectedEngineExpress(this TrainController input) =>
        input.SelectedLocomotive.TryGetTimetableTrain(out Timetable.Train t) && 
        t.TrainClass == Timetable.TrainClass.First;

    public static Car? FindMyCaboose(this Car car, float timeNeeded, bool decrement = false) =>
        car.CarsNearCurrentCar(timeNeeded, decrement).FindNearestCabooseFromNearbyCars();

    public static Car? CabooseWithSufficientCrewHours(this Car car, float timeNeeded, bool decrement = false)
    {
        Car? output = null;
        if (!car.IsCaboose()) return null;

        List<LoadSlot> loadSlots = car.Definition.LoadSlots;
        for (int i = 0; i < loadSlots.Count; i++)
        {
            CarLoadInfo? loadInfo = car.GetLoadInfo(i);
            if (loadInfo.HasValue)
            {
                CarLoadInfo valueOrDefault = loadInfo.GetValueOrDefault();
                output = valueOrDefault.Quantity >= timeNeeded ? car : null;
                if (decrement && output != null) output.SetLoadInfo(i, valueOrDefault with { Quantity = valueOrDefault.Quantity - timeNeeded });
                break;
            }
        }
        return output;
    }

    private static Car FindNearestCabooseFromNearbyCars(this List<(Car car, bool crewCar, float distance)> source) =>
        source
        .OrderBy(c => c.crewCar ? 0 : 1)
        .ThenBy(c => c.distance)
        .Select(c => c.car)
        .FirstOrDefault();

    private static List<(Car car, bool crewCar, float distance)> CarsNearCurrentCar(this Car car, float timeNeeded, bool decrement)
    {

        var cabeese =
            car.EnumerateCoupled().SelectMany(consistCar =>
            {
                Vector3 position = consistCar.GetMotionSnapshot().Position;
                Vector3 center = WorldTransformer.WorldToGame(position);
                var o = TrainController.Shared
                    .CarIdsInRadius(center, SingletonPluginBase<TweaksAndThingsPlugin>.Shared.CabeeseSearchRadiusInMeters())
                    .Where(c => TrainController.Shared.CarForId(c).IsCaboose());
                return o;
            }).Distinct().Select(c => TrainController.Shared.CarForId(c));


        Log.Information($"{nameof(CarsNearCurrentCar)} => {cabeese.Count()}");

        List<(Car car, bool crewCar, float distance)> source =
            cabeese.Select(c => (car: c, crewCar: c.IsCrewCar(), distance: car.Distance(c))).ToList();

        return source;
    }

    public static float Distance(this Car car1, Car car2)
    {
        Vector3 position = car1.GetMotionSnapshot().Position;
        Vector3 center = WorldTransformer.WorldToGame(position);
        Vector3 a = WorldTransformer.WorldToGame(car2.GetMotionSnapshot().Position);

        return Vector3.Distance(a, center);
                }

    public static bool IsCrewCar(this Car car) =>
        !string.IsNullOrEmpty(TrainController.Shared.SelectedLocomotive.trainCrewId) &&
        car.trainCrewId == TrainController.Shared.SelectedLocomotive.trainCrewId;


    public static void AdjustHotboxValue(this Car car) => car.ControlProperties[PropertyChange.Control.Hotbox] = null;
}
