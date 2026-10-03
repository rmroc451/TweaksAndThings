using System;
using System.Collections.Generic;
using System.Linq;
using Game.Messages;
using Game.State;
using Model;
using Model.AI;
using Track;
using Track.Search;
using Track.Signals;
using UnityEngine;
using Helpers;

namespace RMROC451.TweaksAndThings;

internal static class NpcTrafficRouting
{
    private static Graph? graph;
    private static readonly NpcCorridorPolicy reservations = new();
    private static readonly Dictionary<string, float> retries = new();
    private static readonly Dictionary<string, string> notices = new();
    private static readonly Dictionary<string, (float expires, int end, int entrance)> refuges = new();
    private static readonly Dictionary<string, Location> grantedEnds = new();
    private static readonly Dictionary<string, TrackNode> exitNodes = new();
    private static readonly Dictionary<string, (TrackNode entrance, HashSet<string> segments)> sidingTracks = new();
    private static readonly HashSet<string> yielding = new();
    private static readonly HashSet<string> reverseRejected = new();
    private static CTCBlock[] blocks = Array.Empty<CTCBlock>();
    private static HashSet<TrackSegment> ctcSegments = new();
    private static float nextBlocks;
    private static int lookaheadDepth;

    internal static bool MaySpawn(Location spawn, Location destination, float length)
    {
        if (Graph.Shared != graph) { Reset(); graph = Graph.Shared; }
        RefreshBlocks();
        if (!NpcTrainOperations.Route(spawn, destination, out var departure, out _)) return false;
        int departureEnd = Refuge(departure, length);
        // Without a usable refuge the whole remaining route must be clear.
        var requested = departure.Take(departureEnd + 1).Select(s => s.Location.segment.id).ToHashSet();
        // Guard the full staging footprint, including adjacent short segments.
        // Check both orientations because native placement/end conventions differ
        // among flipped engines and steam/tender pairs.
        foreach (float distance in new[] { length, -length }) {
            var end = Graph.Shared.LocationByMoving(spawn, distance, checkSwitchAgainstMovement: false, stopAtEndOfTrack: true);
            if (NpcTrainOperations.Route(spawn, end, out var route, out _)) requested.UnionWith(route.Select(s => s.Location.segment.id));
        }
        var firm = new Dictionary<string, HashSet<string>>();
        var stops = new Dictionary<string, (List<RouteSearch.Step> route, int end, int entrance)>();
        foreach (var owner in reservations.Owners) {
            if (!requested.Any(s => reservations.OwnerOf(s) == owner)) continue;
            if (!TrainController.Shared.TryGetCarForId(owner, out var car) || car is not BaseLocomotive lead || lead.IsInBardo) continue;
            var own = lead.EnumerateCoupled().ToList();
            var ahead = Ahead(lead.AutoEngineerPlanner, lead.AutoEngineerPlanner.StartLocation());
            if (ahead.Count == 0) continue;
            float braking = lead.AutoEngineerPlanner._engineer.CalculateLookaheadDistance() + 100;
            int offset = 0;
            while (offset < ahead.Count - 1 && ahead.Take(offset).Sum(s => s.Distance) < braking) offset++;
            int end = Refuge(ahead.Skip(offset).ToList(), TrainController.ApproximateLength(own.Select(c => c.Descriptor())), out int entrance) + offset;
            if (entrance < 0) continue; // Cannot shorten authority without a safe refuge.
            entrance += offset;
            var keep = ahead.Take(end + 1).Select(s => s.Location.segment.id).ToHashSet();
            keep.UnionWith(own.SelectMany(c => new[] { c.LocationA.segment.id, c.LocationB.segment.id }));
            firm[owner] = keep;
            stops[owner] = (ahead, end, entrance);
        }
        var segments = Graph.Shared.Segments.Where(s => requested.Contains(s.id)).ToList();
        // Native segment occupancy includes the middle of long vehicles too.
        if (TrainController.Shared.CarsOnSegments(segments).Any()) return false;
        if (!reservations.TryAdmit(requested, Array.Empty<string>(), firm)) return false;
        foreach (var pair in stops) {
            // Only replace a far grant when admission actually shortened it.
            var stop = pair.Value;
            if (!grantedEnds.TryGetValue(pair.Key, out var previous) || !stop.route.Skip(stop.end + 1).Any(s => s.Location.segment == previous.segment)) continue;
            grantedEnds[pair.Key] = StopAlongRoute(stop.route, stop.end, Math.Max(50, Graph.Shared.CalculateFoulingDistance(stop.route[stop.end].Node) + 25));
            exitNodes[pair.Key] = stop.route[stop.end].Node;
            sidingTracks[pair.Key] = (stop.route[stop.entrance].Node, stop.route.Skip(stop.entrance + 1).Take(stop.end - stop.entrance).Select(s => s.Location.segment.id).ToHashSet());
            ModDiagnosticLog.Write("ROUTING", pair.Key + ": distant authority shortened for spawn; next refuge=" + stop.route[stop.end].Node.id);
        }
        return true;
    }

    private static void RefreshBlocks()
    {
        if (Time.realtimeSinceStartup < nextBlocks) return;
        blocks = UnityEngine.Object.FindObjectsByType<CTCBlock>(FindObjectsSortMode.None);
        ctcSegments = blocks.Where(b => b.isActiveAndEnabled).SelectMany(b => b.Spans).SelectMany(s => s.GetSegments()).ToHashSet();
        nextBlocks = Time.realtimeSinceStartup + 5;
    }

    internal static void Reset()
    {
        graph = null; reservations.Clear(); retries.Clear(); notices.Clear(); refuges.Clear(); grantedEnds.Clear(); exitNodes.Clear(); sidingTracks.Clear(); yielding.Clear(); reverseRejected.Clear(); blocks = Array.Empty<CTCBlock>(); ctcSegments.Clear(); nextBlocks = 0;
    }

    internal static void Recalculate(BaseLocomotive lead)
    {
        var planner = lead.AutoEngineerPlanner;
        // Native search ignores the train's own cars, checks length/curvature and
        // occupancy, and respects CTC switch boundaries. Force defeats waypoint caching.
        planner.UpdateWaypointRouteIfNeeded(force: true);
        if (ThroughTrafficGuard.IsGenerated(lead) && planner._route?.Count > 0 && planner._route[0].Location.segment == lead.LocationF.segment &&
            planner._route[0].Location.end != lead.LocationF.end) {
            // A bypass must keep the controlling engine in front of the train.
            planner.ClearRoute();
            planner._manualStopDistance = 0;
            if (reverseRejected.Add(lead.id)) ModDiagnosticLog.Write("ROUTING", lead.DisplayName + ": no forward bypass; waiting for track to clear");
        }
        else reverseRejected.Remove(lead.id);
        retries[lead.id] = Time.realtimeSinceStartup + 15;
    }

    internal static void Prepare(AutoEngineerPlanner planner)
    {
        if (!StateManager.IsHost || TweaksAndThingsPlugin.Instance?.IsEnabled != true || !ThroughTrafficGuard.IsGenerated(planner._locomotive)) return;
        if (planner._orders.Mode != AutoEngineerMode.Waypoint || !planner._locomotive.IsStopped()) return;
        if (grantedEnds.ContainsKey(planner._locomotive.id) && !yielding.Contains(planner._locomotive.id)) return;
        if (retries.TryGetValue(planner._locomotive.id, out var retry) && Time.realtimeSinceStartup < retry) return;
        var previous = planner._route?.ToList();
        Recalculate(planner._locomotive);
        if (!yielding.Contains(planner._locomotive.id)) return;
        if (planner._route == null || planner._route.Count == 0) {
            // Keep the original refuge route if native search offers no forward bypass.
            if (previous != null) planner._route = previous;
            return;
        }
        if (previous != null && !previous.Select(s => s.Location.segment.id).SequenceEqual(planner._route.Select(s => s.Location.segment.id))) {
            // A stopped train may take a native occupancy-aware bypass. The new
            // corridor still has to pass Protect before any movement is allowed.
            string id = planner._locomotive.id;
            reservations.RetainOccupied(id, planner._locomotive.EnumerateCoupled().SelectMany(c => new[] { c.LocationA.segment.id, c.LocationB.segment.id }));
            grantedEnds.Remove(id); exitNodes.Remove(id); sidingTracks.Remove(id); yielding.Remove(id);
        }
    }

    internal static void Protect(AutoEngineer engineer, AutoEngineer.Targets targets)
    {
        if (!StateManager.IsHost || TweaksAndThingsPlugin.Instance?.IsEnabled != true || Graph.Shared == null || engineer.Locomotive == null) return;
        if (graph != Graph.Shared) { Reset(); graph = Graph.Shared; }
        var lead = engineer.Locomotive;
        var own = lead.EnumerateCoupled().ToHashSet();
        var ownSegments = own.SelectMany(c => new[] { c.LocationA.segment.id, c.LocationB.segment.id }).ToHashSet();
        // Discard abandoned future claims but preserve occupied sections for parked tails.
        foreach (var id in reservations.Owners) {
            if (!TrainController.Shared.TryGetCarForId(id, out var car) || car is not BaseLocomotive loco || loco.IsInBardo) {
                reservations.Remove(id); grantedEnds.Remove(id); exitNodes.Remove(id); continue;
            }
            if (loco.AutoEngineerPlanner._orders.Mode == AutoEngineerMode.Off || loco.AutoEngineerPlanner._orders.Mode == AutoEngineerMode.Yard)
                reservations.RetainOccupied(id, loco.EnumerateCoupled().SelectMany(c => new[] { c.LocationA.segment.id, c.LocationB.segment.id }));
        }
        if (targets.Mode != AutoEngineerMode.Waypoint && targets.Mode != AutoEngineerMode.Road) { reservations.RetainOccupied(lead.id, ownSegments); return; }
        RefreshBlocks();
        bool ctc = CTCPanelController.Shared != null && CTCPanelController.Shared.SystemMode == SystemMode.CTC;
        if (ctc) {
            // Manual CTC authority belongs to the dispatcher. Native AE still
            // brakes for every signal and locked switch; do not manufacture
            // compulsory siding stops or retain automatic-mode throat locks.
            reservations.Remove(lead.id); grantedEnds.Remove(lead.id); exitNodes.Remove(lead.id); sidingTracks.Remove(lead.id); yielding.Remove(lead.id);
            Notice(lead, "Manual CTC: following dispatcher signals");
            return;
        }
        var planner = lead.AutoEngineerPlanner;
        if (planner._orders.MaxSpeedMph <= 0) { reservations.RetainOccupied(lead.id, ownSegments); grantedEnds.Remove(lead.id); return; }
        var start = planner.StartLocation();
        var route = Ahead(planner, start);
        if (route.Count == 0) {
            reservations.RetainOccupied(lead.id, ownSegments);
            if (exitNodes.ContainsKey(lead.id)) AddStop(targets, 0, "Siding hold: route unavailable");
            return;
        }
        int searchStart = 0;
        float? refugeStopDistance = null;
        // Check the onward corridor while still approaching the granted refuge.
        // Only a failed onward grant latches a full-stop meet/yield requirement.
        if (grantedEnds.TryGetValue(lead.id, out var granted)) {
            int grantedIndex = route.FindIndex(s => s.Location.segment == granted.segment && s.Location.end == granted.end);
            if (exitNodes.TryGetValue(lead.id, out var exit)) {
                float remaining = grantedIndex < 0 ? 0 : route.Take(grantedIndex).Sum(s => s.Distance) +
                    route[grantedIndex].Distance - (route[grantedIndex].Location.distance - granted.distance);
                if (!yielding.Contains(lead.id) && grantedIndex >= 0 &&
                    remaining > Math.Max(500, engineer.CalculateLookaheadDistance() + 100) && lookaheadDepth == 0) return;
                bool tailClear = own.All(c => Vector3.Distance(c.LocationA.GetPosition(), exit.transform.GamePosition()) > Graph.Shared.CalculateFoulingDistance(exit) + 5 &&
                    Vector3.Distance(c.LocationB.GetPosition(), exit.transform.GamePosition()) > Graph.Shared.CalculateFoulingDistance(exit) + 5);
                if (sidingTracks.TryGetValue(lead.id, out var siding))
                    tailClear &= ownSegments.IsSubsetOf(siding.segments) && own.All(c =>
                        Vector3.Distance(c.LocationA.GetPosition(), siding.entrance.transform.GamePosition()) > Graph.Shared.CalculateFoulingDistance(siding.entrance) + 5 &&
                        Vector3.Distance(c.LocationB.GetPosition(), siding.entrance.transform.GamePosition()) > Graph.Shared.CalculateFoulingDistance(siding.entrance) + 5);
                int throatIndex = route.FindIndex(s => s.Node == exit);
                bool opposing = OpposingApproach(lead, route.Take(throatIndex < 0 ? route.Count : throatIndex + 1).ToList(), exit);
                refugeStopDistance = Math.Max(0, remaining);
                if (grantedIndex < 0 || yielding.Contains(lead.id) && !NpcSidingHoldPolicy.MayRequestDeparture(remaining, lead.VelocityMphAbs, tailClear, opposing)) {
                    string state = lead.IsStopped() ? "Holding in protected passing siding" : "Taking protected passing siding";
                    AddStop(targets, Math.Max(0, remaining), state);
                    Notice(lead, state + "; exit=" + exit.id + "; opposing=" + opposing + "; tailClear=" + tailClear + "; route=" + (grantedIndex >= 0),
                        $"; stopRemaining={remaining:N1} m; speed={lead.VelocityMphAbs:N1} mph");
                    return;
                }
                int throat = route.FindIndex(s => s.Node == exit);
                // Include the exit switch as the entrance to the NEXT corridor;
                // otherwise the search repeatedly rediscovers the same siding.
                searchStart = throat < 0 ? 0 : throat;
            }
        }
        int end = Refuge(route.Skip(searchStart).ToList(), TrainController.ApproximateLength(own.Select(c => c.Descriptor())), out int entrance) + searchStart;
        if (entrance >= 0) entrance += searchStart;
        var corridor = route.Take(end + 1).Select(s => s.Location.segment).Distinct().ToList();
        var occupied = TrainController.Shared.CarsOnSegments(corridor).Where(c => c != null && !own.Contains(c)).ToList();
        var blocked = occupied.SelectMany(c => new[] { c.LocationA.segment.id, c.LocationB.segment.id }).ToHashSet();
        // Intent matters: two empty approaches must not both be cleared toward
        // the same throat before either train actually occupies that switch.
        if (OpposingApproach(lead, route.Take(end + 1).ToList(), null)) blocked.UnionWith(corridor.Select(s => s.id));
        // Include native thrown/unlocked-switch and forced occupancy, even when no car is present.
        foreach (var block in blocks.Where(b => ctc && b.isActiveAndEnabled && b.IsOccupied))
        {
            var segments = block.Spans.SelectMany(s => s.GetSegments()).Where(corridor.Contains).ToList();
            var cars = block.CarsInBlock();
            if (segments.Count > 0 && (block._testForceOccupied || block._thrownNodeIds.Count > 0 || block._unlockedNodeIds.Count > 0 || cars.Count == 0 || cars.Any(c => !own.Contains(c))))
                blocked.UnionWith(segments.Select(s => s.id));
        }
        bool clear = reservations.TryReserve(lead.id, corridor.Select(s => s.id), ownSegments, blocked);
        if (clear) {
            yielding.Remove(lead.id);
            var finish = route[end].Location;
            float clearance = route[end].Node == null ? 25 : Math.Max(50, Graph.Shared.CalculateFoulingDistance(route[end].Node) + 25);
            grantedEnds[lead.id] = StopAlongRoute(route, end, clearance);
            if (end < route.Count - 1 && route[end].Node != null) {
                exitNodes[lead.id] = route[end].Node;
                if (entrance >= 0) sidingTracks[lead.id] = (route[entrance].Node, route.Skip(entrance + 1).Take(end - entrance).Select(s => s.Location.segment.id).ToHashSet());
            }
            else { exitNodes.Remove(lead.id); sidingTracks.Remove(lead.id); }
            if (exitNodes.ContainsKey(lead.id) && lookaheadDepth == 0) {
                // Check one more hop in this SAME planner update, before native
                // braking targets are handed to the engineer.
                lookaheadDepth++;
                try { Protect(engineer, targets); }
                finally { lookaheadDepth--; }
                return;
            }
        }
        string reason = clear ? "clear" : "Holding for clear track to next passing siding";
        if (!clear) {
            float stopDistance = refugeStopDistance ?? 0;
            if (refugeStopDistance.HasValue) yielding.Add(lead.id);
            else if (grantedEnds.TryGetValue(lead.id, out var finish) && finish.segment == start.segment && finish.end == start.end)
                stopDistance = Math.Max(0, finish.distance - start.distance);
            else if (ctc && !ctcSegments.Contains(start.segment)) {
                foreach (var step in route) { if (ctcSegments.Contains(step.Location.segment)) break; stopDistance += step.Distance; }
                stopDistance = Math.Max(0, stopDistance - 25);
            }
            // Keep the granted path into the refuge while braking toward it;
            // releasing it here would let the opposing train steal its throat.
            if (!refugeStopDistance.HasValue) reservations.RetainOccupied(lead.id, ownSegments);
            AddStop(targets, stopDistance, reason);
        }
        string owners = string.Join(",", reservations.Owners.Where(id => id != lead.id && corridor.Any(s => reservations.Covers(id, new[] { s.id }))).Select(id => TrainController.Shared.TryGetCarForId(id, out var c) ? c.DisplayName : id));
        Notice(lead, reason + (clear ? "" : "; claims=" + owners),
            "; corridor=" + corridor.Count + "; refuge=" + (entrance >= 0 ? route[end].Node?.id : "route endpoint (no usable protected siding)") +
            "; trainLength=" + TrainController.ApproximateLength(own.Select(c => c.Descriptor())).ToString("N0") + " m; occupiedBy=" + string.Join(",", occupied.Select(c => c.DisplayName).Distinct().Take(4)));
    }

    private static void AddStop(AutoEngineer.Targets targets, float distance, string reason)
    {
        targets.AllTargets.Add(new AutoEngineer.Targets.Target(0, distance, reason));
        targets.AllTargets.Sort((a, b) => a.Distance.CompareTo(b.Distance));
    }

    internal static bool MayThrowSwitch(AutoEngineerPlanner planner, TrackNode node) =>
        !StateManager.IsHost || TweaksAndThingsPlugin.Instance?.IsEnabled != true ||
        CTCPanelController.Shared?.SystemMode == SystemMode.CTC ||
        !exitNodes.TryGetValue(planner._locomotive.id, out var exit) || node != exit;

    private static void Notice(BaseLocomotive lead, string reason, string detail = "")
    {
        if (notices.TryGetValue(lead.id, out var previous) && previous == reason) return;
        notices[lead.id] = reason;
        ModDiagnosticLog.Write("ROUTING", lead.DisplayName + ": " + reason + detail);
    }

    private static Location StopAlongRoute(List<RouteSearch.Step> route, int end, float clearance)
    {
        // Fouling clearance may cross several short switch segments. Clamping
        // only the last segment to zero can pin the stop on a switch itself.
        for (int i = end; i >= 0; i--) {
            var step = route[i];
            if (clearance <= step.Distance) return new Location(step.Location.segment, step.Location.distance - clearance, step.Location.end);
            clearance -= step.Distance;
        }
        return new Location(route[0].Location.segment, route[0].Location.distance - route[0].Distance, route[0].Location.end);
    }

    private static bool OpposingApproach(BaseLocomotive lead, List<RouteSearch.Step> route, TrackNode? exit)
    {
        var directions = route.GroupBy(s => s.Location.segment.id).ToDictionary(g => g.Key, g => g.First().Location.end);
        foreach (var other in TrainController.Shared.Cars.OfType<BaseLocomotive>().Where(c => !c.IsInBardo && !c.IsMuEnabled && c != lead)) {
            if (lead.EnumerateCoupled().Contains(other)) continue;
            var planner = other.AutoEngineerPlanner;
            if (planner._orders.Mode != AutoEngineerMode.Waypoint && planner._orders.Mode != AutoEngineerMode.Road) continue;
            if (planner._orders.MaxSpeedMph <= 0 || planner._coupledCarsCached.Count == 0) continue;
            var ahead = Ahead(planner, planner.StartLocation());
            // A train already held at its own refuge has yielded; don't create
            // reciprocal intent locks after both trains reach separate safe tracks.
            if (grantedEnds.TryGetValue(other.id, out var hold) && exitNodes.ContainsKey(other.id)) {
                int index = ahead.FindIndex(s => s.Location.segment == hold.segment && s.Location.end == hold.end);
                if (index >= 0) {
                    float remaining = ahead.Take(index).Sum(s => s.Distance) + ahead[index].Distance - (ahead[index].Location.distance - hold.distance);
                    if (remaining >= -2 && remaining <= 25 && other.VelocityMphAbs < 0.1f) continue;
                    ahead = ahead.Take(index + 1).ToList();
                }
            }
            else {
                ahead = ahead.Take(Refuge(ahead, TrainController.ApproximateLength(other.EnumerateCoupled().Select(c => c.Descriptor()))) + 1).ToList();
                var shared = ahead.Where(s => directions.ContainsKey(s.Location.segment.id)).Select(s => s.Location.segment.id).Distinct().ToList();
                // A newcomer's ungranted intention cannot revoke an existing
                // grant. Physical occupancy is still checked independently.
                if (shared.Count > 0 && reservations.Covers(lead.id, shared)) continue;
                // Deterministic priority breaks simultaneous approach ties. An
                // existing reservation still outranks an ungranted contender.
                if (!grantedEnds.ContainsKey(lead.id) && string.CompareOrdinal(lead.id, other.id) < 0) continue;
            }
            if (ahead.Any(s => directions.TryGetValue(s.Location.segment.id, out var direction) && direction != s.Location.end)) return true;
            if (exit != null && ahead.Any(s => s.Node == exit)) return true;
        }
        return false;
    }

    private static List<RouteSearch.Step> Ahead(AutoEngineerPlanner planner, Location start)
    {
        if (planner._orders.Mode == AutoEngineerMode.Waypoint && planner._route != null) {
            var route = planner._route.Skip(planner._startStepIndex).ToList();
            int first = route.FindIndex(s => s.Location.segment == start.segment);
            if (first < 0) return new List<RouteSearch.Step>();
            route = route.Skip(first).ToList();
            var step = route[0];
            float remaining = Math.Max(0, step.Location.distance - start.WithEnd(step.Location.end).distance);
            route[0] = step.Node == null ? new RouteSearch.Step(step.Location, step.Direction, remaining, Graph.Shared, step.Flags) :
                new RouteSearch.Step(step.Location, step.Node, step.Direction, remaining, Graph.Shared, step.Flags);
            return route;
        }
        // ROAD follows the currently lined track; it must not reserve an imagined waypoint route.
        var result = new List<RouteSearch.Step>();
        var seen = new HashSet<string>();
        var cursor = start;
        while (cursor.IsValid && seen.Add(cursor.segment.id)) {
            float remaining = cursor.segment.GetLength() - cursor.distance;
            var finish = new Location(cursor.segment, cursor.segment.GetLength(), cursor.end);
            result.Add(new RouteSearch.Step(finish, cursor.EndIsA ? cursor.segment.a : cursor.segment.b, StepDirection.Out, remaining, Graph.Shared));
            var next = Graph.Shared.LocationByMoving(cursor, remaining + 0.05f, checkSwitchAgainstMovement: true, stopAtEndOfTrack: true);
            if (next.segment == cursor.segment) break;
            cursor = next;
        }
        return result;
    }

    private static int Refuge(List<RouteSearch.Step> route, float length) => Refuge(route, length, out _);

    private static int Refuge(List<RouteSearch.Step> route, float length, out int entrance)
    {
        entrance = -1;
        string key = string.Join(",", route.Select(s => s.Location.segment.id + ":" + s.Location.end)) + ":" + Math.Ceiling(length);
        if (refuges.TryGetValue(key, out var cached) && cached.expires > Time.realtimeSinceStartup) { entrance = cached.entrance; return cached.end; }
        var segments = Graph.Shared.Segments.Where(s => s.GroupEnabled && s.turntable == null).ToDictionary(s => s.id);
        var nodes = segments.Values.SelectMany(s => new[] { s.a, s.b }).Distinct().ToDictionary(n => n.id);
        var edges = segments.Values.Select(s => new NpcPassingSidingPolicy.Edge(s.id, s.a.id, s.b.id, s.GetLength())).ToList();
        var ids = route.Select(s => s.Location.segment.id).ToHashSet();
        var switches = route.Select((s, i) => (step: s, index: i)).Where(p => p.step.Node != null && Graph.Shared.DecodeSwitchAt(p.step.Node, out _, out _, out _)).ToList();
        var passingPairs = new Dictionary<(string, string), double>();
        NpcPassingSidingPolicy.Longest(edges, ids,
            (n, x, y) => nodes[n].SegmentCanReachSegment(segments[x], segments[y]),
            junctions: switches.Select(p => p.step.Node.id).ToHashSet(),
            foulingClearance: (a, b) => Math.Max(20, Graph.Shared.CalculateFoulingDistance(nodes[a]) + Graph.Shared.CalculateFoulingDistance(nodes[b])),
            found: (a, b, usable) => { if (!passingPairs.TryGetValue((a, b), out var old) || usable > old) passingPairs[(a, b)] = usable; });
        int end = route.Count - 1;
        for (int b = 1; b < switches.Count; b++) {
            for (int a = 0; a < b; a++) {
                var left = switches[a]; var right = switches[b];
                // Automatic operation may advance to the next protected refuge
                // even when the train starts outside signal coverage.
                if (!ctcSegments.Contains(right.step.Location.segment)) continue;
                float mainLength = route.Skip(left.index + 1).Take(right.index - left.index).Sum(s => s.Distance);
                float clearance = Math.Max(20, Graph.Shared.CalculateFoulingDistance(left.step.Node) + Graph.Shared.CalculateFoulingDistance(right.step.Node)) + 50;
                if (mainLength - clearance < length) continue;
                if (!passingPairs.TryGetValue((left.step.Node.id, right.step.Node.id), out var alternate) || alternate < length) continue;
                end = right.index; entrance = left.index; break;
            }
            if (end != route.Count - 1) break;
        }
        if (refuges.Count > 128) refuges.Clear();
        refuges[key] = (Time.realtimeSinceStartup + 10, end, entrance);
        return end;
    }
}
