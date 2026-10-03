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
    internal static bool IsProtected(string? symbol) => !string.IsNullOrEmpty(symbol) && NpcServiceStore.Load() &&
        NpcServiceStore.State.ProtectedTimetables.ContainsKey(symbol!);
    internal static bool IsAiCrew(string? id) => !string.IsNullOrEmpty(id) && NpcServiceStore.Load() &&
        NpcServiceStore.State.AiCrews.ContainsValue(id!);

    internal static Timetable.Train MakeTrain(string symbol, string from, string to, int trainClass, double depart, double arrive)
    {
        var controller = TimetableController.Shared;
        var codes = controller.GetAllStations(includeDuplicates: false).Select(s => s.code).ToList();
        var direction = codes.IndexOf(from) < codes.IndexOf(to) ? Timetable.Direction.West : Timetable.Direction.East;
        var links = new List<NpcTimetablePlanPolicy.Link>();
        foreach (var branch in controller.branches) {
            var stations = branch.stations.Where(s => s.IsEnabled).ToList();
            for (int i = 1; i < stations.Count; i++)
                if (controller.TryGetTimingForStations(stations[i - 1].code, stations[i].code, out int fast, out int slow))
                    links.Add(new(stations[i - 1].code, stations[i].code, trainClass == 0 ? fast : slow));
        }
        var path = NpcTimetablePlanPolicy.Path(links, from, to);
        if (path.Count < 2) path = new() { from, to };
        var entries = new List<Timetable.Entry> { new(from, TimetableTime.Absolute((int)(depart / 60)), Array.Empty<string>()) };
        for (int i = 1; i < path.Count; i++) {
            int minutes = controller.TryGetTimingForStations(path[i - 1], path[i], out int fast, out int slow)
                ? (trainClass == 0 ? fast : slow) : Math.Max(1, (int)Math.Ceiling((arrive - depart) / 60));
            entries.Add(new(path[i], TimetableTime.Relative(Math.Max(1, minutes)), Array.Empty<string>()));
        }
        return new Timetable.Train(symbol, direction, (Timetable.TrainClass)trainClass, Timetable.TrainType.Freight, entries);
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
            Remove(new NpcServiceRecord { TrainSymbol = service.ReturnTrainSymbol });
            service.ReturnTrainSymbol = "";
        }
        double now = TimeWeather.Now.TotalSeconds;
        double travel = NpcTrainOperations.Route(from, to, out var route, out _) ? NpcTrainOperations.TravelSeconds(route, trainClass == 1) : 60;
        TimetableLiveForecast.Forget(service.TrainSymbol, service.Id);
        Register(service, MakeTrain(service.TrainSymbol, origin, destination, trainClass, now, now + System.Math.Max(60, travel)));
        foreach (string id in service.PowerIds.Concat(service.Outbound.Where(p => p.Queued && !p.Missed).Select(p => p.CarId)))
            if (TrainController.Shared.TryGetCarForId(id, out var car)) SetCrew(car, service.CrewId);
        NpcServiceStore.Save();
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
        string symbol = service.TrainSymbol.Length > 0 ? service.TrainSymbol : NewSymbol("NI");
        Register(service, MakeTrain(symbol, origin, destination, 2, TimeWeather.Now.TotalSeconds,
            Math.Max(TimeWeather.Now.TotalSeconds + 60, service.Scheduled)));
    }

    internal static bool InterchangeStations(Location from, Location to, out string originCode, out string destinationCode)
    {
        originCode = destinationCode = "";
        var stations = TimetableController.Shared.GetAllStations(includeDuplicates: false).Select(s =>
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
        }
        finally { InternalChange = false; }
    }

    internal static void Remove(NpcServiceRecord service)
    {
        if (service.ReturnTrainSymbol.Length > 0) Remove(new NpcServiceRecord { TrainSymbol = service.ReturnTrainSymbol });
        if (service.TrainSymbol.Length == 0) return;
        NpcDispatchBoardMarkers.Remove(service.TrainSymbol);
        TimetableLiveForecast.Forget(service.TrainSymbol, service.Id);
        var state = NpcServiceStore.State;
        state.TrafficPlans.RemoveAll(p => p.Symbol == service.TrainSymbol);
        state.ProtectedTimetables.Remove(service.TrainSymbol);
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
        if (controller != null && controller.TryRead(new TimetableController.TimetableDocument(controller._keyValueObject["current"]).Source ?? "", out var timetable, null) &&
            timetable.Trains.Remove(service.TrainSymbol)) Publish(timetable);
    }

    internal static void Tick()
    {
        if (!StateManager.IsHost || StateManager.Shared?.HasRestoredProperties != true || StateManager.IsUnloading ||
            TrainController.Shared == null || OpsController.Shared == null || TimetableController.Shared == null || !NpcServiceStore.Load() || Time.realtimeSinceStartup < nextCheck) return;
        nextCheck = Time.realtimeSinceStartup + 2;
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
        }
        foreach (var orphan in NpcServiceStore.State.ProtectedTimetables.Keys.Where(s => !NpcTrafficPlanPolicy.Owns(NpcServiceStore.State, s)).ToList())
            Remove(new NpcServiceRecord { TrainSymbol = orphan });
        foreach (var orphan in NpcServiceStore.State.AiCrews.Keys.Where(s => !NpcTrafficPlanPolicy.Owns(NpcServiceStore.State, s)).ToList())
            Remove(new NpcServiceRecord { TrainSymbol = orphan });
        foreach (var interchange in OpsController.Shared.EnabledInterchanges) NpcPulpwoodOrdering.Restore(interchange);
        Restore();
        NpcServiceStore.Save();
    }
}
