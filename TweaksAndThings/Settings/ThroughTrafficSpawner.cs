using Game.Messages;
using Game.State;
using KeyValue.Runtime;
using Model;
using Model.AI;
using Model.Ops;
using Model.Ops.Timetable;
using Network;
using RollingStock;
using System;
using System.Collections.Generic;
using System.Linq;
using Track;
using Track.Search;
using Track.Signals;
using UI.EngineControls;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

/// <summary>Host-side timetable scanner and consist generator for marked first-class trains.</summary>
internal static class ThroughTrafficSpawner
{
    private const float ServiceSpeedMph = 20f;
    private static readonly HashSet<string> spawnedServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static int lastSeenDay = int.MinValue;
    private static float tickRemainder;

    internal static void Reset()
    {
        spawnedServices.Clear();
        lastSeenDay = int.MinValue;
        tickRemainder = 0f;
    }

    internal static void Tick(Settings settings, float deltaTime)
    {
        if (!settings.ThroughTrafficEnabled || !StateManager.IsHost) return;
        tickRemainder += deltaTime;
        if (tickRemainder < 1f) return;
        tickRemainder = 0f;

        var now = StateManager.Now;
        if (lastSeenDay != now.Day)
        {
            spawnedServices.Clear();
            lastSeenDay = now.Day;
        }

        var timetable = TimetableController.Shared?.Current;
        if (timetable?.Trains == null) return;
        int minuteOfDay = now.Hours * 60 + now.Minutes;

        foreach (var train in timetable.Trains)
        {
            if (!ThroughTrafficPolicy.IsMarkedTrain(train.Name, settings.ThroughTrafficTrainSymbolPrefix) ||
                train.TrainClass != Timetable.TrainClass.First || train.Entries == null || train.Entries.Count < 2)
                continue;

            int scheduledDeparture = DepartureMinute(train);
            if (scheduledDeparture < 0 || !ThroughTrafficPolicy.IsDepartureDue(minuteOfDay, scheduledDeparture, 0))
                continue;

            string serviceKey = $"{now.Day}:{train.Name}";
            if (spawnedServices.Contains(serviceKey)) continue;

            try
            {
                if (TrySpawn(train, settings)) spawnedServices.Add(serviceKey);
            }
            catch (Exception ex)
            {
                TweaksAndThingsPlugin.LogException($"Unable to generate through traffic for {train.Name}", ex);
            }
        }
    }

    private static int DepartureMinute(Timetable.Train train)
    {
        return train.TryGetAbsoluteTimeForEntry(0, TimetableTimeType.Departure, out int minutes)
            ? ((minutes % 1440) + 1440) % 1440
            : -1;
    }

    private static bool TrySpawn(Timetable.Train train, Settings settings)
    {
        if (!TimetableController.Shared.TryGetStation(train.Entries[0].Station, out var origin) ||
            !TimetableController.Shared.TryGetStation(train.Entries[train.Entries.Count - 1].Station, out var destination) ||
            !IsInterchange(origin) || !IsInterchange(destination))
            return false;

        var stations = new List<TimetableStation>();
        foreach (var entry in train.Entries)
        {
            if (!TimetableController.Shared.TryGetStation(entry.Station, out var station) || !station.IsEnabled)
                return false;
            stations.Add(station);
        }

        var locations = new List<Location>();
        foreach (var station in stations)
        {
            if (!TryGetStationLocation(station, out var location)) return false;
            locations.Add(location);
        }

        var routeSteps = new List<RouteSearch.Step>();
        for (int i = 0; i < locations.Count - 1; i++)
        {
            var leg = new List<RouteSearch.Step>();
            if (!Graph.Shared.FindRoute(locations[i], locations[i + 1], HeuristicCosts.AutoEngineer, leg, out _, checkForCars: false) || leg.Count == 0)
                return false;
            routeSteps.AddRange(leg);
        }

        // Fail closed unless CTC is active and every step between interchanges is in a CTC block.
        var ctc = CTCPanelController.Shared;
        if (ctc == null || string.Equals(ctc.SystemMode.ToString(), "Off", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ctc.SystemMode.ToString(), "None", StringComparison.OrdinalIgnoreCase)) return false;
        var blocks = ctc.AllBlocks?.Values.Where(block => block.IsCTC).ToList();
        if (blocks == null || blocks.Count == 0 || routeSteps.Any(step => !blocks.Any(block => block.Contains(step.Position))))
            return false;

        var trainController = TrainController.Shared;
        var locomotives = trainController.Cars.OfType<BaseLocomotive>()
            .Where(loco => loco.RatedTractiveEffort > 0 && loco.Weight > 0)
            .OrderByDescending(loco => loco.RatedTractiveEffort / loco.Weight)
            .ToList();
        if (locomotives.Count == 0) return false;

        var consistDescriptors = new List<CarDescriptor>();
        var consistIds = new List<string>();
        string crewId = EnsureTrainCrew(train);
        BaseLocomotive chosenLoco = locomotives[0];

        if (train.TrainType == Timetable.TrainType.Passenger)
        {
            if (!TryBuildPassengerCars(train, stations, chosenLoco, crewId, settings, consistDescriptors, consistIds))
                return false;
        }
        else
        {
            var freightTemplate = trainController.Cars.FirstOrDefault(car => !car.IsLocomotive &&
                !stations.Any(station => station.passengerStop != null && station.passengerStop.IsPassengerCar(car)));
            if (freightTemplate == null) return false;
            for (int i = 0; i < 6; i++)
                AddClonedCar(freightTemplate, crewId, null, consistDescriptors, consistIds);
        }

        float gradePercent = routeSteps.Max(step => Math.Abs(Graph.Shared.GradeAtLocation(step.Location))) * 100f;
        if ((float.IsNaN(gradePercent) || float.IsInfinity(gradePercent))) return false;
        if (gradePercent > 100f) gradePercent = 100f;

        float carTonnage = 0f;
        foreach (var descriptor in consistDescriptors)
        {
            var template = trainController.Cars.FirstOrDefault(car => car.DefinitionInfo == descriptor.DefinitionInfo);
            if (template != null) carTonnage += template.Weight;
        }
        if (carTonnage <= 0) return false;

        int powerUnits = 1;
        double requiredHpPerTonne = ThroughTrafficPolicy.HorsepowerPerTonneForGrade(gradePercent, ServiceSpeedMph);
        double hpPerTonne = 0;
        for (; powerUnits <= 4; powerUnits++)
        {
            float totalTonnage = carTonnage + chosenLoco.Weight * powerUnits;
            hpPerTonne = chosenLoco.RatedTractiveEffort * powerUnits * ServiceSpeedMph / 375d / totalTonnage;
            if (hpPerTonne >= requiredHpPerTonne) break;
        }
        if (powerUnits > 4) return false;

        for (int i = 0; i < powerUnits; i++)
            AddClonedCar(chosenLoco, crewId, null, consistDescriptors, consistIds);

        // PlaceTrain expects the locomotive at the head of the descriptor list.
        var firstLocoIndex = consistDescriptors.FindIndex(descriptor => descriptor.DefinitionInfo == chosenLoco.DefinitionInfo);
        for (int i = 0; i < powerUnits && firstLocoIndex > 0; i++)
        {
            var descriptor = consistDescriptors[firstLocoIndex];
            var id = consistIds[firstLocoIndex];
            consistDescriptors.RemoveAt(firstLocoIndex);
            consistIds.RemoveAt(firstLocoIndex);
            consistDescriptors.Insert(i, descriptor);
            consistIds.Insert(i, id);
        }

        ThroughTrafficGuard.BeginTrustedMutation();
        try
        {
            trainController.PlaceTrain(locations[0], consistDescriptors, consistIds, 1f, PlaceTrainHandbrakes.Automatic);
        }
        finally
        {
            ThroughTrafficGuard.EndTrustedMutation();
        }

        var spawnedLoco = trainController.Cars.FirstOrDefault(car => car.id == consistIds[0]) as BaseLocomotive;
        if (spawnedLoco == null) return true;
        var orders = new AutoEngineerOrdersHelper(spawnedLoco, new AutoEngineerPersistence(spawnedLoco.KeyValueObject));
        orders.SetWaypoint(locations[locations.Count - 1], stations[stations.Count - 1].code);
        ThroughTrafficGuard.BeginTrustedMutation();
        try { orders.SendAutoEngineerCommand(AutoEngineerMode.Waypoint, true, ServiceSpeedMph, null, null); }
        finally { ThroughTrafficGuard.EndTrustedMutation(); }
        return true;
    }

    private static bool TryBuildPassengerCars(Timetable.Train train, List<TimetableStation> stations,
        BaseLocomotive locomotive, string crewId, Settings settings, List<CarDescriptor> descriptors, List<string> ids)
    {
        var passengerStops = stations.Where(station => station.passengerStop != null).Select(station => station.passengerStop).ToList();
        if (passengerStops.Count < 2) return false;

        var passengerTemplate = TrainController.Shared.Cars.FirstOrDefault(car => passengerStops.Any(stop => stop.IsPassengerCar(car)));
        if (passengerTemplate == null) return false;
        int capacity = passengerStops[0].PassengerCapacity(passengerTemplate);
        if (capacity <= 0) return false;

        int routeDemand = 0;
        for (int i = 0; i < passengerStops.Count - 1; i++)
            for (int j = i + 1; j < passengerStops.Count; j++)
                routeDemand += Math.Max(0, passengerStops[i].GetTotalWaitingForDestination(passengerStops[j].identifier));

        // Randomized through load scales with observed route demand. With no waiting pool, run a lightly loaded one-car service.
        int passengerCount = routeDemand > 0
            ? UnityEngine.Random.Range(Math.Max(1, routeDemand / 2), routeDemand + 1)
            : UnityEngine.Random.Range(Math.Max(1, capacity / 4), Math.Max(2, capacity * 3 / 4));
        int carCount = Math.Max(1, ThroughTrafficPolicy.PassengerCarsForDemand(passengerCount, capacity));
        if (carCount > 20) carCount = 20;

        var marker = PassengerMarker.Empty();
        int left = passengerCount;
        var origin = stations[0].code;
        for (int i = 1; i < stations.Count && left > 0; i++)
        {
            var stop = stations[i].passengerStop;
            if (stop == null) continue;
            int targetCount = i == stations.Count - 1 ? left : UnityEngine.Random.Range(0, left + 1);
            if (targetCount > 0)
            {
                marker.AddPassengers(origin, stop.identifier, targetCount, StateManager.Now);
                left -= targetCount;
            }
        }
        if (left > 0) marker.AddPassengers(origin, passengerStops[passengerStops.Count - 1].identifier, left, StateManager.Now);

        for (int i = 0; i < carCount; i++)
            AddClonedCar(passengerTemplate, crewId, i == 0 ? marker : null, descriptors, ids);
        return true;
    }

    private static void AddClonedCar(Car template, string crewId, PassengerMarker? marker,
        List<CarDescriptor> descriptors, List<string> ids)
    {
        var source = template.Descriptor();
        var properties = new Dictionary<string, Value>(source.Properties);
        properties.Remove(Car.KeyOwned);
        properties.Remove(Car.KeyOpsWaybill);
        properties.Remove(Car.KeyOpsPassengerMarker);
        properties.Remove(Car.KeyOpsRepairDestination);
        if (marker != null) properties[Car.KeyOpsPassengerMarker] = marker.PropertyValue();
        properties[ThroughTrafficGuard.MarkerKey] = Value.Bool(true);

        var ident = new CarIdent(source.Ident.ReportingMark, "AI" + Guid.NewGuid().ToString("N").Substring(0, 6));
        descriptors.Add(new CarDescriptor(source.DefinitionInfo, ident, string.Empty, crewId, source.Flipped, properties));
        ids.Add(Guid.NewGuid().ToString("N"));
    }

    private static string EnsureTrainCrew(Timetable.Train train)
    {
        var manager = StateManager.Shared.PlayersManager;
        var existing = manager.TrainCrews.FirstOrDefault(crew => string.Equals(crew.TimetableSymbol, train.Name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing.Id;

        string id = Guid.NewGuid().ToString("N");
        var crew = new TrainCrew(id, "AI " + train.Name, new List<string>(), "Automated through traffic", train.Name);
        manager.HandleRequestCreateTrainCrew(new RequestCreateTrainCrew(crew), manager.LocalPlayer);
        return crew.Id;
    }

    private static bool IsInterchange(TimetableStation station)
    {
        var interchanges = OpsController.Shared?.AllInterchanges;
        if (interchanges == null) return false;
        string featureName = station.mapFeature?.GetType().GetProperty("name")?.GetValue(station.mapFeature, null)?.ToString() ?? string.Empty;
        return interchanges.Any(interchange =>
            string.Equals(interchange.name, station.code, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(interchange.name, station.name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(interchange.name, featureName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetStationLocation(TimetableStation station, out Location location)
    {
        location = default;
        var stop = station.passengerStop;
        if (stop != null && stop.TrackSpans != null)
        {
            var span = stop.TrackSpans.FirstOrDefault();
            if (span != null && Graph.Shared.TryGetLocationFromPoint(
                span.GetSegments().FirstOrDefault(), span.GetCenterPoint(), 200f, out location)) return true;
        }

        // Freight timetable entries can refer to map features without a passenger stop.
        var feature = station.mapFeature;
        if (feature == null) return false;
        var property = feature.GetType().GetProperty("CenterPoint");
        if (property?.GetValue(feature, null) is Vector3 point)
            return Graph.Shared.TryGetLocationFromGamePoint(point, 200f, out location);
        return false;
    }
}
