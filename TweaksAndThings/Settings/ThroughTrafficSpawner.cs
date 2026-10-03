using Game;
using Game.Messages;
using Game.State;
using KeyValue.Runtime;
using Model;
using Model.Definition;
using Model.Ops;
using Model.Ops.Timetable;
using System;
using System.Collections.Generic;
using System.Linq;
using Track;
using Track.Search;

namespace RMROC451.TweaksAndThings;

/// <summary>Persistent host-side timetable services, with an explicit order for every scheduled stop.</summary>
internal static class ThroughTrafficSpawner
{
    private static float tickRemainder;
    internal static void Reset() => tickRemainder = 0;

    internal static void Tick(Settings settings, float deltaTime)
    {
        if (!StateManager.IsHost || TrainController.Shared == null || TimetableController.Shared == null || !NpcServiceStore.Load()) return;
        tickRemainder += deltaTime;
        if (tickRemainder < 0.5f) return;
        tickRemainder = 0;
        double now = TimeWeather.Now.TotalSeconds;
        NpcTimetableRegistry.Tick();
        NpcRandomThroughFreights.Tick(settings, now);
        var state = NpcServiceStore.State;
        foreach (var service in state.Services.Where(s => s.TrainSymbol.Length > 0 && s.InterchangeId.Length == 0).ToList())
        {
            try { Advance(service, now); }
            catch (Exception ex) { TweaksAndThingsPlugin.LogException("Through service " + service.TrainSymbol, ex); }
        }
        double previous = state.LastScan < 0 || now < state.LastScan ? now - 60 : state.LastScan;
        var timetable = TimetableController.Shared.Current;
        if (settings.ThroughTrafficEnabled && timetable?.Trains != null)
        {
            int firstDay = Math.Max(0, (int)Math.Floor(previous / 86400));
            int lastDay = (int)Math.Floor(now / 86400);
            foreach (var train in timetable.Trains.Values.ToList())
            {
                if (NpcTimetableRegistry.IsProtected(train.Name) || !ThroughTrafficPolicy.IsMarkedTrain(train.Name, settings.ThroughTrafficTrainSymbolPrefix) ||
                    train.TrainClass != Timetable.TrainClass.First || train.Entries.Count < 2 ||
                    !train.TryGetAbsoluteTimeForEntry(0, TimetableTimeType.Departure, out int departure)) continue;
                for (int day = firstDay; day <= lastDay; day++)
                {
                    double scheduled = day * 86400d + ((departure % 1440 + 1440) % 1440) * 60;
                    string key = day + ":" + train.Name;
                    if (!NpcServicePolicy.Crossed(previous, now, scheduled) || state.ThroughDepartures.ContainsKey(key)) continue;
                    try
                    {
                        state.ThroughDepartures[key] = scheduled;
                        TrySpawn(train, settings, scheduled);
                    }
                    catch (Exception ex) { TweaksAndThingsPlugin.LogException("Unable to generate through traffic for " + train.Name, ex); }
                }
            }
        }
        state.LastScan = now;
        foreach (string key in state.ThroughDepartures.Where(p => p.Value < now - 172800).Select(p => p.Key).ToList())
            state.ThroughDepartures.Remove(key);
        NpcServiceStore.Save();
    }

    private static bool TrySpawn(Timetable.Train train, Settings settings, double scheduled, bool randomFreight = false, IReadOnlyList<string>? plannedFreight = null)
    {
        var stations = new List<TimetableStation>();
        var locations = new List<Location>();
        var operating = Enumerable.Range(0, train.Entries.Count).Where(i => !randomFreight || i == 0 || i == train.Entries.Count - 1 ||
            !train.Entries[i].HasSingleArrivalAndDeparture).ToList();
        foreach (int index in operating)
        {
            var entry = train.Entries[index];
            if (!TimetableController.Shared.TryGetStation(entry.Station, out var station) || !station.IsEnabled ||
                !TryGetStationLocation(station, out var location)) return false;
            stations.Add(station);
            locations.Add(location);
        }
        if (!IsInterchange(stations[0]) || !IsInterchange(stations.Last())) return false;
        int firstStop = operating[1];
        var steps = new List<RouteSearch.Step>();
        for (int i = 1; i < locations.Count; i++)
        {
            if (!NpcTrainOperations.Route(locations[i - 1], locations[i], out var leg, out _)) return false;
            steps.AddRange(leg);
        }
        string? crewId = null;
        var freight = new List<CarDescriptor>();
        var freightWeights = new List<float>();
        float weight;
        if (train.TrainType == Timetable.TrainType.Passenger)
        {
            if (!BuildPassengers(stations, crewId!, freight, out weight)) return false;
            freightWeights.AddRange(freight.Select(_ => weight / freight.Count));
        }
        else if (randomFreight)
        {
            var catalog = TrainController.Shared.PrefabStore.AllCarDefinitionInfos.Where(i => i.Definition.VisibleInPlacer && i.Definition.Archetype.IsFreight()).ToList();
            if (catalog.Count == 0) return false;
            int minimum = Math.Max(1, Math.Min(50, settings.RandomThroughFreightMinCars));
            int maximum = Math.Max(minimum, Math.Min(50, settings.RandomThroughFreightMaxCars));
            int count = plannedFreight?.Count > 0 ? plannedFreight.Count : UnityEngine.Random.Range(minimum, maximum + 1);
            weight = 0;
            for (int i = 0; i < count; i++)
            {
                var info = plannedFreight?.Count > 0 ? catalog.FirstOrDefault(c => c.Identifier == plannedFreight[i]) : catalog[UnityEngine.Random.Range(0, catalog.Count)];
                if (info == null)
                { NpcTrafficDiagnostics.Detail("catalog:" + train.Name, $"{train.Name}: saved freight definition {plannedFreight![i]} is unavailable; dispatch retained for retry."); return false; }
                weight += info.Definition.WeightEmpty;
                freightWeights.Add(info.Definition.WeightEmpty);
                freight.Add(new CarDescriptor(info, new CarIdent("NPC", null), string.Empty, null, false,
                    new Dictionary<string, Value> { [ThroughTrafficGuard.MarkerKey] = Value.Bool(true) }));
            }
        }
        else
        {
            var reference = string.IsNullOrWhiteSpace(settings.ThroughFreightReferenceCar)
                ? TrainController.Shared.Cars.FirstOrDefault(c => c.Archetype.IsFreight() && !ThroughTrafficGuard.IsGenerated(c))
                : TrainController.Shared.CarForString(settings.ThroughFreightReferenceCar);
            if (reference == null) return false;
            var cars = reference.EnumerateCoupled().Where(c => c.Archetype.IsFreight() && !ThroughTrafficGuard.IsGenerated(c)).ToList();
            if (cars.Count == 0) return false;
            weight = cars.Sum(c => c.Weight);
            freightWeights.AddRange(cars.Select(c => c.Weight));
            freight.AddRange(cars.Select(c => NpcTrainOperations.Clone(c, crewId)));
        }
        float sidingLimit = NpcPassingSidings.Limit(steps);
        if (sidingLimit <= 0)
        { NpcTrafficDiagnostics.Detail("siding:" + train.Name, $"{train.Name}: dispatch blocked; no usable passing siding on route."); return false; }
        List<CarDescriptor> descriptors;
        while (true)
        {
            if (!NpcTrainOperations.TryCatalogPower(weight, steps, crewId, out descriptors,
                trainLength: NpcPassingSidings.Length(freight) + 60)) return false;
            if (NpcPassingSidings.Length(descriptors.Concat(freight)) <= sidingLimit) break;
            if (freight.Count == 0) return false;
            int remove = UnityEngine.Random.Range(0, freight.Count);
            weight = Math.Max(0, weight - freightWeights[remove]);
            freight.RemoveAt(remove);
            freightWeights.RemoveAt(remove);
            if (freight.Count == 0) return false;
        }
        ModDiagnosticLog.Write("SIDING", $"{train.Name}: full train {NpcPassingSidings.Length(descriptors.Concat(freight)):N0} m; passing siding limit {sidingLimit:N0} m.");
        var spawn = NpcTrainOperations.FacingRoute(locations[0], locations[1]);
        if (!NpcDeparturePlacement.Find(ref spawn, locations[1], NpcPassingSidings.Length(descriptors.Concat(freight)))) return false;
        // Moving the departure point can remove a siding from the route or
        // choose another approach. Recheck both length and power on the actual trip.
        if (!NpcTrainOperations.Route(spawn, locations[1], out var actualSteps, out _)) return false;
        for (int i = 2; i < locations.Count; i++)
        {
            if (!NpcTrainOperations.Route(locations[i - 1], locations[i], out var leg, out _)) return false;
            actualSteps.AddRange(leg);
        }
        sidingLimit = NpcPassingSidings.Limit(actualSteps);
        if (sidingLimit <= 0) return false;
        while (true)
        {
            if (!NpcTrainOperations.TryCatalogPower(weight, actualSteps, crewId, out descriptors,
                trainLength: NpcPassingSidings.Length(freight) + 60)) return false;
            if (NpcPassingSidings.Length(descriptors.Concat(freight)) <= sidingLimit) break;
            if (freight.Count <= 1) return false;
            int remove = UnityEngine.Random.Range(0, freight.Count);
            weight = Math.Max(0, weight - freightWeights[remove]);
            freight.RemoveAt(remove);
            freightWeights.RemoveAt(remove);
        }
        descriptors.AddRange(freight);
        if (!NpcTrainOperations.CanPlaceAt(spawn, NpcPassingSidings.Length(descriptors) + 20)) return false;
        if (!NpcTrafficRouting.MaySpawn(spawn, locations.Last(), NpcPassingSidings.Length(descriptors) + 20)) return false;
        var ids = descriptors.Select(_ => Guid.NewGuid().ToString("N")).ToList();
        crewId = NpcTimetableRegistry.EnsureCrew(train.Name);
        descriptors = descriptors.Select(d => new CarDescriptor(d.DefinitionInfo, d.Ident, d.Bardo, crewId, d.Flipped, d.Properties)).ToList();
        try { NpcTrainOperations.PlacePower(spawn, descriptors, ids); }
        catch { NpcTimetableRegistry.Remove(new NpcServiceRecord { TrainSymbol = train.Name }); throw; }
        if (!TrainController.Shared.TryGetCarForId(ids[0], out var lead) || !(lead is BaseLocomotive loco)) return false;
        RMROC451.TweaksAndThings.Patches.InterchangeTimewarp_Patch.CancelForSpawn(train.Name);
        var service = new NpcServiceRecord
        {
            Id = Guid.NewGuid().ToString("N"), TrainSymbol = train.Name, LeadId = ids[0], PowerIds = ids,
            Scheduled = scheduled, Spawn = Graph.Shared.LocationToString(spawn), Target = Graph.Shared.LocationToString(locations[1]), RandomFreight = randomFreight, CrewId = crewId, StopIndex = firstStop
        };
        NpcServiceStore.State.Services.Add(service);
        NpcTimetableRegistry.Register(service, train);
        NpcTrainOperations.Order(loco, locations[1]);
        NpcTrafficMessages.Send(service, "depart-1", $"On the move from {stations[0].name} toward {stations[1].name}.");
        return true;
    }

    private static bool BuildPassengers(List<TimetableStation> stations, string crewId, List<CarDescriptor> descriptors, out float weight)
    {
        weight = 0;
        var stops = stations.Where(s => s.passengerStop != null).Select(s => s.passengerStop).ToList();
        if (stops.Count < 2) return false;
        var template = TrainController.Shared.Cars.FirstOrDefault(c => !ThroughTrafficGuard.IsGenerated(c) && stops.Any(s => s.IsPassengerCar(c)));
        if (template == null) return false;
        int capacity = stops[0].PassengerCapacity(template);
        if (capacity <= 0) return false;
        int demand = 0;
        for (int i = 0; i < stops.Count - 1; i++)
            for (int j = i + 1; j < stops.Count; j++) demand += Math.Max(0, stops[i].GetTotalWaitingForDestination(stops[j].identifier));
        int passengers = demand > 0 ? UnityEngine.Random.Range(Math.Max(1, demand / 2), demand + 1) :
            UnityEngine.Random.Range(Math.Max(1, capacity / 4), Math.Max(2, capacity * 3 / 4));
        passengers = Math.Min(passengers, capacity * 20);
        int carCount = ThroughTrafficPolicy.PassengerCarsForDemand(passengers, capacity);
        for (int i = 0; i < carCount; i++)
        {
            var marker = PassengerMarker.Empty();
            marker.Destinations = new HashSet<string>(stops.Skip(1).Select(s => s.identifier));
            int remaining = Math.Min(capacity, passengers);
            passengers -= remaining;
            for (int j = 1; j < stops.Count; j++)
            {
                int assigned = j == stops.Count - 1 ? remaining : UnityEngine.Random.Range(0, remaining + 1);
                if (assigned > 0) marker.AddPassengers(stops[0].identifier, stops[j].identifier, assigned, TimeWeather.Now);
                remaining -= assigned;
            }
            var descriptor = NpcTrainOperations.Clone(template, crewId);
            var properties = new Dictionary<string, Value>(descriptor.Properties) { [Car.KeyOpsPassengerMarker] = marker.PropertyValue() };
            descriptors.Add(new CarDescriptor(descriptor.DefinitionInfo, descriptor.Ident, descriptor.Bardo, crewId, descriptor.Flipped, properties));
        }
        weight = carCount * (template.Weight + capacity * 180f);
        return true;
    }

    private static void Advance(NpcServiceRecord service, double now)
    {
        if (!TrainController.Shared.TryGetCarForId(service.LeadId, out var car) || !(car is BaseLocomotive lead)) return;
        var train = TimetableController.Shared.Current?.Trains?.Values.FirstOrDefault(t => t.Name == service.TrainSymbol);
        if (train == null && NpcServiceStore.State.ProtectedTimetables.TryGetValue(service.TrainSymbol, out var source) &&
            TimetableController.Shared.TryRead(source, out var saved, null))
            train = saved.ToAbsolute().Trains.Values.FirstOrDefault();
        if (train == null) return;
        if (service.StopIndex >= train.Entries.Count) { Finish(service); return; }
        if (!TimetableController.Shared.TryGetStation(train.Entries[service.StopIndex].Station, out var station) ||
            !TryGetStationLocation(station, out var target)) return;
        if (service.Suspended)
        {
            if (!NpcDeparturePlacement.FaceDeparture(service, target))
            { service.WaitingReason = "Clear track to turn NPC power toward its destination"; return; }
            service.Suspended = false;
            NpcTrainOperations.Order(lead, target);
        }
        NpcTrafficMessages.Approaching(service, lead, target, station.name, "approach-" + service.StopIndex);
        if (!NpcTrainOperations.Arrived(lead, target)) return;
        if (service.Phase == "Approaching")
        {
            service.Phase = "At stop";
            service.NextTransfer = now;
            NpcTrainOperations.Order(lead, target, false);
        }
        if (station.passengerStop != null)
        {
            var passengers = lead.EnumerateCoupled().Where(c => station.passengerStop.IsPassengerCar(c)).ToList();
            int budget = Math.Min(10000, Math.Max(0, (int)(now - service.NextTransfer)));
            for (int i = 0; i < budget; i++)
            {
                bool unloaded = false;
                NpcTrainOperations.Trusted(() =>
                {
                    foreach (var coach in passengers)
                        if ((coach.GetPassengerMarker()?.CountPassengersForStop(station.passengerStop.identifier) ?? 0) > 0)
                            unloaded |= station.passengerStop.UnloadCar(coach);
                });
                if (!unloaded) break;
            }
            service.NextTransfer += budget;
            if (passengers.Any(c => (c.GetPassengerMarker()?.CountPassengersForStop(station.passengerStop.identifier) ?? 0) > 0)) return;
        }
        if (now < ScheduledTime(service, train, service.StopIndex)) return;
        service.StopIndex++;
        if (service.RandomFreight)
            while (service.StopIndex < train.Entries.Count - 1 && train.Entries[service.StopIndex].HasSingleArrivalAndDeparture) service.StopIndex++;
        service.Phase = "Approaching";
        if (service.StopIndex >= train.Entries.Count) { Finish(service); return; }
        if (TimetableController.Shared.TryGetStation(train.Entries[service.StopIndex].Station, out var next) && TryGetStationLocation(next, out var nextLocation))
        {
            service.Target = Graph.Shared.LocationToString(nextLocation);
            if (!NpcDeparturePlacement.FaceDeparture(service, nextLocation))
            { service.Suspended = true; service.WaitingReason = "Clear track to turn NPC power toward its next destination"; return; }
            NpcTrainOperations.Order(lead, nextLocation);
            NpcTrafficMessages.Send(service, "depart-" + service.StopIndex, $"Departing {station.name}; on the move toward {next.name}.");
        }
    }

    private static double ScheduledTime(NpcServiceRecord service, Timetable.Train train, int index)
    {
        double dayStart = Math.Floor(service.Scheduled / 86400) * 86400;
        int last = (int)((service.Scheduled - dayStart) / 60), dayOffset = 0;
        for (int i = 1; i <= index; i++)
        {
            if (!train.TryGetAbsoluteTimeForEntry(i, TimetableTimeType.Departure, out int minute)) continue;
            if (minute < last) dayOffset += 1440;
            last = minute;
        }
        return dayStart + (last + dayOffset) * 60;
    }

    private static void Finish(NpcServiceRecord service)
    {
        NpcTrafficMessages.Send(service, "complete", "Journey complete; traffic leaving the railroad.");
        NpcTrainOperations.Trusted(() =>
        {
            foreach (string id in service.PowerIds)
                if (TrainController.Shared.TryGetCarForId(id, out _)) TrainController.Shared.RemoveCarSmart(id);
        });
        NpcServiceStore.State.Services.Remove(service);
        NpcTimetableRegistry.Remove(service);
    }

    private static string EnsureTrainCrew(Timetable.Train train)
    {
        var manager = StateManager.Shared.PlayersManager;
        var existing = manager.TrainCrews.FirstOrDefault(c => string.Equals(c.TimetableSymbol, train.Name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing.Id;
        var crew = new Snapshot.TrainCrew { Id = Guid.NewGuid().ToString("N"), Name = "AI " + train.Name,
            MemberPlayerIds = new HashSet<string>(), Description = "Automated through traffic", TimetableSymbol = train.Name };
        manager.HandleRequestCreateTrainCrew(manager.LocalPlayer, crew);
        return crew.Id;
    }

    private static bool MatchesInterchange(TimetableStation station, Interchange interchange)
    {
        string Normalize(string? name) => new string((name ?? "").ToUpperInvariant().Replace("INTERCHANGE", "").Where(char.IsLetterOrDigit).ToArray());
        var names = new[] { interchange.name, interchange.Industry.name, interchange.Industry.identifier }.Select(Normalize).Where(n => n.Length > 0).ToHashSet();
        return new[] { station.code, station.name, station.mapFeature?.name, station.mapFeature?.DisplayName }.Select(Normalize).Any(n => n.Length > 0 && names.Contains(n));
    }

    internal static bool IsInterchange(TimetableStation station) => OpsController.Shared?.EnabledInterchanges.Any(i => MatchesInterchange(station, i)) == true;

    internal static bool TrySpawnRandom(NpcRandomDeparture departure, Settings settings)
    {
        var controller = TimetableController.Shared;
        if (!controller.TryGetStation(departure.From, out var from) || !controller.TryGetStation(departure.To, out var to) ||
            !TryGetStationLocation(from, out var a) || !TryGetStationLocation(to, out var b) ||
            !NpcTrainOperations.Route(a, b, out var route, out _)) return false;
        double start = TimeWeather.Now.TotalSeconds;
        var train = NpcTimetableRegistry.MakeTrain(departure.Symbol, departure.From, departure.To, departure.TrainClass, start,
            start + NpcTrainOperations.TravelSeconds(route, departure.TrainClass == 0));
        if (NpcServiceStore.State.ProtectedTimetables.TryGetValue(departure.Symbol, out var source) &&
            controller.TryRead(source, out var planned, null) && planned.Trains.ContainsKey(departure.Symbol)) {
            train = planned.ToAbsolute().Trains[departure.Symbol];
            start = departure.Scheduled;
        }
        return TrySpawn(train, settings, start, true, departure.FreightDefinitions);
    }

    internal static bool TryGetStationLocation(TimetableStation station, out Location location)
    {
        location = Location.Invalid;
        var span = station.passengerStop?.TrackSpans?.FirstOrDefault();
        if (span != null && Graph.Shared.TryGetLocationFromPoint(span.GetSegments().FirstOrDefault(), span.GetCenterPoint(), 200f, out location)) return true;
        var interchange = OpsController.Shared?.EnabledInterchanges.FirstOrDefault(i => MatchesInterchange(station, i));
        span = interchange?.TrackSpans.FirstOrDefault();
        if (span != null && Graph.Shared.TryGetLocationFromPoint(span.GetSegments().FirstOrDefault(), span.GetCenterPoint(), 200f, out location)) return true;
        var feature = station.mapFeature;
        if (feature == null) return false;
        if (feature.GetType().GetProperty("CenterPoint")?.GetValue(feature, null) is UnityEngine.Vector3 point)
            return Graph.Shared.TryGetLocationFromGamePoint(point, 200f, out location);
        return false;
    }
}
