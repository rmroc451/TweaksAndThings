using Game.Messages;
using Game.State;
using Model;
using Model.AI;
using RMROC451.TweaksAndThings.Extensions;
using RollingStock;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Track;
using UI.EngineControls;

namespace RMROC451.TweaksAndThings.Patches;

/// <summary>
/// Runs the locomotive around its consist using the same anglecock and coupler
/// commands used by the mod's original car interaction logic. The final
/// destination is restored after the saved tail car has been coupled again.
/// </summary>
internal static class AutoEngineerRunaround
{
    private enum Phase
    {
        Decoupling,
        RunningAround
    }

    private sealed class PendingRunaround
    {
        public BaseLocomotive Locomotive = null!;
        public string TailCarId = string.Empty;
        public Location TailLocation;
        public Location Destination;
        public string DestinationCarId = string.Empty;
        public Phase CurrentPhase { get; set; }
        public bool SawSeparation { get; set; }
    }

    private static readonly Car.LogicalEnd[] LogicalEnds = { Car.LogicalEnd.A, Car.LogicalEnd.B };
    private static readonly Dictionary<string, PendingRunaround> Pending = new();

    internal static bool TryStart(BaseLocomotive locomotive, Location destination, string destinationCarId)
    {
        if (locomotive == null || !locomotive.IsStopped())
            return false;

        var consist = locomotive.EnumerateCoupled().GroupBy(car => car.id).Select(group => group.First()).ToList();
        var boundaries = new List<(Car motiveCar, Car rollingCar, Car.LogicalEnd motiveEnd, Car.LogicalEnd rollingEnd)>();
        foreach (Car motiveCar in consist.Where(car => car.MotivePower()))
        foreach (Car.LogicalEnd motiveEnd in LogicalEnds)
        {
            if (!motiveCar.TryGetAdjacentCar(motiveEnd, out Car adjacent) || adjacent.MotivePower())
                continue;

            foreach (Car.LogicalEnd rollingEnd in LogicalEnds)
            {
                if (adjacent.TryGetAdjacentCar(rollingEnd, out Car back) && back.id == motiveCar.id)
                    boundaries.Add((motiveCar, adjacent, motiveEnd, rollingEnd));
            }
        }

        var tailCouplers = consist
            .Where(car => !car.MotivePower())
            .SelectMany(car => LogicalEnds
                .Where(end => !car[end].IsCoupled && !car.TryGetAdjacentCar(end, out _))
                .Select(end => (car, end)))
            .ToList();

        if (!FeaturePolicies.CanBeginRunaround(
                locomotiveStopped: true,
                oneMotiveBoundary: boundaries.Count == 1,
                oneTailCoupler: tailCouplers.Count == 1,
                destinationResolved: true))
            return false;

        string locoId = locomotive.id;
        if (Pending.ContainsKey(locoId))
            return false;

        var (tailCar, tailEnd) = tailCouplers[0];
        Car.End physicalEnd = tailCar.LogicalToEnd(tailEnd);
        Location tailLocation = physicalEnd == Car.End.F ? tailCar.LocationF : tailCar.LocationR;
        var boundary = boundaries[0];

        var pending = new PendingRunaround
        {
            Locomotive = locomotive,
            TailCarId = tailCar.id,
            TailLocation = tailLocation,
            Destination = destination,
            DestinationCarId = destinationCarId ?? string.Empty,
            CurrentPhase = Phase.Decoupling
        };
        Pending.Add(locoId, pending);

        try
        {
            // Close the air connection at both sides, then issue the game's
            // native coupler click command, matching the existing mod logic.
            boundary.motiveCar.ApplyEndGearChange(boundary.motiveEnd, Car.EndGearStateKey.Anglecock, 0f);
            boundary.rollingCar.ApplyEndGearChange(boundary.rollingEnd, Car.EndGearStateKey.Anglecock, 0f);
            boundary.motiveCar.HandleCouplerClick(boundary.motiveCar[boundary.motiveEnd].Coupler);

            pending.CurrentPhase = Phase.RunningAround;
            pending.SawSeparation = !locomotive.EnumerateCoupled().Any(car => car.id == pending.TailCarId);
            SetWaypoint(locomotive, pending.TailLocation, pending.TailCarId);
            return true;
        }
        catch (Exception ex)
        {
            Pending.Remove(locoId);
            try
            {
                new AutoEngineerOrdersHelper(locomotive, new AutoEngineerPersistence(locomotive.KeyValueObject))
                    .SetOrdersValue(mode: AutoEngineerMode.Off);
            }
            catch (Exception stopException)
            {
                Log.Error(stopException, "Could not stop locomotive {LocomotiveId} after a run-around command failed", locoId);
            }
            Log.Error(ex, "Could not start Auto Engineer run-around for {LocomotiveId}", locoId);
            return false;
        }
    }

    internal static void OnCoupledChange()
    {
        foreach (var pair in Pending.ToArray())
        {
            PendingRunaround pending = pair.Value;
            bool tailIsCoupled = pending.Locomotive.EnumerateCoupled().Any(car => car.id == pending.TailCarId);

            if (pending.CurrentPhase == Phase.Decoupling)
                continue;

            if (!tailIsCoupled)
            {
                pending.SawSeparation = true;
                continue;
            }

            if (!FeaturePolicies.ShouldCompleteRunaround(pending.SawSeparation, tailIsCoupled))
                continue;

            try
            {
                SetWaypoint(pending.Locomotive, pending.Destination, pending.DestinationCarId);
                Pending.Remove(pair.Key);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not restore the saved Auto Engineer destination for {LocomotiveId}", pair.Key);
                Pending.Remove(pair.Key);
            }
        }
    }

    private static void SetWaypoint(BaseLocomotive locomotive, Location location, string carId)
    {
        var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject);
        var orders = new AutoEngineerOrdersHelper(locomotive, persistence);
        var waypoint = (location, carId);
        orders.SetWaypoint(waypoint.location, waypoint.carId);
        orders.SetOrdersValue(maybeWaypoint: waypoint);
    }
}

[HarmonyLib.HarmonyPatch(typeof(Car))]
[HarmonyLib.HarmonyPatch(nameof(Car.HandleCoupledChange))]
[HarmonyLib.HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class Car_HandleCoupledChange_Runaround_Patch
{
    private static void Postfix() => AutoEngineerRunaround.OnCoupledChange();
}
