using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Messages;
using Game.State;
using KeyValue.Runtime;
using Model;
using Model.Ops;
using Model.Ops.Timetable;
using Track;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

/// <summary>Save-backed ownership of NPC timetable columns and their empty, AI-only crews.</summary>
internal static class NpcTimetableRegistry
{
    internal static bool InternalChange { get; private set; }
    internal static bool AssigningCrew { get; private set; }
    private static float nextCheck;
    internal static string NewSymbol(string prefix)
    {
        var state = NpcServiceStore.State;
        string symbol;
        do { symbol = prefix + state.NextNpcTrainNumber++.ToString("0000"); }
        while (state.ProtectedTimetables.ContainsKey(symbol) || state.RandomDepartures.Any(d => d.Symbol == symbol) ||
            TimetableController.Shared?.CurrentRaw?.Trains.ContainsKey(symbol) == true);
        return symbol;
    }
    internal static string NewFreightSymbol(string from, string to, int trainClass, bool interchange = false, string? pairedWith = null)
    {
        var codes = TimetableController.Shared.GetAllStations(includeDisabled: true, includeDuplicates: false).Select(s => s.code).ToList();
        bool west = codes.IndexOf(from) < codes.IndexOf(to);
        var state = NpcServiceStore.State;
        bool Available(string candidate) => !state.ProtectedTimetables.ContainsKey(candidate) &&
            !state.RandomDepartures.Any(d => d.Symbol == candidate) &&
            !state.Services.Any(s => s.TrainSymbol == candidate || s.ReturnTrainSymbol == candidate) &&
            TimetableController.Shared?.CurrentRaw?.Trains.ContainsKey(candidate) != true;
        if (pairedWith != null && int.TryParse(pairedWith.Split('-').Last(), out int outbound))
        {
            string sectionPrefix = pairedWith.Contains("-") ? pairedWith.Substring(0, pairedWith.LastIndexOf('-') + 1) : "";
            string opposite = sectionPrefix + (outbound % 2 == 1 ? outbound + 1 : outbound - 1);
            if (Available(opposite)) return opposite;
        }
        int first = interchange ? 300 : trainClass == 0 ? 100 : trainClass == 1 ? 200 : 400;
        for (int section = 0; ; section++)
        for (int attempt = 0; attempt < 49; attempt++)
        {
            int pair = (state.NextNpcTrainNumber++ - 1) % 49;
            string symbol = (first + pair * 2 + (west ? 1 : 2)).ToString();
            // Sections allow unusually busy saves to stay within short numbers.
            if (section > 0) symbol = (section + 1) + "-" + symbol;
            if (Available(symbol)) return symbol;
        }
    }
    internal static bool IsProtected(string? symbol) => !string.IsNullOrEmpty(symbol) && NpcServiceStore.Load() &&
        NpcServiceStore.State.ProtectedTimetables.ContainsKey(symbol!);
    internal static bool IsAiCrew(string? id) => !string.IsNullOrEmpty(id) && NpcServiceStore.Load() &&
        NpcServiceStore.State.AiCrews.ContainsValue(id!);

    internal static Timetable.Train MakeTrain(string symbol, string from, string to, int trainClass, double depart, double arrive, bool grace = false)
    {
        var controller = TimetableController.Shared;
        var codes = controller.GetAllStations(includeDisabled: true, includeDuplicates: false).Select(s => s.code).ToList();
        var direction = codes.IndexOf(from) < codes.IndexOf(to) ? Timetable.Direction.West : Timetable.Direction.East;
        var links = new List<NpcTimetablePlanPolicy.Link>();
        var connectivity = TimetableTrackConnectivity.Current;
        foreach (var branch in controller.branches) {
            // Ownership does not shorten an NPC's trip across the railroad.
            var stations = branch.stations.ToList();
            for (int i = 1; i < stations.Count; i++)
            {
                if (!connectivity.Visible(stations[i - 1]) || !connectivity.Visible(stations[i]) ||
                    !connectivity.Connected(stations[i - 1], stations[i])) continue;
                // Match native timing from the branch's authored traverse time,
                // including junction duplicates and locked station pairs.
                links.Add(new(stations[i - 1].code, stations[i].code,
                    Math.Max(1, (int)Math.Ceiling(stations[i - 1].traverseTimeToNext * (trainClass == 0 ? 1d : 1.2d) / 60))));
            }
        }
        var path = NpcTimetablePlanPolicy.Path(links, from, to);
        if (path.Count < 2) path = new() { from, to };
        var entries = new List<Timetable.Entry> { new(from, TimetableTime.Absolute((int)(depart / 60)), Array.Empty<string>()) };
        for (int i = 1; i < path.Count; i++) {
            int minutes = links.Where(e => e.From == path[i - 1] && e.To == path[i] || e.To == path[i - 1] && e.From == path[i])
                .Select(e => e.Minutes).DefaultIfEmpty(Math.Max(1, (int)Math.Ceiling((arrive - depart) / 60))).Min();
            entries.Add(new(path[i], TimetableTime.Relative(Math.Max(1, minutes)), Array.Empty<string>()));
        }
        if (grace)
        {
            int total = entries.Skip(1).Sum(e => e.DepartureTime.Minutes);
            int allowed = Math.Max(total, (int)Math.Ceiling((arrive - depart) / 60));
            int elapsed = 0, previous = 0;
            for (int i = 1; i < entries.Count; i++)
            {
                elapsed += entries[i].DepartureTime.Minutes;
                int minutes = (int)Math.Ceiling(elapsed * (double)allowed / Math.Max(1, total));
                entries[i] = new(entries[i].Station, TimetableTime.Relative(Math.Max(1, minutes - previous)), Array.Empty<string>());
                previous = minutes;
            }
        }
        return new Timetable.Train(symbol, direction, (Timetable.TrainClass)trainClass, Timetable.TrainType.Freight, entries);
    }

    internal static bool ExpandSavedRoute(Timetable.Train train, out Timetable.Train expanded, out Dictionary<int, int> indices)
    {
        expanded = train.Clone(); indices = new Dictionary<int, int>();
        if (train.Entries.Count < 2) return false;
        var times = NpcTimetablePlanning.Minutes(train);
        var entries = new List<Timetable.Entry>();
        bool changed = false;
        for (int i = 0; i < train.Entries.Count; i++) {
            var entry = train.Entries[i];
            int arrival = times[i];
            if (entry.ArrivalTime.HasValue && train.TryGetAbsoluteTimeForEntry(i, TimetableTimeType.Arrival, out int clock))
                arrival = (int)TimetableForecastPolicy.ClockNear(clock, times[i]);
            if (i > 0) {
                var leg = MakeTrain("migration", train.Entries[i - 1].Station, entry.Station, (int)train.TrainClass,
                    0, Math.Max(0, arrival - times[i - 1]) * 60d);
                foreach (var passing in NpcTimetablePlanPolicy.PassingEntries(leg.Entries.Select(e => e.Station).ToList(),
                    leg.Entries.Skip(1).Select(e => e.DepartureTime.Minutes).ToList(), times[i - 1], arrival)) {
                    entries.Add(new(passing.Code, TimetableTime.Absolute(passing.Minute), Array.Empty<string>()));
                    changed = true;
                }
            }
            indices[i] = entries.Count;
            // Preserve published departure, arrival, meets and switching dwell.
            entries.Add(entry.ArrivalTime.HasValue ?
                new(entry.Station, TimetableTime.Absolute(arrival), TimetableTime.Absolute(times[i]), entry.Meets) :
                new(entry.Station, TimetableTime.Absolute(times[i]), entry.Meets));
        }
        if (!changed) return false;
        expanded.Entries.Clear(); expanded.Entries.AddRange(entries);
        return true;
    }

    internal static double EstimatedTravelSeconds(string from, string to, int trainClass, double fallback)
    {
        var train = MakeTrain("estimate", from, to, trainClass, 0, fallback);
        return train.Entries.Skip(1).Sum(e => e.DepartureTime.Minutes) * 60d;
    }

    internal static string EnsureCrew(string symbol)
    {
        var state = NpcServiceStore.State;
        var manager = StateManager.Shared.PlayersManager;
        if (state.AiCrews.TryGetValue(symbol, out var id) && manager.TrainCrewForId(id, out var existingCrew))
        {
            if (existingCrew.MemberPlayerIds.Count > 0 || existingCrew.TimetableSymbol != symbol)
            {
                existingCrew.MemberPlayerIds.Clear();
                existingCrew.TimetableSymbol = symbol;
                StateManager.ApplyLocal(new UpdateTrainCrews(manager.TrainCrewsSnapshot()));
            }
            return id;
        }
        // Never take over a player's crew merely because it has the same timetable symbol.
        string name = "AI " + symbol;
        var existing = manager.TrainCrews.FirstOrDefault(c => c.Name == name &&
            (c.Description == "Automated traffic — AI only" || c.Description == "Automated through traffic"));
        if (existing == null)
        {
            name += " " + Guid.NewGuid().ToString("N").Substring(0, 6);
            InternalChange = true;
            try
            {
                manager.HandleRequestCreateTrainCrew(manager.LocalPlayer, new Snapshot.TrainCrew
                { Name = name, Description = "Automated traffic — AI only", TimetableSymbol = symbol, MemberPlayerIds = new HashSet<string>() });
            }
            finally { InternalChange = false; }
            // The game allocates the real ID inside HandleRequestCreateTrainCrew.
            existing = manager.TrainCrews.First(c => c.Name == name);
        }
        state.AiCrews[symbol] = existing.Id;
        existing.Description = "Automated traffic — AI only";
        existing.MemberPlayerIds.Clear();
        existing.TimetableSymbol = symbol;
        StateManager.ApplyLocal(new UpdateTrainCrews(manager.TrainCrewsSnapshot()));
        NpcServiceStore.Save();
        return existing.Id;
    }

    internal static void Register(NpcServiceRecord service, Timetable.Train train)
    {
        service.TrainSymbol = train.Name;
        var state = NpcServiceStore.State;
        Reserve(train);
        service.CrewId = EnsureCrew(train.Name);
        foreach (var id in service.PowerIds.Concat(service.Inbound.Skip(service.SetoutDone)))
            if (TrainController.Shared.TryGetCarForId(id, out var car)) SetCrew(car, service.CrewId);
        NpcServiceStore.Save();
        Restore();
    }

    internal static void Reserve(Timetable.Train train)
    {
        if (!NpcServiceStore.State.ProtectedTimetables.ContainsKey(train.Name)) NpcTimetablePlanning.PlanMeets(train);
        SaveTrain(train);
    }

    internal static void SaveTrain(Timetable.Train train) =>
        NpcServiceStore.State.ProtectedTimetables[train.Name] = TimetableWriter.Write(new Timetable(new Dictionary<string, Timetable.Train> { [train.Name] = train.Clone() }));

    internal static void SetCrew(Car car, string? crewId)
    {
        if (car.trainCrewId == crewId) return;
        AssigningCrew = true;
        try { NpcTrainOperations.Trusted(() => StateManager.ApplyLocal(new SetCarTrainCrew(car.id, crewId!))); }
        finally { AssigningCrew = false; }
    }

    internal static void RegisterReturn(NpcServiceRecord service)
    {
        if (service.TrainSymbol.Length == 0 || TimetableController.Shared == null) return;
        var from = Graph.Shared.ResolveLocationString(service.Target);
        var to = Graph.Shared.ResolveLocationString(service.Spawn);
        if (!InterchangeStations(from, to, out var origin, out var destination)) return;
        int trainClass = 2;
        if (TimetableController.Shared.CurrentRaw.Trains.TryGetValue(service.TrainSymbol, out var previous)) trainClass = (int)previous.TrainClass;
        if (service.ReturnTrainSymbol.Length > 0) {
            if (int.TryParse(service.TrainSymbol.Split('-').Last(), out _))
            {
                string nextSymbol = service.ReturnTrainSymbol;
                Remove(new NpcServiceRecord { TrainSymbol = service.TrainSymbol });
                service.TrainSymbol = nextSymbol;
            }
            else Remove(new NpcServiceRecord { TrainSymbol = service.ReturnTrainSymbol });
            service.ReturnTrainSymbol = "";
        }
        double now = TimeWeather.Now.TotalSeconds;
        double travel = NpcTrainOperations.Route(from, to, out var route, out _) ? NpcTrainOperations.TravelSeconds(route, trainClass == 1) : 60;
        TimetableLiveForecast.Forget(service.TrainSymbol, service.Id);
        Register(service, MakeTrain(service.TrainSymbol, origin, destination, trainClass, now,
            now + NpcTimetablePacingPolicy.BufferedTravelSeconds(travel), grace: true));
        foreach (string id in service.PowerIds.Concat(service.Outbound.Where(p => p.Queued && !p.Missed).Select(p => p.CarId)))
            if (TrainController.Shared.TryGetCarForId(id, out var car)) SetCrew(car, service.CrewId);
        NpcServiceStore.Save();
    }

    internal static void RefreshReturnReservation(NpcServiceRecord service)
    {
        if (service.ReturnTrainSymbol.Length == 0 || service.Phase == "Departing") return;
        var target = Graph.Shared.ResolveLocationString(service.Target);
        var exit = Graph.Shared.ResolveLocationString(service.Spawn);
        if (!InterchangeStations(target, exit, out var from, out var to)) return;
        double arrival = TimeWeather.Now.TotalSeconds;
        if (service.Phase == "Approaching" && TrainController.Shared.TryGetCarForId(service.LeadId, out var lead) &&
            NpcTrainOperations.Route(lead.LocationF, target, out var inboundRoute, out _))
            arrival += NpcTrainOperations.TravelSeconds(inboundRoute, false);
        double departure = NpcServicePolicy.ReturnDeparture(service.Scheduled, arrival,
            service.Inbound.Count - service.SetoutDone,
            Math.Max(service.PickupSnapshot.Count, service.Outbound.Count) - service.PickupDone, service.TransferSecondsPerCar);
        double travel = NpcTrainOperations.Route(target, exit, out var route, out _) ? NpcTrainOperations.TravelSeconds(route, false) : 60;
        Reserve(MakeTrain(service.ReturnTrainSymbol, from, to, 2, departure,
            departure + NpcTimetablePacingPolicy.BufferedTravelSeconds(travel), grace: true));
        service.ReturnPlanVersion = 1;
        NpcServiceStore.Save(); Restore();
        ModDiagnosticLog.Write("RETURN PLAN", $"{service.ReturnTrainSymbol}: revised after manifest confirmation; departure {NpcRandomTrafficPolicy.Clock(departure)}, setouts {service.Inbound.Count - service.SetoutDone}, pickups {Math.Max(service.PickupSnapshot.Count, service.Outbound.Count) - service.PickupDone}, including five minutes for repositioning.");
    }

    internal static void RegisterInterchange(NpcServiceRecord service)
    {
        if (TimetableController.Shared == null || Graph.Shared == null) return;
        if (service.TrainSymbol.Length > 0 && NpcServiceStore.State.ProtectedTimetables.TryGetValue(service.TrainSymbol, out var saved) &&
            TimetableController.Shared.TryRead(saved, out var timetable, null) && timetable.Trains.TryGetValue(service.TrainSymbol, out var planned))
        { Register(service, planned); return; }
        var from = Graph.Shared.ResolveLocationString(service.Spawn);
        var to = Graph.Shared.ResolveLocationString(service.Target);
        if (!InterchangeStations(from, to, out var origin, out var destination)) return;
        string symbol = service.TrainSymbol.Length > 0 ? service.TrainSymbol : NewFreightSymbol(origin, destination, 2, interchange: true);
        Register(service, MakeTrain(symbol, origin, destination, 2, TimeWeather.Now.TotalSeconds,
            Math.Max(TimeWeather.Now.TotalSeconds + 60, service.Scheduled), grace: true));
    }

    internal static bool InterchangeStations(Location from, Location to, out string originCode, out string destinationCode)
    {
        originCode = destinationCode = "";
        var stations = TimetableController.Shared.GetAllStations(includeDisabled: true, includeDuplicates: false).Select(s =>
            (station: s, location: ThroughTrafficSpawner.TryGetStationLocation(s, out var p) ? p : Location.Invalid))
            .Where(p => p.location.IsValid).ToList();
        if (stations.Count < 2) return false;
        var destination = stations.OrderBy(p => Vector3.SqrMagnitude(p.location.GetPosition() - to.GetPosition())).First();
        var origin = stations.Where(p => p.station.code != destination.station.code)
            .OrderBy(p => Vector3.SqrMagnitude(p.location.GetPosition() - from.GetPosition())).First();
        originCode = origin.station.code; destinationCode = destination.station.code;
        return true;
    }

    internal static bool Merge(Timetable proposed)
    {
        var snapshots = new Dictionary<string, Timetable.Train>();
        foreach (var saved in NpcServiceStore.State.ProtectedTimetables)
        {
            if (!TimetableController.Shared.TryRead(saved.Value, out var snapshot, null) || !snapshot.Trains.TryGetValue(saved.Key, out var train)) continue;
            snapshots[saved.Key] = train;
        }
        return NpcTimetableProtection.Merge(proposed.Trains, snapshots, train => train.Clone());
    }

    internal static void IncludeSavedPlans(Timetable display)
    {
        var controller = TimetableController.Shared;
        if (controller == null || !NpcServiceStore.Load()) return;
        foreach (var saved in NpcServiceStore.State.ProtectedTimetables) {
            if (!controller.TryRead(saved.Value, out var snapshot, null)) continue;
            var absolute = snapshot.ToAbsolute();
            if (absolute.Trains.TryGetValue(saved.Key, out var train)) display.Trains[saved.Key] = train.Clone();
        }
    }

    internal static void Restore()
    {
        var controller = TimetableController.Shared;
        if (!StateManager.IsHost || controller == null || !NpcServiceStore.Load()) return;
        string source = new TimetableController.TimetableDocument(controller._keyValueObject["current"]).Source;
        if (!controller.TryRead(source ?? "", out var timetable, null)) return; // Do not overwrite malformed player source.
        if (Merge(timetable)) Publish(timetable);
    }

    private static void Publish(Timetable timetable)
    {
        InternalChange = true;
        try
        {
            var controller = TimetableController.Shared;
            controller._keyValueObject["current"] = new TimetableController.TimetableDocument(TimetableWriter.Write(timetable), TimeWeather.Now, "AI dispatcher").ToValue();
            ModDiagnosticLog.Write("DAY PLAN", $"Published timetable document: {timetable.Trains.Count} trains; native usable={controller.Current?.Trains.Count ?? 0}; protected NPC plans={NpcServiceStore.State.ProtectedTimetables.Count}; company timetable feature={Game.State.StateManager.Shared.Storage.TimetableFeature}.",
                "timetable-publication", 30);
        }
        finally { InternalChange = false; }
    }

    internal static void Remove(NpcServiceRecord service, bool expire = false)
    {
        if (service.ReturnTrainSymbol.Length > 0) Remove(new NpcServiceRecord { TrainSymbol = service.ReturnTrainSymbol }, expire);
        if (service.TrainSymbol.Length == 0) return;
        NpcDispatchBoardMarkers.Remove(service.TrainSymbol);
        TimetableLiveForecast.Forget(service.TrainSymbol, service.Id);
        var state = NpcServiceStore.State;
        state.TrafficPlans.RemoveAll(p => p.Symbol == service.TrainSymbol);
        if (expire) { state.ProtectedTimetables.Remove(service.TrainSymbol); state.CompletedTimetableDays.Remove(service.TrainSymbol); }
        else if (state.ProtectedTimetables.ContainsKey(service.TrainSymbol) && !state.CompletedTimetableDays.ContainsKey(service.TrainSymbol))
            state.CompletedTimetableDays[service.TrainSymbol] = (int)Math.Floor(TimeWeather.Now.TotalSeconds / 86400);
        if (state.AiCrews.TryGetValue(service.TrainSymbol, out var crew))
        {
            foreach (var car in TrainController.Shared.Cars.Where(c => c.trainCrewId == crew).ToList()) SetCrew(car, null);
            InternalChange = true;
            try
            {
                var manager = StateManager.Shared.PlayersManager;
                if (manager.TrainCrewForId(crew, out _)) manager.HandleRequestDeleteTrainCrew(manager.LocalPlayer, crew);
                state.AiCrews.Remove(service.TrainSymbol);
            }
            finally { InternalChange = false; }
        }
        NpcServiceStore.Save();
        var controller = TimetableController.Shared;
        if (expire && controller != null && controller.TryRead(new TimetableController.TimetableDocument(controller._keyValueObject["current"]).Source ?? "", out var timetable, null) &&
            timetable.Trains.Remove(service.TrainSymbol)) Publish(timetable);
    }

    internal static void Tick()
    {
        if (!StateManager.IsHost || StateManager.Shared?.HasRestoredProperties != true || StateManager.IsUnloading ||
            TrainController.Shared == null || OpsController.Shared == null || TimetableController.Shared == null || !NpcServiceStore.Load() || Time.realtimeSinceStartup < nextCheck) return;
        nextCheck = Time.realtimeSinceStartup + 2;
        int day = (int)Math.Floor(TimeWeather.Now.TotalSeconds / 86400);
        foreach (var completed in NpcServiceStore.State.CompletedTimetableDays.Where(p => p.Value < day).ToList())
            Remove(new NpcServiceRecord { TrainSymbol = completed.Key }, expire: true);
        foreach (var service in NpcServiceStore.State.Services.ToList())
        {
            if (service.InterchangeId.Length > 0) SimulatedInterchangeService.ReleaseCompletedSetouts(service);
            if (!TrainController.Shared.TryGetCarForId(service.LeadId, out _))
            {
                Remove(service);
                NpcServiceStore.State.Services.Remove(service);
                continue;
            }
            if (service.InterchangeId.Length > 0 && !IsProtected(service.TrainSymbol)) RegisterInterchange(service);
            else if (!IsProtected(service.TrainSymbol) && TimetableController.Shared.Current?.Trains.TryGetValue(service.TrainSymbol, out var train) == true) Register(service, train);
            else if (service.ReturnTrainSymbol.Length > 0 && service.Phase == "Departing") RegisterReturn(service);
            else if (service.TrainSymbol.Length > 0) service.CrewId = EnsureCrew(service.TrainSymbol);
            if (service.ReturnPlanVersion < 1 && service.ReturnTrainSymbol.Length > 0 &&
                (service.Phase == "Approaching" || service.Phase == "Setting out" || service.Phase == "Picking up"))
                RefreshReturnReservation(service);
        }
        foreach (var orphan in NpcServiceStore.State.ProtectedTimetables.Keys.Where(s => !NpcServiceStore.State.CompletedTimetableDays.ContainsKey(s) && !NpcTrafficPlanPolicy.Owns(NpcServiceStore.State, s)).ToList())
            Remove(new NpcServiceRecord { TrainSymbol = orphan });
        foreach (var orphan in NpcServiceStore.State.AiCrews.Keys.Where(s => !NpcTrafficPlanPolicy.Owns(NpcServiceStore.State, s)).ToList())
            Remove(new NpcServiceRecord { TrainSymbol = orphan });
        foreach (var interchange in OpsController.Shared.EnabledInterchanges) NpcPulpwoodOrdering.Restore(interchange);
        Restore();
        NpcServiceStore.Save();
    }
}
