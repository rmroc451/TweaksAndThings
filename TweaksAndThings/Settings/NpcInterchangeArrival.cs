using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Model.Ops;
using Track;
using Track.Search;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcInterchangeArrival
{
    private static readonly Dictionary<string, float> nextCheck = new();

    internal static bool Ready(NpcServiceRecord service, Interchange interchange, BaseLocomotive lead, Location target)
    {
        var span = interchange.TrackSpans.FirstOrDefault(s => s.id == service.ArrivalSpan);
        return span != null && lead.VelocityMphAbs < 0.5f && Graph.Shared.CheckSameRoute(lead.LocationF, target, 10) &&
            lead.EnumerateCoupled().All(c => span.Contains(c.LocationF) && span.Contains(c.LocationR));
    }

    // Sample the searched route, rather than the current switch alignment.
    // The engineer will line switches when it reaches them.
    private static Location Back(List<RouteSearch.Step> route, float distance)
    {
        for (int i = route.Count - 1; i >= 0; i--)
        {
            var step = route[i];
            if (distance <= step.Distance)
                return new Location(step.Location.segment, step.Location.distance - distance, step.Location.end);
            distance -= step.Distance;
        }
        // Once the lead enters a berth, part of the train is behind the
        // route's starting point. Follow that already occupied track too.
        var first = route[0];
        var start = new Location(first.Location.segment, first.Location.distance - first.Distance, first.Location.end);
        try { return Graph.Shared.LocationByMoving(start, -distance, checkSwitchAgainstMovement: false); }
        catch (EndOfTrack) { return Location.Invalid; }
        catch (SwitchAgainstMovement) { return Location.Invalid; }
    }

    private static bool Fits(List<RouteSearch.Step> route, float offset, float length, TrackSpan span, HashSet<string> own)
    {
        var found = new HashSet<Car>();
        for (float d = 0; d <= length + 10; d += 2)
        {
            var point = Back(route, offset + d);
            if (!point.IsValid || !span.Contains(point)) return false;
            TrainController.Shared.CheckForCarsAtPoint(point.GetPosition(), 2, found, point);
            if (found.Any(c => !own.Contains(c.id))) return false;
        }
        // A vacant pocket beyond parked cars is not an entrance pocket. Check
        // the approach through this span as well as the final train footprint.
        float routeLength = route.Sum(s => s.Distance);
        for (float d = offset + length + 10; d < routeLength; d += 2)
        {
            var point = Back(route, d);
            if (!point.IsValid || !span.Contains(point)) break;
            TrainController.Shared.CheckForCarsAtPoint(point.GetPosition(), 2, found, point);
            if (found.Any(c => !own.Contains(c.id))) return false;
        }
        return true;
    }

    private static bool ParkedFit(Location target, float length, TrackSpan span, HashSet<string> own)
    {
        var found = new HashSet<Car>();
        try
        {
            for (float d = 0; d <= length + 10; d += 2)
            {
                var point = Graph.Shared.LocationByMoving(target, -d, checkSwitchAgainstMovement: false);
                if (!point.IsValid || !span.Contains(point)) return false;
                TrainController.Shared.CheckForCarsAtPoint(point.GetPosition(), 2, found, point);
                if (found.Any(c => !own.Contains(c.id))) return false;
            }
            return true;
        }
        catch (EndOfTrack) { return false; }
        catch (SwitchAgainstMovement) { return false; }
    }

    internal static bool Update(NpcServiceRecord service, Interchange interchange, BaseLocomotive lead, ref Location target, bool force = false)
    {
        // Plan once at departure, then recheck near the yard at most every ten
        // real seconds. Distant trains do not repeatedly scan yard occupancy.
        bool near = Vector3.Distance(lead.LocationF.GetPosition(), interchange.CenterPoint) < 1500;
        if (!force && !near && !string.IsNullOrEmpty(service.ArrivalSpan)) return true;
        if (!force && nextCheck.TryGetValue(service.Id, out float next) && Time.realtimeSinceStartup < next)
        {
            if (service.ArrivalHolding || string.IsNullOrEmpty(service.ArrivalSpan))
                service.WaitingReason = "Waiting for an interchange track long enough to clear the main";
            return !service.ArrivalHolding && !string.IsNullOrEmpty(service.ArrivalSpan);
        }
        nextCheck[service.Id] = Time.realtimeSinceStartup + 10;
        var cars = lead.EnumerateCoupled().ToList();
        var own = cars.Select(c => c.id).ToHashSet();
        float length = TrainController.ApproximateLength(cars.Select(c => c.Descriptor()));
        var reserved = NpcServiceStore.State.Services.Where(s => s.Id != service.Id && s.InterchangeId == service.InterchangeId &&
            (s.Phase == "Approaching" || s.Phase == "Loading" || s.Phase == "Setting out" || s.Phase == "Picking up"))
            .Select(s => s.ArrivalSpan).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
        var spans = interchange.TrackSpans.Where(s => !reserved.Contains(s.id))
            .OrderBy(s => TrainController.Shared.CarsOnSpan(s).Any(c => !own.Contains(c.id))).ToList();
        // Upgrade an already dispatched save using its original approach,
        // before the route becomes too short to describe the rear of the train.
        if (service.ArrivalFootprint.Count == 0)
        {
            var savedTarget = target;
            var savedSpan = spans.FirstOrDefault(s => s.Contains(savedTarget));
            if (savedSpan != null && NpcTrainOperations.Route(Graph.Shared.ResolveLocationString(service.Spawn), target, out var original, out _) &&
                Fits(original, 0, length, savedSpan, own))
            {
                service.ArrivalSpan = savedSpan.id;
                CaptureFootprint(service, original, 0, length);
                ModDiagnosticLog.Write("ARRIVAL", $"{service.Id}: restored fixed berth footprint on {savedSpan.id}; target={service.Target}; train length={length:N0} m.");
            }
        }
        // Preserve a safe berth once the train has actually entered it. Moving
        // its target back to the opposite throat would force a reversal.
        var current = spans.FirstOrDefault(s => s.id == service.ArrivalSpan);
        // Keep the accepted route footprint: switches behind a moving train
        // can change, so reconstructing its rear from the current switch state
        // must not invalidate the berth or replace its fixed destination.
        if (current != null && service.ArrivalFootprint.Count > 0)
        {
            var found = new HashSet<Car>();
            bool safe = true;
            foreach (var encoded in service.ArrivalFootprint)
            {
                var point = Graph.Shared.ResolveLocationString(encoded);
                if (!point.IsValid || !current.Contains(point)) { safe = false; break; }
                TrainController.Shared.CheckForCarsAtPoint(point.GetPosition(), 2, found, point);
                if (found.Any(c => !own.Contains(c.id))) { safe = false; break; }
            }
            if (safe && (Graph.Shared.CheckSameRoute(lead.LocationF, target, 10) ||
                NpcTrainOperations.Route(lead.LocationF, target, out var accepted, out _) && accepted[0].Location.end == lead.LocationF.end))
            {
                if (service.ArrivalHolding) NpcTrainOperations.Order(lead, target);
                service.ArrivalHolding = false;
                service.WaitingReason = string.Empty;
                return true;
            }
        }
        if (current != null && current.Contains(lead.LocationF) &&
            Graph.Shared.CheckSameRoute(lead.LocationF, target, 10) && ParkedFit(target, length, current, own))
        { Resume(service, lead, target); return true; }
        if (current != null && current.Contains(lead.LocationF) &&
            NpcTrainOperations.Route(lead.LocationF, target, out var currentRoute, out _) &&
            Fits(currentRoute, 0, length, current, own))
        { Resume(service, lead, target); return true; }
        foreach (var span in spans)
        {
            if (span.Length < length + 16) continue;
            foreach (var boundary in new[] { span.lower, span.upper })
            {
                if (!boundary.HasValue || !NpcTrainOperations.Route(lead.LocationF, boundary.Value, out var route, out float distance)) continue;
                // Never turn the train into a pushing move to reach a berth.
                if (route[0].Location.end != lead.LocationF.end) continue;
                var end = route.Last();
                float clearance = 6;
                var node = end.Location.end == TrackSegment.End.A ? end.Location.segment.b : end.Location.segment.a;
                if (end.Location.segment.GetLength() - end.Location.distance < 50)
                    clearance = Math.Max(clearance, NpcTrackClearance.Fouling(node) + 6);
                // Start at the far end. An occupied span may still have an
                // unobstructed entrance pocket long enough to clear the main.
                for (float offset = clearance; offset < Math.Min(distance, span.Length); offset += 5)
                {
                    var candidate = Back(route, offset);
                    if (!candidate.IsValid || !span.Contains(candidate)) break;
                    if (!Fits(route, offset, length, span, own)) continue;
                    string encoded = Graph.Shared.LocationToString(candidate);
                    bool changed = service.Target != encoded;
                    if (changed)
                    {
                        service.Target = encoded;
                        target = candidate;
                        ModDiagnosticLog.Write("ARRIVAL", $"{service.Id}: berth {span.id}, " +
                            (TrainController.Shared.CarsOnSpan(span).Any(c => !own.Contains(c.id)) ? "occupied span, clear entrance pocket" : "empty span") +
                            $"; target={encoded}; train length={length:N0} m; end clearance={offset:N0} m.");
                    }
                    if (force || changed || service.ArrivalSpan != span.id || service.ArrivalHolding)
                        NpcTrainOperations.Order(lead, target);
                    service.ArrivalSpan = span.id;
                    service.ArrivalHolding = false;
                    CaptureFootprint(service, route, offset, length);
                    service.WaitingReason = string.Empty;
                    return true;
                }
            }
        }
        service.WaitingReason = "Waiting for an interchange track long enough to clear the main";
        // Hold short of the yard instead of starting transfers on the main.
        if (!service.ArrivalHolding && !interchange.TrackSpans.Any(s => s.Contains(lead.LocationF)) &&
            NpcTrainOperations.Route(lead.LocationF, target, out var approach, out float remaining) && remaining > 100)
        {
            var hold = Back(approach, 100);
            if (hold.IsValid) NpcTrainOperations.Order(lead, hold);
        }
        else if (!service.ArrivalHolding) NpcTrainOperations.Hold(lead);
        service.ArrivalHolding = true;
        NpcTrafficDiagnostics.Detail("arrival:" + service.Id, service.WaitingReason);
        return false;
    }

    private static void CaptureFootprint(NpcServiceRecord service, List<RouteSearch.Step> route, float offset, float length)
    {
        service.ArrivalFootprint.Clear();
        for (float d = 0; d <= length + 10; d += 2)
            service.ArrivalFootprint.Add(Graph.Shared.LocationToString(Back(route, offset + d)));
    }

    private static void Resume(NpcServiceRecord service, BaseLocomotive lead, Location target)
    {
        if (service.ArrivalHolding) NpcTrainOperations.Order(lead, target);
        service.ArrivalHolding = false;
        service.WaitingReason = string.Empty;
    }
}
