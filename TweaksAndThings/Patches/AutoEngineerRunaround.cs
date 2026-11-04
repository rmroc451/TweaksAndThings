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
using Track.Search;
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
        WaitingForPlanner,
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
        public float ReadyAfter { get; set; }
        public float LastWaitLog { get; set; }
    }

    private static readonly Car.LogicalEnd[] LogicalEnds = { Car.LogicalEnd.A, Car.LogicalEnd.B };
    private static readonly Dictionary<string, PendingRunaround> Pending = new();
    private static readonly System.Reflection.FieldInfo? CachedCarsField = typeof(AutoEngineerPlanner).GetField(
        "_coupledCarsCached", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    internal static bool InProgress(string locomotiveId) => Pending.ContainsKey(locomotiveId);
    internal static void Abort(string locomotiveId) => Pending.Remove(locomotiveId);

    internal static bool TryStart(BaseLocomotive locomotive, Location destination, string destinationCarId)
    {
        if (!StateManager.IsHost || locomotive == null || CachedCarsField == null || locomotive.AutoEngineerPlanner == null ||
            ThroughTrafficGuard.BlockInteraction(locomotive) || !locomotive.IsStopped())
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

        // Validate a physical path around the stationary cut before uncoupling.
        // Ignoring only the power and terminal coupling car prevents a route
        // straight through the freight string from masquerading as a runaround.
        if (AeLocalService.Job(locomotive) is AeLocalJob local)
        {
            var power = consist.Where(c => c.MotivePower()).ToList();
            var ignored = power.Concat(new[] { tailCar }).ToHashSet();
            float length = AutoEngineerPlanner.CalculateTotalLength(power) + 8;
            var route = new List<RouteSearch.Step>();
            if (!RouteSearch.FindRoute(Graph.Shared, locomotive.LocationF, tailLocation, HeuristicCosts.AutoEngineer,
                route, out _, checkForCars: true, trainLength: length, trainMomentum: 0, maxIterations: 5000,
                checkForCarsIgnored: ignored, checkForCarsImpasse: new HashSet<Car>(), limitSwitchIds: null, enableLogging: false))
            {
                ModDiagnosticLog.Write("AE LOCAL PLAN", local.Id + ": runaround rejected; no clear path around the stationary cut.");
                return false;
            }
        }

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
            NpcSetoutPlacement.SecureFreight(consist.Where(c => !c.MotivePower()).ToList());
            boundary.rollingCar.ApplyEndGearChange(boundary.rollingEnd, Car.EndGearStateKey.Anglecock, 0f);
            boundary.motiveCar.HandleCouplerClick(boundary.motiveCar[boundary.motiveEnd].Coupler);

            pending.CurrentPhase = Phase.WaitingForPlanner;
            pending.SawSeparation = !locomotive.EnumerateCoupled().Any(car => car.id == pending.TailCarId);
            pending.ReadyAfter = UnityEngine.Time.realtimeSinceStartup + 0.1f;
            // Keep the native engineer active at zero speed so its next loop
            // refreshes the coupled-car cache and the train length after the cut.
            NpcTrainOperations.Hold(locomotive);
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
                ModLog.Error(stopException, "Could not stop locomotive {LocomotiveId} after a run-around command failed", locoId);
            }
            ModLog.Error(ex, "Could not start Auto Engineer run-around for {LocomotiveId}", locoId);
            return false;
        }
    }

    internal static void Tick()
    {
        if (!StateManager.IsHost) return;
        foreach (var pair in Pending.ToArray())
        {
            var pending = pair.Value;
            if (pending.CurrentPhase != Phase.WaitingForPlanner ||
                UnityEngine.Time.realtimeSinceStartup < pending.ReadyAfter) continue;
            var locomotive = pending.Locomotive;
            var actual = locomotive.EnumerateCoupled().ToList();
            bool separated = actual.All(car => car.id != pending.TailCarId);
            bool refreshed = CachedCarsField?.GetValue(locomotive.AutoEngineerPlanner) is List<Car> cached &&
                cached.Select(car => car.id).ToHashSet().SetEquals(actual.Select(car => car.id));
            if (!separated || !locomotive.IsStopped() || !refreshed)
            {
                if (UnityEngine.Time.realtimeSinceStartup - pending.LastWaitLog >= 10f)
                {
                    pending.LastWaitLog = UnityEngine.Time.realtimeSinceStartup;
                    ModDiagnosticLog.Write("AE LOCAL RUNAROUND", $"{locomotive.DisplayName}: waiting for detached power and native consist-length refresh; separated={separated}; cacheReady={refreshed}.");
                }
                continue;
            }
            try
            {
                pending.CurrentPhase = Phase.RunningAround;
                ModDiagnosticLog.Write("AE LOCAL RUNAROUND", $"{locomotive.DisplayName}: routing {actual.Count} power cars, native length={AutoEngineerPlanner.CalculateTotalLength(actual):F1} m, after the cut.");
                SetWaypoint(locomotive, pending.TailLocation, pending.TailCarId);
            }
            catch (Exception ex)
            {
                Pending.Remove(pair.Key);
                NpcTrainOperations.Hold(locomotive);
                ModLog.Error(ex, "Could not set refreshed run-around waypoint for {LocomotiveId}", pair.Key);
            }
        }
    }

    internal static void OnCoupledChange()
    {
        if (!StateManager.IsHost) return;
        foreach (var pair in Pending.ToArray())
        {
            PendingRunaround pending = pair.Value;
            bool tailIsCoupled = pending.Locomotive.EnumerateCoupled().Any(car => car.id == pending.TailCarId);

            if (pending.CurrentPhase != Phase.RunningAround)
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
                // Setting orders can synchronously report another coupled change.
                // Remove this runaround before notifying the planner so the
                // callback cannot restore the same waypoint recursively.
                Pending.Remove(pair.Key);
                SetWaypoint(pending.Locomotive, pending.Destination, pending.DestinationCarId);
            }
            catch (Exception ex)
            {
                ModLog.Error(ex, "Could not restore the saved Auto Engineer destination for {LocomotiveId}", pair.Key);
                Pending.Remove(pair.Key);
            }
        }
    }

    private static void SetWaypoint(BaseLocomotive locomotive, Location location, string carId)
    {
        var persistence = new AutoEngineerPersistence(locomotive.KeyValueObject);
        var orders = new AutoEngineerOrdersHelper(locomotive, persistence);
        var waypoint = new OrderWaypoint(Graph.LocationToString(location), carId);
        NpcTrainOperations.Trusted(() => orders.SetOrdersValue(mode: AutoEngineerMode.Waypoint, maybeWaypoint: waypoint));
    }
}

[HarmonyLib.HarmonyPatch(typeof(Car))]
[HarmonyLib.HarmonyPatch(nameof(Car.HandleCoupledChange))]
[HarmonyLib.HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class Car_HandleCoupledChange_Runaround_Patch
{
    private static void Postfix() => AutoEngineerRunaround.OnCoupledChange();
}
