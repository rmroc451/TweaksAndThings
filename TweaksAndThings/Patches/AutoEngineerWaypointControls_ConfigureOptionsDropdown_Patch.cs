using Game.Messages;
using Game.State;
using HarmonyLib;
using Model;
using Model.AI;
using Model.Ops;
using Model.Ops.Timetable;
using Network;
using Network.Messages;
using Railloader;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Track;
using Track.Search;
using UI;
using UI.EngineControls;
using UnityEngine;
using UnityEngine.UI;
using static Track.Search.RouteSearch;
using Location = Track.Location;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(LocomotiveControlsUIAdapter))]
[HarmonyPatch(nameof(LocomotiveControlsUIAdapter.UpdateCarText))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class LocomotiveControlsUIAdapter_UpdateCarText_Postfix()
{
    private static Serilog.ILogger _log => Log.ForContext<LocomotiveControlsUIAdapter_UpdateCarText_Postfix>();
    private static int lastSeenIntegrationSetCount = default;
    private static string? lastLocoSeenCarId = default;
    private static Coroutine? watchyWatchy = null;
    private static HashSet<OpsCarPosition?> locoConsistDestinations = [];
    private static Game.GameDateTime? timetableSaveTime = null;
    static string getDictKey(Car car) => car.DisplayName;

    static void Postfix(LocomotiveControlsUIAdapter __instance)
    {
        try
        {

            if (lastLocoSeenCarId != null && lastLocoSeenCarId.Equals(TrainController.Shared?.SelectedLocomotive.id) && watchyWatchy != null) return;
            if (watchyWatchy != null) ((MonoBehaviour)__instance).StopCoroutine(watchyWatchy);
            watchyWatchy = null;

            if (__instance._persistence.Orders.Mode == AutoEngineerMode.Waypoint) watchyWatchy = ((MonoBehaviour)__instance).StartCoroutine(UpdateCogCoroutine(__instance));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "I have a very unique set of skills; I will find you and I will squash you.");
        }
    }

    public static IEnumerator UpdateCogCoroutine(LocomotiveControlsUIAdapter __instance)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(3f);

        while (true)
        {
            if (__instance._persistence.Orders.Mode != AutoEngineerMode.Waypoint || ((AutoEngineerWaypointControls)__instance.aiWaypointControls).Locomotive == null) yield return wait;

            PrepLocoUsage((AutoEngineerWaypointControls)__instance.aiWaypointControls, out BaseLocomotive selectedLoco, out int numberOfCars);
            HashSet<OpsCarPosition?> destinations = [];
            if (!tweaksAndThings.IsEnabled() || !ShouldRecalc(__instance, selectedLoco, out destinations)) yield return wait;
            timetableSaveTime = TimetableController.Shared.CurrentDocument.Modified;
            lastSeenIntegrationSetCount = selectedLoco.set.NumberOfCars;

            IterateCarsDetectDestinations(
                (AutoEngineerWaypointControls)__instance.aiWaypointControls,
                ((AutoEngineerWaypointControls)__instance.aiWaypointControls).ConfigureOptionsDropdown(),
                selectedLoco,
                numberOfCars,
                destinations: destinations,
                out List<DropdownMenu.RowData> rowDatas,
                out Action<int> func,
                out int origCount,
                out int maxRowOrig,
                out AutoEngineerOrdersHelper aeoh
            );

            List<(string destinationId, string destination, float? distance, float sortDistance, Location? location)> jumpTos = 
                BuildJumpToOptions((AutoEngineerWaypointControls)__instance.aiWaypointControls, selectedLoco);

            var config = WireUpJumpTosToSettingMenu(
                (AutoEngineerWaypointControls)__instance.aiWaypointControls,
                selectedLoco,
                rowDatas,
                func,
                origCount,
                maxRowOrig,
                aeoh,
                ref jumpTos
            );

            List<DropdownMenu.RowData> list = config.Rows;
            Action<int> action = config.OnRowSelected;

            __instance.optionsDropdown.Configure(list, action);
            ((Selectable)__instance.optionsDropdown).interactable = list.Count > 0;
            yield return wait;
        }
    }

    private static bool ShouldRecalc(LocomotiveControlsUIAdapter __instance, BaseLocomotive selectedLoco, out HashSet<OpsCarPosition?> destinations)
    {
        bool output = false;
        string locoKey = getDictKey(selectedLoco);
        List<Car> consist = new List<Car>();
        consist = selectedLoco.EnumerateCoupled().ToList();
        destinations = consist.Where(c => GetCarDestinationIdentifier(c).HasValue).Select(GetCarDestinationIdentifier).ToHashSet();

        //_log.Information($"{locoKey} --> [{destinations.Count}] -> Seen -> {string.Join(Environment.NewLine, destinations.Select(k => k.Value.DisplayName))}");
        //_log.Information($"{locoKey} --> [{locoConsistDestinations.Count}] -> Cache -> {string.Join(Environment.NewLine, locoConsistDestinations.Select(k => $"{locoKey}:{k.Value.DisplayName}"))}");

        output |= !locoConsistDestinations.SetEquals(destinations);
        //_log.Information($"{locoKey} 1-> {output}");
        if (output) lastSeenIntegrationSetCount = default;
        output |= lastSeenIntegrationSetCount != selectedLoco.set.NumberOfCars;
        //_log.Information($"{locoKey} 2-> {output}");
        //output |= __instance.optionsDropdown.scrollRect.content.childCount != (destinations.Count + timetableDestinations.Count + 1); //+1 for the default "JumpTo" entry)
        //_log.Information($"{locoKey} 2.5-> {output} {__instance.optionsDropdown.scrollRect.content.childCount} {(destinations.Count)} {timetableDestinations.Count}");
        output |= selectedLoco.TryGetTimetableTrain(out _) && TimetableController.Shared.CurrentDocument.Modified != timetableSaveTime;
        //_log.Information($"{locoKey} 3-> {output}");

        return output;
    }

    private static OptionsDropdownConfiguration WireUpJumpTosToSettingMenu(AutoEngineerWaypointControls __instance, BaseLocomotive selectedLoco, List<DropdownMenu.RowData> rowDatas, Action<int> func, int origCount, int maxRowOrig, AutoEngineerOrdersHelper aeoh, ref List<(string destinationId, string destination, float? distance, float sortDistance, Location? location)> jumpTos)
    {
        OptionsDropdownConfiguration __result;
        jumpTos = jumpTos?.OrderBy(c => c.sortDistance)?.ToList() ?? default;
        var localJumpTos = jumpTos.ToList();
        var safetyFirst = AutoEngineerPlanner_HandleCommand_Patch.SafetyFirstGoverningApplies(selectedLoco) && jumpTos.Any();

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
            }
            ;
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
            //_log.Information($"{getDictKey(selectedLoco)} -> {t.DisplayStringLong}");
            foreach (var e in t.Entries)
            {
                var stp = TimetableController.Shared.GetAllStations().FirstOrDefault(ps => ps.code == e.Station);
                //_log.Information($"{getDictKey(selectedLoco)} -> {t.DisplayStringLong} -> {e.Station} {stp}");
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
                        }
                        ;
                        _log.Debug($"{getDictKey(selectedLoco)} ->  {destName} {destId} {distance?.ToString()}");
                        jumpTos.Add((
                            destinationId: destId,
                            destination: $"{t.DisplayStringLong} > {destName}"
                            , distance: distance
                            , sortdistance: sortdistance
                            , location: (Location?)destLoc
                        ));
                    }
                    catch (Exception ex)
                    {
                        _log.Warning(ex, $"Timetable entry not added to AE gear cog options {stp}");
                    }
                }
            }
        }

        return jumpTos;
    }

    private static void IterateCarsDetectDestinations(
        AutoEngineerWaypointControls __instance, 
        OptionsDropdownConfiguration __result, 
        BaseLocomotive selectedLoco,
        int numberOfCars, 
        HashSet<OpsCarPosition?> destinations, 
        out List<DropdownMenu.RowData> rowDatas,
        out Action<int> func, 
        out int origCount, 
        out int maxRowOrig, 
        out AutoEngineerOrdersHelper aeoh
    )
    {
        rowDatas = __result.Rows.ToList();
        func = __result.OnRowSelected;
        origCount = rowDatas.Count;
        maxRowOrig = origCount - 1;
        aeoh = new AutoEngineerOrdersHelper(persistence: new AutoEngineerPersistence(selectedLoco.KeyValueObject), locomotive: selectedLoco);        
        string locoKey = getDictKey(selectedLoco);

        var dropped =
            locoConsistDestinations.Except(destinations).ToHashSet(); //are no longer here

        _log.Debug($"{locoKey} --> [{destinations.Count}] -> Seen -> {string.Join(Environment.NewLine, destinations.Select(k => k.Value.DisplayName))}");
        _log.Debug($"{locoKey} --> [{locoConsistDestinations.Count}] -> Cache -> {string.Join(Environment.NewLine, locoConsistDestinations.Select(k => $"{locoKey}:{k.Value.DisplayName}"))}");
        _log.Debug($"{locoKey} --> [{dropped.Count}] -> removed -> {string.Join(Environment.NewLine, dropped.Select(k => k.Value.DisplayName))}");
        locoConsistDestinations = destinations.ToList().ToHashSet(); //remove ones that are no longer here
    }

    private static void PrepLocoUsage(AutoEngineerWaypointControls __instance, out BaseLocomotive selectedLoco, out int numberOfCars)
    {
        //wire up that loco
        selectedLoco = __instance.Locomotive;
        numberOfCars = selectedLoco?.set.NumberOfCars ?? -1;
        _log.Debug($"{selectedLoco?.id} --> HI BOB[{numberOfCars}]");
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

    private static Location RouteStartLocation(AutoEngineerWaypointControls __instance, BaseLocomotive _locomotive)
    {
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
}

[HarmonyPatch(typeof(LocomotiveControlsUIAdapter))]
[HarmonyPatch(nameof(LocomotiveControlsUIAdapter.UpdateOptionsDropdown))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class LocomotiveControlsUIAdapter_UpdateOptionsDropdown_Prefix
{
    static bool Prefix(LocomotiveControlsUIAdapter __instance)
    {
        return false;
    }
}
