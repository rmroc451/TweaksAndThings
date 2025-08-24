using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Model;
using Model.AI;
using Model.Ops;
using Network;
using Network.Messages;
using Railloader;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Track;
using Track.Search;
using UI;
using UI.EngineControls;
using UnityEngine;
using static Track.Search.RouteSearch;
using Location = Track.Location;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(AutoEngineerWaypointControls))]
[HarmonyPatch(nameof(AutoEngineerWaypointControls.ConfigureOptionsDropdown))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoEngineerWaypointControls_ConfigureOptionsDropdown_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<AutoEngineerWaypointControls_ConfigureOptionsDropdown_Patch>();

    private static readonly HashSet<IDisposable> _keyChangeObservers = new HashSet<IDisposable>();
    private static readonly HashSet<string> destinations = new HashSet<string>();
    private static readonly HashSet<Car> consist = new HashSet<Car>();
    private static string _lastLocoCarId = string.Empty;
    private static bool recalcing = false;

    static void Postfix(AutoEngineerWaypointControls __instance, ref OptionsDropdownConfiguration __result)
    {

        _log.Information($"HI BOB");
        foreach(var o in _keyChangeObservers) o.Dispose();
        _keyChangeObservers.Clear();
        destinations.Clear();
        recalcing = false;
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return;
        //_log.Information($"HI2");


        List<DropdownMenu.RowData> rowDatas = __result.Rows.ToList();
        var func = __result.OnRowSelected;
        int origCount = rowDatas.Count;
        int maxRowOrig = origCount - 1;

        var selectedLoco = TrainController.Shared.SelectedLocomotive;
        if (selectedLoco.id != _lastLocoCarId || !(consist?.Any() ?? false))
        {
            _lastLocoCarId = selectedLoco.id;
            consist.Clear();
            consist.UnionWith(selectedLoco.EnumerateCoupled()?.ToList() ?? Enumerable.Empty<Car>());
        }
        var aeoh = new AutoEngineerOrdersHelper(persistence: new AutoEngineerPersistence(selectedLoco.KeyValueObject), locomotive: selectedLoco);
        List<(string destinationId, string destination, float? distance, Location? location)> jumpTos = new();

        foreach(var c in consist)
        {
            AddObserversToCar(__instance, c);
            OpsCarPosition? destination = c.Waybill.HasValue && !c.Waybill.Value.Completed ? c.Waybill.Value.Destination : null;
            bool completed = c.Waybill?.Completed ?? false;
            if (!destination.HasValue && c.TryGetOverrideDestination(OverrideDestination.Repair, OpsController.Shared, out (OpsCarPosition, string)? result)) destination = result.Value.Item1;
            _log.Information($"{c.DisplayName} -> {destination.HasValue}");

            string destId = destination?.Identifier ?? string.Empty;

            if (destinations.Contains(destId)) continue;

            if (destination.HasValue && !completed)
            {
                string destName = destination.Value.DisplayName;
                float? distance = null;

                if (Graph.Shared.TryGetLocationFromPoint(destination.Value.Spans?.FirstOrDefault().GetSegments().FirstOrDefault(), destination.Value.Spans?.FirstOrDefault()?.GetCenterPoint() ?? default, 200f, out Location destLoc))
                {

                    float trainMomentum = 0f;
                    Location start = StateManager.IsHost ? selectedLoco.AutoEngineerPlanner.RouteStartLocation(out trainMomentum) : RouteStartLocation(__instance, selectedLoco);
                    HeuristicCosts autoEngineer = HeuristicCosts.AutoEngineer;
                    List<RouteSearch.Step> list = new List<RouteSearch.Step>();
                    var totLen = StateManager.IsHost ? selectedLoco.AutoEngineerPlanner.CalculateTotalLength() : CalculateTotalLength(selectedLoco);
                    distance = Graph.Shared.FindRoute(start, destLoc, autoEngineer, list, out var metrics, checkForCars: false, totLen, trainMomentum)
                    ? metrics.Distance
                    : null;
                };
                _log.Information($"{c.DisplayName} -> {destName} {destId} {distance?.ToString()}");
                if (distance.HasValue)
                {
                    destinations.Add(destId);
                    jumpTos.Add((
                        destinationId: destId,
                        destination: $"WP> {destName}"
                        , distance: distance
                        , location: (Location?)destLoc
                    ));
                }
            }
        }
        
        jumpTos = jumpTos?.OrderBy(c => c.distance)?.ToList() ?? [];
        var safetyFirst = AutoEngineerOrdersHelper_SendAutoEngineerCommand_Patch.SafetyFirstGoverningApplies() && jumpTos.Any();

        rowDatas.AddRange(jumpTos.Select(j =>
            new DropdownMenu.RowData(
                $"{j.destination} <b>({(j.distance.HasValue ? Units.DistanceText(j.distance.Value) : "N/A")})</b>",
                !safetyFirst ? null : "<i>Disabled; Safety First!</i>"
            )
        ));

        __result = new OptionsDropdownConfiguration(
            rowDatas
            , delegate (int row)
            {
                _log.Information($"{TrainController.Shared.SelectedLocomotive.DisplayName} row {row}/{jumpTos.Count}/{rowDatas.Count}");
                if (row <= maxRowOrig)
                {
                    func(row);
                }
                if (row > maxRowOrig && jumpTos[row - origCount].location.HasValue)
                {
                    if (safetyFirst)
                    {
                        Multiplayer.SendError(StateManager.Shared.PlayersManager.LocalPlayer, "Safety First, find yourself a caboose!", AlertLevel.Error);
                        return;
                    }
                    float trainMomentum = 0f;
                    Location end = jumpTos[row - origCount].location.Value;
                    Location start = RouteStartLocation(__instance, selectedLoco);
                    HeuristicCosts autoEngineer = HeuristicCosts.AutoEngineer;
                    List<RouteSearch.Step> list = new List<RouteSearch.Step>();

                    var totLen = StateManager.IsHost ? selectedLoco.AutoEngineerPlanner.CalculateTotalLength() : CalculateTotalLength(selectedLoco);
                    if (!Graph.Shared.FindRoute(start, end, autoEngineer, list, out var metrics, checkForCars: false, totLen, trainMomentum))
                    {
                        RouteSearch.Metrics metrics2;
                        bool flag = Graph.Shared.FindRoute(start, end, autoEngineer, null, out metrics2);
                        Multiplayer.SendError(StateManager.Shared.PlayersManager.LocalPlayer, flag ? (selectedLoco.DisplayName + " Train too long to navigate to waypoint.") : (selectedLoco.DisplayName + " Unable to find a path to waypoint."), AlertLevel.Error); 
                    } else
                    {
                        var mw = (location: end, carId: string.Empty);
                        aeoh.SetWaypoint(mw.location, mw.carId);
                        aeoh.SetOrdersValue(maybeWaypoint: mw);
                    }
                }
            }
        );

    }

    private static Location RouteStartLocation(AutoEngineerWaypointControls __instance, BaseLocomotive _locomotive) {
        bool num = _locomotive.IsStopped();
        bool? flag = (num ? null : new bool?(_locomotive.velocity >= 0f));

        bool flag2 = flag ?? __instance.OrdersHelper.Orders.Forward;
        if (_locomotive.EndToLogical((!flag2) ? Car.End.R : Car.End.F) == Car.LogicalEnd.A)
        {
            return _locomotive.EnumerateCoupled().First().WheelBoundsA;
        }

        return _locomotive.EnumerateCoupled(Car.LogicalEnd.B).First().WheelBoundsB.Flipped();
    }

    private static float CalculateTotalLength(BaseLocomotive selectedLoco)
    {
        List<Car> coupledCarsCached = selectedLoco.EnumerateCoupled().ToList();
        float num = 0f;
        foreach (Car item in coupledCarsCached)
        {
            num += item.carLength;
        }

        return num + 1.04f * (float)(coupledCarsCached.Count - 1);
    }

    private static void AddObserversToCar(AutoEngineerWaypointControls __instance, Car c)
    {
        AddObserver(__instance, c, Car.KeyOpsWaybill);
        AddObserver(__instance, c, Car.KeyOpsRepairDestination);
        foreach (Car.LogicalEnd logicalEnd in CarInspector_PopulateCarPanel_Patch.ends)
        {
            AddObserver(__instance, c, Car.KeyValueKeyFor(Car.EndGearStateKey.IsCoupled, c.LogicalToEnd(logicalEnd)), true);
        }
    }

    private static void AddObserver(AutoEngineerWaypointControls __instance, Model.Car car, string key, bool clearCarCache = false)
    {
        _keyChangeObservers.Add(
            car.KeyValueObject.Observe(
                key,
                delegate (Value value)
                {
                    _log.Information($"{car.DisplayName} OBSV {key}: {value}; recalcing {recalcing}");
                    if (recalcing) return;
                    try
                    {
                        foreach(var o in _keyChangeObservers)
                        {
                            o.Dispose();
                        }
                        var loco = TrainController.Shared.SelectedLocomotive;
                        TrainController.Shared.SelectedCar = null;
                        _keyChangeObservers.Clear();
                        recalcing = true;
                        new WaitForSeconds(0.25f);
                        TrainController.Shared.SelectedCar = loco;
                    }
                    catch (Exception ex)
                    {
                        _log.ForContext("car", car).Warning(ex, $"{nameof(AddObserver)} {car} Exception logged for {key}");
                    }
                },
                false
            )
        );
    }
}
