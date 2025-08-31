using GalaSoft.MvvmLight.Messaging;
using Game.Events;
using Game.Messages;
using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Model;
using Model.AI;
using Model.Ops;
using Model.Ops.Timetable;
using Model.Physics;
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
using static Track.Search.RouteSearch;
using Location = Track.Location;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(AutoEngineerWaypointControls))]
[HarmonyPatch(nameof(AutoEngineerWaypointControls.ConfigureOptionsDropdown))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoEngineerWaypointControls_ConfigureOptionsDropdown_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<AutoEngineerWaypointControls_ConfigureOptionsDropdown_Patch>();
    private static readonly HashSet<IDisposable> _keyChangeObservers = [];
    private static HashSet<OpsCarPosition?> locoConsistDestinations = [];
    private static Game.GameDateTime? timetableSaveTime = null;
    static string getDictKey(Car car) => car.DisplayName;
    static Car placeholder = new();

    static void Postfix(AutoEngineerWaypointControls __instance, ref OptionsDropdownConfiguration __result)
    {
        try
        {
            PrepLocoUsage(__instance, out BaseLocomotive selectedLoco, out int numberOfCars);
            TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
            if (!tweaksAndThings.IsEnabled() || !ShouldRecalc(selectedLoco)) return;

            timetableSaveTime = TimetableController.Shared.CurrentDocument.Modified;
            Messenger.Default.Unregister(placeholder);

            Messenger.Default.Register(placeholder, delegate (TimetableDidChange evt)
            {
                _log.Debug("Received {evt}, rebuilding.", evt);
                Rebuild(__instance, selectedLoco, evt.GetType().Name);
            });
            Messenger.Default.Register(placeholder, delegate (CarTrainCrewChanged evt)
            {
                _log.Debug("Received {evt}, rebuilding {1} - {2}.", evt, selectedLoco.id, evt.CarId);
                Rebuild(__instance, selectedLoco, evt.GetType().Name);
            });
            Messenger.Default.Register(placeholder, delegate (TrainCrewsDidChange evt)
            {
                _log.Debug("Received {evt}, rebuilding.", evt);
                Rebuild(__instance, selectedLoco, evt.GetType().Name);
            });
            Messenger.Default.Register(placeholder, delegate (UpdateTrainCrews evt)
            {
                _log.Debug("Received {evt}, rebuilding.", evt);
                Rebuild(__instance, selectedLoco, evt.GetType().Name);
            });

            IterateCarsDetectDestinations(
                __instance,
                __result,
                selectedLoco,
                numberOfCars,
                out List<DropdownMenu.RowData> rowDatas,
                out Action<int> func,
                out int origCount,
                out int maxRowOrig,
                out AutoEngineerOrdersHelper aeoh
            );

            List<(string destinationId, string destination, float? distance, float sortDistance, Location? location)> jumpTos = BuildJumpToOptions(__instance, selectedLoco);

            __result = WireUpJumpTosToSettingMenu(
                __instance,
                selectedLoco,
                rowDatas,
                func,
                origCount,
                maxRowOrig,
                aeoh,
                ref jumpTos
            );
        } catch(Exception ex)
        {
            _log.Error(ex, "I have a very unique set of skills; I will find you and I will squash you.");
        }        
    }

    private static bool ShouldRecalc(BaseLocomotive selectedLoco)
    {
        bool output = false;
        string locoKey = getDictKey(selectedLoco);
        List<Car> consist = new List<Car>();
        consist = selectedLoco.EnumerateCoupled().ToList();
        HashSet<OpsCarPosition?> destinations = consist.Where(c => GetCarDestinationIdentifier(c).HasValue).Select(GetCarDestinationIdentifier).ToHashSet();

        output |= !locoConsistDestinations.Equals(destinations);
        output |= !(_keyChangeObservers?.Any() ?? false);
        output |= selectedLoco.TryGetTimetableTrain(out _) && TimetableController.Shared.CurrentDocument.Modified != timetableSaveTime;

        return output;
    }

    private static OptionsDropdownConfiguration WireUpJumpTosToSettingMenu(AutoEngineerWaypointControls __instance, BaseLocomotive selectedLoco, List<DropdownMenu.RowData> rowDatas, Action<int> func, int origCount, int maxRowOrig, AutoEngineerOrdersHelper aeoh, ref List<(string destinationId, string destination, float? distance, float sortDistance, Location? location)> jumpTos)
    {
        OptionsDropdownConfiguration __result;
        jumpTos = jumpTos?.OrderBy(c => c.sortDistance)?.ToList() ?? default;
        var localJumpTos = jumpTos.ToList();
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
                _log.Debug($"{__instance.Locomotive.DisplayName} row {row}/{localJumpTos.Count}/{rowDatas.Count}");
                if (row <= maxRowOrig)
                {
                    func(row);
                }
                if (row > maxRowOrig && localJumpTos[row - origCount].location.HasValue)
                {
                    if (safetyFirst)
                    {
                        Multiplayer.SendError(StateManager.Shared.PlayersManager.LocalPlayer, "Safety First, find yourself a caboose!", AlertLevel.Error);
                        return;
                    }
                    float trainMomentum = 0f;
                    Location end = localJumpTos[row - origCount].location.Value;
                    Location start = RouteStartLocation(__instance, selectedLoco);
                    HeuristicCosts autoEngineer = HeuristicCosts.AutoEngineer;
                    List<RouteSearch.Step> list = new List<RouteSearch.Step>();

                    var totLen = StateManager.IsHost ? selectedLoco.AutoEngineerPlanner.CalculateTotalLength() : CalculateTotalLength(selectedLoco);
                    if (!Graph.Shared.FindRoute(start, end, autoEngineer, list, out var metrics, checkForCars: false, totLen, trainMomentum))
                    {
                        RouteSearch.Metrics metrics2;
                        bool flag = Graph.Shared.FindRoute(start, end, autoEngineer, null, out metrics2);
                        Multiplayer.SendError(StateManager.Shared.PlayersManager.LocalPlayer, flag ? (getDictKey(selectedLoco) + " Train too long to navigate to waypoint.") : (getDictKey(selectedLoco) + " Unable to find a path to waypoint."), AlertLevel.Error);
                    }
                    else
                    {
                        var mw = (location: end, carId: string.Empty);
                        aeoh.SetWaypoint(mw.location, mw.carId);
                        aeoh.SetOrdersValue(maybeWaypoint: mw);
                    }
                }
            }
        );
        return __result;
    }

    private static List<(string destinationId, string destination, float? distance, float sortDistance, Location? location)> BuildJumpToOptions(AutoEngineerWaypointControls __instance, BaseLocomotive selectedLoco)
    {
        List<(string destinationId, string destination, float? distance, float sortDistance, Location? location)> jumpTos = new();
        foreach (OpsCarPosition ocp in locoConsistDestinations)
        {
            string destName = ocp.DisplayName;
            string destId = ocp.Identifier;
            float? distance = null;
            float sortdistance = 0f;
            if (
                Graph.Shared.TryGetLocationFromPoint(
                    ocp.Spans?.FirstOrDefault().GetSegments().FirstOrDefault(),
                    ocp.Spans?.FirstOrDefault()?.GetCenterPoint() ?? default,
                    200f,
                    out Location destLoc
                )
            )
            {
                float trainMomentum = 0f;
                Location start = StateManager.IsHost ? selectedLoco.AutoEngineerPlanner.RouteStartLocation(out trainMomentum) : RouteStartLocation(__instance, selectedLoco);
                HeuristicCosts autoEngineer = HeuristicCosts.AutoEngineer;
                List<RouteSearch.Step> list = new List<RouteSearch.Step>();
                var totLen = StateManager.IsHost ? selectedLoco.AutoEngineerPlanner.CalculateTotalLength() : CalculateTotalLength(selectedLoco);
                distance = Graph.Shared.FindRoute(start, destLoc, autoEngineer, list, out var metrics, checkForCars: false, totLen, trainMomentum)
                ? metrics.Distance
                : null;
                sortdistance = Graph.Shared.FindRoute(start, destLoc, autoEngineer, list, out metrics, checkForCars: false, 0f, trainMomentum)
                ? metrics.Distance
                : float.MaxValue;
            };
            _log.Debug($"{getDictKey(selectedLoco)} ->  {destName} {destId} {distance?.ToString()}");
            jumpTos.Add((
                destinationId: destId,
                destination: $"WP> {destName}"
                , distance: distance
                , sortdistance: sortdistance
                , location: (Location?)destLoc
            ));
        }

        if (selectedLoco.TryGetTimetableTrain(out Timetable.Train t))
        {
            _log.Information($"{getDictKey(selectedLoco)} -> {t.DisplayStringLong}");
            foreach (var e in t.Entries)
            {
                var stp = TimetableController.Shared.GetAllStations().FirstOrDefault(ps => ps.code == e.Station);
                _log.Information($"{getDictKey(selectedLoco)} -> {t.DisplayStringLong} -> {e.Station} {stp}");
                if (stp != null)
                {
                    try
                    {
                        string destName = t.TrainType == Timetable.TrainType.Passenger ? stp.passengerStop.DisplayName : stp.DisplayName;
                        string destId = t.TrainType == Timetable.TrainType.Passenger ? stp.passengerStop.identifier : stp.code;
                        float? distance = null;
                        float sortdistance = 0f;
                        if (
                            Graph.Shared.TryGetLocationFromPoint(
                                stp.passengerStop.TrackSpans?.FirstOrDefault().GetSegments().FirstOrDefault(),
                                stp.passengerStop.TrackSpans?.FirstOrDefault()?.GetCenterPoint() ?? default,
                                200f,
                                out Location destLoc
                            )
                        )
                        {
                            float trainMomentum = 0f;
                            Location start = StateManager.IsHost ? selectedLoco.AutoEngineerPlanner.RouteStartLocation(out trainMomentum) : RouteStartLocation(__instance, selectedLoco);
                            HeuristicCosts autoEngineer = HeuristicCosts.AutoEngineer;
                            List<RouteSearch.Step> list = new List<RouteSearch.Step>();
                            var totLen = StateManager.IsHost ? selectedLoco.AutoEngineerPlanner.CalculateTotalLength() : CalculateTotalLength(selectedLoco);
                            distance = Graph.Shared.FindRoute(start, destLoc, autoEngineer, list, out var metrics, checkForCars: false, totLen, trainMomentum)
                            ? metrics.Distance
                            : null;
                            sortdistance = Graph.Shared.FindRoute(start, destLoc, autoEngineer, list, out metrics, checkForCars: false, 0f, trainMomentum)
                            ? metrics.Distance
                            : float.MaxValue;
                        };
                        _log.Debug($"{getDictKey(selectedLoco)} ->  {destName} {destId} {distance?.ToString()}");
                        jumpTos.Add((
                            destinationId: destId,
                            destination: $"{t.DisplayStringLong} > {destName}"
                            , distance: distance
                            , sortdistance: sortdistance
                            , location: (Location?)destLoc
                        ));
                    } catch (Exception ex)
                    {
                        _log.Warning(ex, $"Timetable entry not added to AE gear cog options {stp}");
                    }
                }
            }
        }

        return jumpTos;
    }

    private static void IterateCarsDetectDestinations(AutoEngineerWaypointControls __instance, OptionsDropdownConfiguration __result, BaseLocomotive selectedLoco, int numberOfCars, out List<DropdownMenu.RowData> rowDatas, out Action<int> func, out int origCount, out int maxRowOrig, out AutoEngineerOrdersHelper aeoh)
    {
        rowDatas = __result.Rows.ToList();
        func = __result.OnRowSelected;
        origCount = rowDatas.Count;
        maxRowOrig = origCount - 1;
        locoConsistDestinations = [];
        HashSet<OpsCarPosition?> seen = [];
        aeoh = new AutoEngineerOrdersHelper(persistence: new AutoEngineerPersistence(selectedLoco.KeyValueObject), locomotive: selectedLoco);
        Car.LogicalEnd logicalEnd = ((selectedLoco.set.IndexOfCar(selectedLoco).GetValueOrDefault(0) >= numberOfCars / 2) ? Car.LogicalEnd.B : Car.LogicalEnd.A);
        Car.LogicalEnd end = ((logicalEnd == Car.LogicalEnd.A) ? Car.LogicalEnd.B : Car.LogicalEnd.A);
        bool stop = false;
        int carIndex = selectedLoco.set.StartIndexForConnected(selectedLoco, logicalEnd, IntegrationSet.EnumerationCondition.Coupled);
        Car car;
        string locoKey = getDictKey(selectedLoco);
        while (!stop && (car = selectedLoco.set.NextCarConnected(ref carIndex, logicalEnd, IntegrationSet.EnumerationCondition.Coupled, out stop)) != null)
        {
            AddObserversToCar(__instance, car);
            var ocp = GetCarDestinationIdentifier(car);
            _log.Debug($"{locoKey} --> {getDictKey(car)} -> {ocp.HasValue} {ocp?.DisplayName}");

            if (ocp.HasValue)
            {
                seen.Add(ocp.Value);
                locoConsistDestinations.Add(ocp.Value);
            }
        }

        var dropped =
            locoConsistDestinations.Except(seen).ToHashSet(); //are no longer here

        _log.Debug($"{locoKey} --> [{seen.Count}] -> Seen -> {string.Join(Environment.NewLine, seen.Select(k => k.Value.DisplayName))}");
        _log.Debug($"{locoKey} --> [{locoConsistDestinations.Count}] -> Cache -> {string.Join(Environment.NewLine, locoConsistDestinations.Select(k => $"{locoKey}:{k.Value.DisplayName}"))}");
        _log.Debug($"{locoKey} --> [{dropped.Count}] -> removed -> {string.Join(Environment.NewLine, dropped.Select(k => k.Value.DisplayName))}");
        locoConsistDestinations = locoConsistDestinations.Intersect(seen).ToHashSet(); //remove ones that are no longer here
        seen.Clear();
    }

    private static void PrepLocoUsage(AutoEngineerWaypointControls __instance, out BaseLocomotive selectedLoco, out int numberOfCars)
    {
        //wire up that loco
        selectedLoco = __instance.Locomotive;
        numberOfCars = selectedLoco.set.NumberOfCars;
        _log.Debug($"{getDictKey(selectedLoco)} --> HI BOB[{numberOfCars}]");
        foreach (var o in _keyChangeObservers) o.Dispose();
        _keyChangeObservers.Clear();
    }

    private static OpsCarPosition? GetCarDestinationIdentifier(Car c)
    {
        OpsCarPosition? destination = null;
        if (c.TryGetOverrideDestination(OverrideDestination.Repair, OpsController.Shared, out (OpsCarPosition, string)? result))
            destination = result.Value.Item1;

        if (!destination.HasValue && c.Waybill.HasValue && !c.Waybill.Value.Completed)
            destination = c.Waybill.Value.Destination;
        return destination;
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
        AddObserver(__instance, c, nameof(Car.trainCrewId));
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

                    _log.Debug($"{getDictKey(car)} OBSV {key}: {value}");

                    if (!(locoConsistDestinations?.Any() ?? false)) return;

                    bool waybillChng = (new[] { Car.KeyOpsWaybill, Car.KeyOpsRepairDestination }).Contains(key);
                    string? destId =
                        waybillChng ?
                        GetCarDestinationIdentifier(car)?.Identifier ?? null :
                        null;
                    if (waybillChng)
                    {
                        if (locoConsistDestinations.Contains(GetCarDestinationIdentifier(car)))
                        {
                            return;
                        }
                        else
                        {
                            _log.Debug($"{getDictKey(car)} OBSV {key}: destNew; {destId}; reload");
                        }
                    }
                    else
                    {
                        _log.Debug($"{getDictKey(car)} OBSV {key}: {value}");
                    }

                    Rebuild(__instance, car, key);
                },
                false
            )
        );
    }

    private static void Rebuild(AutoEngineerWaypointControls __instance, Car car, string key)
    {
        try
        {
            foreach (var o in _keyChangeObservers)
            {
                o.Dispose();
            }
            var loco = __instance.Locomotive;
            if (!TrainController.Shared.SelectRecall()) TrainController.Shared.SelectedCar = null;
            _keyChangeObservers.Clear();
            locoConsistDestinations = [];
            TrainController.Shared.SelectedCar = loco;
        }
        catch (Exception ex)
        {
            _log.ForContext("car", car).Warning(ex, $"{nameof(AddObserver)} {car} Exception logged for {key}");
        }
    }
}
