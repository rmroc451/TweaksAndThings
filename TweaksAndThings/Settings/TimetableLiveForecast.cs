using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.State;
using Model;
using Model.Ops;
using Model.Ops.Timetable;
using Track;
using Track.Search;
using UnityEngine;
using Timetable = Model.Ops.Timetable.Timetable;

namespace RMROC451.TweaksAndThings;

internal static class TimetableLiveForecast
{
    internal sealed class Stop
    {
        internal string Code = "";
        internal double Arrival, Departure;
    }
    internal sealed class Forecast
    {
        internal string Symbol = "", Status = "", VehicleId = "";
        internal double Now, Speed, Progress;
        internal float UpdatedAt;
        internal bool ShowPosition = true;
        internal Leg Route = null!;
        internal readonly List<Stop> Stops = new();
    }
    internal sealed class Leg
    {
        internal string Key = "";
        internal string FromCode = "", ToCode = "";
        internal Location From, To;
        internal List<RouteSearch.Step>? Steps;
        internal double Meters;
        internal float Retry;
        internal bool Queued;
        internal bool Project(Location location, out double meters)
        {
            meters = 0;
            if (Steps == null || !location.IsValid) return false;
            foreach (var step in Steps)
            {
                if (location.segment == step.Location.segment)
                {
                    double d = location.WithEnd(step.Location.end).distance;
                    double begin = step.Location.distance - step.Distance;
                    if (d >= begin - 25 && d <= step.Location.distance + 25)
                    { meters += Math.Max(0, Math.Min(step.Distance, d - begin)); return true; }
                }
                meters += step.Distance;
            }
            return false;
        }
        internal double? Travel(double progress, double speed, bool knownDeparture)
        {
            double total = 0, offset = 0;
            if (Steps == null) return null;
            foreach (var step in Steps)
            {
                double distance = Math.Max(0, offset + step.Distance - Math.Max(offset, progress));
                double? seconds = TimetableForecastPolicy.TravelSeconds(distance, speed, Math.Max(1, step.Location.segment.GetExpectedSpeedLimit()), knownDeparture);
                if (!seconds.HasValue) return null;
                total += seconds.Value; offset += step.Distance;
            }
            return total * Math.Max(0.01, TimeWeather.TimeMultiplier);
        }
    }

    private static Graph? graph;
    private static int lastFrame;
    private static readonly Dictionary<string, Leg> legs = new();
    private static readonly Queue<Leg> pending = new();
    private static readonly Dictionary<string, Location> stations = new();
    private static readonly Dictionary<string, Forecast> latest = new();
    internal static void Reset()
    { graph = null; lastFrame = -1; legs.Clear(); pending.Clear(); stations.Clear(); latest.Clear(); }

    // Search only while the chart is visible, at most one route per rendered frame.
    internal static void Advance()
    {
        if (graph != Graph.Shared) { Reset(); graph = Graph.Shared; }
        if (Graph.Shared == null || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        if (pending.Count == 0) return;
        var leg = pending.Dequeue(); leg.Queued = false;
        if (!legs.TryGetValue(leg.Key, out var registered) || !ReferenceEquals(registered, leg)) return;
        leg.Steps = NpcTrainOperations.Route(leg.From, leg.To, out var steps, out var meters) ? steps : null;
        leg.Meters = meters; leg.Retry = Time.realtimeSinceStartup + 15;
    }

    internal static Location Station(string code)
    {
        if (stations.TryGetValue(code, out var location) && location.IsValid) return location;
        if (TimetableController.Shared.TryGetStation(code, out var station) && ThroughTrafficSpawner.TryGetStationLocation(station, out location))
            stations[code] = location;
        return location;
    }

    private static Leg? GetLeg(string key, string from, string to, Location? origin = null, Location? destination = null)
    {
        if (!legs.TryGetValue(key, out var leg))
        {
            var a = origin ?? Station(from); var b = destination ?? Station(to);
            if (!a.IsValid || !b.IsValid) return null;
            leg = new Leg { Key = key, From = a, To = b, FromCode = from, ToCode = to };
            legs[key] = leg;
        }
        if (leg.Steps == null && !leg.Queued && Time.realtimeSinceStartup >= leg.Retry)
        { leg.Queued = true; pending.Enqueue(leg); }
        if (leg.Steps?.Any(s => !s.Location.segment.GroupEnabled) == true) leg.Steps = null;
        return leg.Steps == null ? null : leg;
    }

    internal static Forecast? Get(Timetable.Train train)
    {
        if (Graph.Shared == null || TrainController.Shared == null || !NpcServiceStore.Load()) return null;
        if (NpcDailyTrafficPlans.IsPending(train.Name)) return null;
        var service = NpcServiceStore.State.Services.FirstOrDefault(s => s.TrainSymbol == train.Name || s.ReturnTrainSymbol == train.Name);
        BaseLocomotive? lead = null;
        if (service != null && TrainController.Shared.TryGetCarForId(service.LeadId, out var car)) lead = car as BaseLocomotive;
        else
        {
            var crew = StateManager.Shared?.PlayersManager?.TrainCrews.FirstOrDefault(c => c.TimetableSymbol == train.Name && !NpcTimetableRegistry.IsAiCrew(c.Id));
            if (crew != null) lead = TrainController.Shared.Cars.OfType<BaseLocomotive>().FirstOrDefault(c => c.trainCrewId == crew.Id && !c.IsMuEnabled && !c.IsInBardo);
        }
        if (lead == null || !lead.OpsLocation.IsValid || train.Entries.Count < 2) { latest.Remove(train.Name); return null; }
        bool interchange = service?.InterchangeId.Length > 0;
        bool returning = interchange && (service!.ReturnTrainSymbol == train.Name ||
            (service.ReturnTrainSymbol.Length == 0 && service.Phase == "Departing" && service.TrainSymbol == train.Name));
        if (interchange && !returning && service!.Phase == "Departing") { latest.Remove(train.Name); return null; }
        int next = service != null ? (interchange ? 1 : Math.Min(service.StopIndex, train.Entries.Count - 1)) : -1;
        if (interchange || service?.RandomFreight == true) next = -1;
        Leg? leg = null; double progress = 0;
        if (next > 0)
        {
            var a = train.Entries[next - 1].Station; var b = train.Entries[next].Station;
            Location? from = null, to = null;
            if (interchange)
            {
                from = Graph.Shared.ResolveLocationString(returning ? service!.Target : service!.Spawn);
                to = Graph.Shared.ResolveLocationString(returning ? service!.Spawn : service!.Target);
            }
            else if (next == 1) from = Graph.Shared.ResolveLocationString(service!.Spawn);
            leg = GetLeg((service?.Id ?? train.Name) + ":" + (returning ? "return" : next.ToString()), a, b, from, to);
            if (leg == null || !leg.Project(lead.OpsLocation, out progress)) return null;
        }
        else
        {
            // Match the train's actual track to a scheduled leg; don't substitute
            // geographical proximity on an unrelated branch.
            double best = double.MaxValue;
            for (int i = 1; i < train.Entries.Count; i++)
            {
                Location? from = null, to = null;
                if (service != null && i == 1) from = Graph.Shared.ResolveLocationString(returning ? service.Target : service.Spawn);
                if (interchange && i == train.Entries.Count - 1) to = Graph.Shared.ResolveLocationString(returning ? service!.Spawn : service!.Target);
                var candidate = GetLeg((service?.Id ?? "timetable") + ":" + (returning ? "return:" : "") + train.Entries[i - 1].Station + ":" + train.Entries[i].Station,
                    train.Entries[i - 1].Station, train.Entries[i].Station, from, to);
                if (candidate == null || !candidate.Project(lead.OpsLocation, out double offset)) continue;
                double clock = TimetableForecastPolicy.ClockNear(train.Entries[i].DepartureTime.Minutes, TimeWeather.Now.TotalSeconds / 60);
                double difference = Math.Abs(clock - TimeWeather.Now.TotalSeconds / 60);
                if (difference < best) { best = difference; next = i; leg = candidate; progress = offset; }
            }
            if (leg == null) return null;
        }
        double now = TimeWeather.Now.TotalSeconds;
        var forecast = new Forecast { Symbol = train.Name, VehicleId = lead.id, Route = leg!, Progress = progress, Now = now / 60, Speed = lead.VelocityMphAbs,
            UpdatedAt = Time.unscaledTime,
            ShowPosition = !returning || service!.Phase == "Departing" };
        double ready = now;
        bool knownWait = false;
        if (interchange && returning && service!.Phase != "Departing")
        {
            forecast.Progress = progress = 0;
            ready = Math.Max(now, service.Scheduled) + RemainingTransfers(service);
            knownWait = service.Phase != "Loading";
        }
        else if (service?.Phase == "At stop" || interchange && (service!.Phase == "Setting out" || service.Phase == "Picking up")) knownWait = true;
        if (service?.Suspended == true || service?.WaitingReason.Length > 0 && service.WaitingReason != "Waiting for configured service time" || service?.Phase == "Loading")
        { forecast.Status = service.Phase == "Loading" ? "Preparing consist; departure uncertain" : "Blocked; departure uncertain"; latest[train.Name] = forecast; return forecast; }
        double reference = (service?.Scheduled ?? now) / 60;
        double previous = TimetableForecastPolicy.ClockNear(train.Entries[0].ArrivalTime?.Minutes ?? train.Entries[0].DepartureTime.Minutes, reference);
        var planned = new List<Stop>();
        foreach (var entry in train.Entries)
        {
            double arrival = TimetableChartGeometry.Unwrap(entry.ArrivalTime?.Minutes ?? entry.DepartureTime.Minutes, previous);
            double departure = TimetableChartGeometry.Unwrap(entry.DepartureTime.Minutes, arrival);
            planned.Add(new Stop { Code = entry.Station, Arrival = arrival * 60, Departure = departure * 60 }); previous = departure;
        }
        // Waiting at the originating station for its scheduled departure.
        if (progress < 30 && planned[next - 1].Departure > now)
        { knownWait = true; ready = Math.Max(ready, planned[next - 1].Departure); }
        bool atNextStop = leg!.Meters - progress <= 30;
        if (atNextStop && planned[next].Departure > now) knownWait = true;
        double? firstTravel = atNextStop ? 0 : leg.Travel(progress, forecast.Speed, knownWait);
        if (!firstTravel.HasValue)
        { forecast.Status = "Stopped; departure uncertain"; latest[train.Name] = forecast; return forecast; }
        if (ready > now)
            forecast.Stops.Add(new Stop { Code = train.Entries[next - 1].Station, Arrival = now / 60, Departure = ready / 60 });
        double arrivalTime = ready + firstTravel.Value;
        for (int i = next; i < train.Entries.Count; i++)
        {
            var entry = train.Entries[i];
            double dwell = planned[i].Departure - planned[i].Arrival;
            if (i == next && atNextStop && now >= planned[i].Arrival && now < planned[i].Departure)
                dwell = planned[i].Departure - now;
            double meet = 0;
            foreach (var partner in entry.Meets)
            {
                var predicted = latest.TryGetValue(partner, out var other) ? other.Stops.FirstOrDefault(s => s.Code == entry.Station) : null;
                if (predicted != null) meet = Math.Max(meet, predicted.Arrival * 60);
                else if (TimetableController.Shared.Current.Trains.TryGetValue(partner, out var otherTrain))
                {
                    int index = otherTrain.Entries.FindIndex(e => e.Station == entry.Station);
                    if (index >= 0 && otherTrain.TryGetAbsoluteTimeForEntry(index, TimetableTimeType.Departure, out int minute))
                        meet = Math.Max(meet, TimetableForecastPolicy.ClockNear(minute, arrivalTime / 60) * 60);
                }
            }
            double passenger = 0;
            if (service != null && TimetableController.Shared.TryGetStation(entry.Station, out var station) && station.passengerStop != null)
                passenger = lead.EnumerateCoupled().Sum(c => c.GetPassengerMarker()?.CountPassengersForStop(station.passengerStop.identifier) ?? 0);
            double departureTime = TimetableForecastPolicy.Departure(arrivalTime, Math.Max(dwell, passenger), planned[i].Departure, meet, 0);
            if (interchange && !returning && i == train.Entries.Count - 1) departureTime = Math.Max(departureTime, Math.Max(arrivalTime, service!.Scheduled) + RemainingTransfers(service));
            forecast.Stops.Add(new Stop { Code = entry.Station, Arrival = arrivalTime / 60, Departure = departureTime / 60 });
            if (i + 1 >= train.Entries.Count) break;
            var nextLeg = GetLeg(entry.Station + ":" + train.Entries[i + 1].Station, entry.Station, train.Entries[i + 1].Station);
            double? travel = nextLeg?.Travel(0, forecast.Speed, knownWait);
            if (!travel.HasValue) break;
            arrivalTime = departureTime + travel.Value;
        }
        forecast.Status = knownWait ? "Forecast includes scheduled wait / service work" : "Forecast at current speed, capped by track limits";
        latest[train.Name] = forecast;
        return forecast;
    }

    private static double RemainingTransfers(NpcServiceRecord service)
    {
        int inbound = service.InboundPlanKnown ? Math.Max(service.PlannedInboundCars, service.Inbound.Count) : service.Inbound.Count;
        int count = Math.Max(0, inbound - service.SetoutDone) + Math.Max(0, Math.Max(service.PickupSnapshot.Count, service.Outbound.Count) - service.PickupDone);
        if ((service.Phase == "Setting out" || service.Phase == "Picking up") && service.NextTransfer > TimeWeather.Now.TotalSeconds)
        {
            int pendingCount = service.Phase == "Picking up" ? service.Outbound.Count - service.PickupDone : service.Inbound.Count - service.SetoutDone;
            int batch = Math.Min(NpcServicePolicy.BatchSize, Math.Max(0, pendingCount));
            return service.NextTransfer - TimeWeather.Now.TotalSeconds + Math.Max(0, count - batch) * service.TransferSecondsPerCar;
        }
        return count * service.TransferSecondsPerCar;
    }

    internal static float? Row(Forecast forecast, IReadOnlyList<TimetableStation> rows)
    {
        var anchors = new List<(double meters, int row)>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (forecast.Route.Project(Station(rows[i].code), out double meters)) anchors.Add((meters, i));
            else if (rows[i].code == forecast.Route.FromCode) anchors.Add((0, i));
            else if (rows[i].code == forecast.Route.ToCode) anchors.Add((forecast.Route.Meters, i));
        }
        if (anchors.Count == 0) return null;
        anchors = anchors.OrderBy(a => a.meters).ToList();
        var before = anchors.LastOrDefault(a => a.meters <= forecast.Progress);
        if (forecast.Progress < anchors[0].meters) return anchors[0].row;
        var after = anchors.FirstOrDefault(a => a.meters >= forecast.Progress);
        if (forecast.Progress > anchors[anchors.Count - 1].meters) return anchors[anchors.Count - 1].row;
        double width = after.meters - before.meters;
        return width <= 0 ? before.row : (float)(before.row + (after.row - before.row) * (forecast.Progress - before.meters) / width);
    }

    internal static string Status(string symbol) => latest.TryGetValue(symbol, out var forecast) && Time.unscaledTime - forecast.UpdatedAt < 5 ? $"Live: {forecast.Speed:N0} mph · {forecast.Status}" : "No active train matched to this route.";
    internal static string StopText(string symbol, string station) => latest.TryGetValue(symbol, out var forecast) && Time.unscaledTime - forecast.UpdatedAt < 5 && forecast.Stops.FirstOrDefault(s => s.Code == station) is Stop stop
        ? "Forecast " + TimetableChartGeometry.Clock(stop.Arrival) + " / " + TimetableChartGeometry.Clock(stop.Departure) : "Forecast unavailable";

    internal static void Forget(string symbol, string serviceId)
    {
        latest.Remove(symbol);
        if (serviceId.Length == 0) return;
        foreach (string key in legs.Keys.Where(k => k.StartsWith(serviceId + ":")).ToList()) legs.Remove(key);
    }
}
