using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Model.Ops;
using Track;

namespace RMROC451.TweaksAndThings;

internal static class NpcSetoutPlacement
{
    internal static List<(Location location, List<CarDescriptor> descriptors)>? Plan(NpcServiceRecord service,
        Interchange interchange, List<CarDescriptor> descriptors, List<string> ids)
    {
        var controller = TrainController.Shared;
        var lead = controller.CarForId(service.LeadId);
        var destination = Graph.Shared.ResolveLocationString(service.Spawn);
        var reserved = new Dictionary<string, List<(float from, float to, TrackSegment.End end)>>();
        // Reserve both potential departure ends: power is turned to the front
        // of the return consist after pickups. No setout may cut off either path.
        foreach (var start in new[] { lead.LocationF, NpcTrainOperations.Tail(service).LocationB }) {
            if (!NpcTrainOperations.Route(start, destination, out var route, out _)) return null;
            foreach (var step in route) {
                string key = step.Location.segment.id;
                if (!reserved.TryGetValue(key, out var intervals)) reserved[key] = intervals = new();
                float clearance = step.Node == null ? 25 : Math.Max(25, Graph.Shared.CalculateFoulingDistance(step.Node) + 10);
                intervals.Add((step.Location.distance - step.Distance - clearance, step.Location.distance + clearance, step.Location.end));
            }
        }
        bool Safe(Location front, float length, TrackSpan span)
        {
            try {
                for (float distance = 0; distance <= length + 2; distance += 1) {
                    var point = Graph.Shared.LocationByMoving(front, -distance, checkSwitchAgainstMovement: true);
                    if (!span.Contains(point)) return false;
                    if (reserved.TryGetValue(point.segment.id, out var intervals) && intervals.Any(i => {
                        float position = point.WithEnd(i.end).distance;
                        return position >= i.from && position <= i.to;
                    })) return false;
                }
                return NpcDeparturePlacement.Fits(front, length + 2);
            }
            catch (EndOfTrack) { return false; }
            catch (SwitchAgainstMovement) { return false; }
        }
        float fullLength = TrainController.ApproximateLength(descriptors);
        // Native compact-cut finder starts beside exposed ends of parked freight
        // cuts, rather than leaving its normal ten-metre buffer per batch.
        foreach (var span in interchange.TrackSpans) {
            var parked = controller.CarsOnSpan(span).ToList();
            if (!parked.Any() || parked.Any(c => c.EnumerateCoupled().Any(v => v.IsLocomotive || !v.IsStopped() || ThroughTrafficGuard.IsGenerated(v)))) continue;
            var compact = TrainPlacementHelper.FindLocationForCut(controller, span.GetSegments().ToList(), fullLength, buffer: 1);
            if (compact.HasValue && Safe(compact.Value, fullLength, span))
                return new() { (compact.Value, descriptors) };
        }
        // Retain the native fitter's assignment and cut splitting, validating
        // every proposed footprint before making any changes.
        var spans = interchange.TrackSpans.ToList();
        while (spans.Count > 0) {
            var plan = TrainPlacementHelper.FindLocationsForCars(controller, spans, descriptors, ids);
            if (plan == null) return null;
            var bad = plan.Where(p => !spans.Any(s => Safe(p.location, TrainController.ApproximateLength(p.descriptorsAndIds.Select(d => d.descriptor)), s))).ToList();
            if (bad.Count == 0) return plan.Select(p => (p.location, p.descriptorsAndIds.Select(d => d.descriptor).ToList())).ToList();
            if (spans.RemoveAll(s => bad.Any(p => s.Contains(p.location))) == 0) return null;
        }
        return null;
    }

    internal static void JoinNearby(List<Car> cars, Interchange interchange)
    {
        var newIds = cars.Select(c => c.id).ToHashSet();
        var parked = interchange.TrackSpans.SelectMany(s => TrainController.Shared.CarsOnSpan(s)).Distinct()
            .Where(c => !newIds.Contains(c.id) && c.EnumerateCoupled().All(v => !v.IsLocomotive && v.IsStopped() && !ThroughTrafficGuard.IsGenerated(v))).ToList();
        foreach (var edge in new[] { (car: cars.First(), end: Car.LogicalEnd.A), (car: cars.Last(), end: Car.LogicalEnd.B) })
            foreach (var other in parked)
                foreach (var end in new[] { Car.LogicalEnd.A, Car.LogicalEnd.B }) {
                    if (other.set.TryGetCoupledCar(other, other.LogicalToEnd(end), out _)) continue;
                    if (UnityEngine.Vector3.Distance(edge.car.LocationFor(edge.end).GetPosition(), other.LocationFor(end).GetPosition()) > 2) continue;
                    float gap = Graph.Shared.GetDistanceBetweenClose(edge.car.LocationFor(edge.end), other.LocationFor(end));
                    if (Math.Abs(gap) > 2) continue;
                    edge.car.ApplyEndGearChange(edge.end, Car.EndGearStateKey.IsCoupled, boolValue: true);
                    other.ApplyEndGearChange(end, Car.EndGearStateKey.IsCoupled, boolValue: true);
                    // Keep yard cuts braked, with air isolated from the NPC train.
                    NpcTrainOperations.SyncPlacement(cars.Concat(other.EnumerateCoupled()));
                    return;
                }
    }
}
