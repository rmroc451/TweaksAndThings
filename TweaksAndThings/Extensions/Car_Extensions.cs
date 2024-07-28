using Game.Messages;
using Game.State;
using Helpers;
using Model;
using Model.Definition.Data;
using Model.OpsNew;
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

    public static bool NotMotivePower(this Car car) => car is not BaseLocomotive && car.Archetype != Model.Definition.CarArchetype.Tender;

    /// <summary>
    /// For every car in the consist, cost 1 minute of AI Engineer time.
    /// </summary>
    /// <param name="consist"></param>
    public static int CalculateCostForAutoEngineerEndGearSetting(this IEnumerable<Car> consist) =>
        consist.Count() * 60;

    public static bool IsCaboose(this Car car) => car.Archetype == Model.Definition.CarArchetype.Caboose;

    public static bool IsCabooseAndStoppedForLoadRefresh(this Car car) => car.IsCaboose() && car.IsStopped(30f);

    public static Car? CabooseInConsist(this IEnumerable<Car> input) => input.FirstOrDefault(c => c.IsCaboose());

    public static Car? CabooseWithSufficientCrewHours(this Car car, float timeNeeded, HashSet<string> carIdsCheckedAlready, bool decrement = false)
    {
        Car? output = null;
        if (carIdsCheckedAlready.Contains(car.id) || !car.IsCaboose()) return null;

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

    public static Car? HuntingForCabeeseNearCar(this Car car, float timeNeeded, TrainController tc, HashSet<string> carIdsCheckedAlready, bool decrement = false)
    {
        List<(string carId, float distance)> source =
            CarsNearCurrentCar(car, timeNeeded, tc, carIdsCheckedAlready, decrement);

        Car output = FindNearestCabooseFromNearbyCars(tc, source);
        if (output != null) output.CabooseWithSufficientCrewHours(timeNeeded, carIdsCheckedAlready, decrement);

        return output;
    }

    private static Car FindNearestCabooseFromNearbyCars(TrainController tc, List<(string carId, float distance)> source) =>
        (
            from t in source
            where t.distance < 21f //todo: add setting slider for catchment
            orderby t.distance ascending
            select tc.CarForId(t.carId)
        ).FirstOrDefault();

    private static List<(string carId, float distance)> CarsNearCurrentCar(Car car, float timeNeeded, TrainController tc, HashSet<string> carIdsCheckedAlready, bool decrement)
    {
        Vector3 position = car.GetMotionSnapshot().Position;
        Vector3 center = WorldTransformer.WorldToGame(position);
        Rect rect = new Rect(new Vector2(center.x - 30f, center.z - 30f), Vector2.one * 30f * 2f);
        var cars = tc.CarIdsInRect(rect);
        Log.Information($"{nameof(HuntingForCabeeseNearCar)} => {cars.Count()}");
        List<(string carId, float distance)> source =
            cars
            .Select(carId =>
            {
                Car car = tc.CarForId(carId);
                if (car == null || !car.CabooseWithSufficientCrewHours(timeNeeded, carIdsCheckedAlready))
                {
                    return (carId: carId, distance: 1000f);
                }
                Vector3 a = WorldTransformer.WorldToGame(car.GetMotionSnapshot().Position);
                return (carId: carId, distance: Vector3.Distance(a, center));
            }).ToList();
        return source;
    }

    public static void AdjustHotboxValue(this Car car, float hotboxValue) =>
        StateManager.ApplyLocal(
            new PropertyChange(
                car.id, PropertyChange.KeyForControl(PropertyChange.Control.Hotbox),
                new FloatPropertyValue(hotboxValue)
            )
        );
}
