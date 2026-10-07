using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Model.Ops;
using Game.Messages;
using Track;

namespace RMROC451.TweaksAndThings;

internal static class NpcSetoutPlacement
{
    internal static List<(Location location, List<CarDescriptor> descriptors)>? Plan(NpcServiceRecord service,
        Interchange interchange, List<CarDescriptor> descriptors, List<string> ids, HashSet<string>? ownCars = null)
    {
        var controller = TrainController.Shared;
        var lead = controller.CarForId(service.LeadId);
        var destination = Graph.Shared.ResolveLocationString(service.Spawn);
        var reserved = new Dictionary<string, List<(float from, float to, TrackSegment.End end)>>();
        var planned = new Dictionary<string, List<float>>();
        // Reserve both potential departure ends: power is turned to the front
        // of the return consist after pickups. No setout may cut off either path.
        foreach (var start in new[] { lead.LocationF, NpcTrainOperations.Tail(service).LocationB }) {
            if (!NpcTrainOperations.Route(start, destination, out var route, out _)) return null;
            foreach (var step in route) {
                string key = step.Location.segment.id;
                if (!reserved.TryGetValue(key, out var intervals)) reserved[key] = intervals = new();
                float clearance = step.Node == null ? 25 : Math.Max(25, NpcTrackClearance.Fouling(step.Node) + 10);
                intervals.Add((step.Location.distance - step.Distance - clearance, step.Location.distance + clearance, step.Location.end));
            }
        }
        bool Safe(Location front, float length, TrackSpan span)
        {
            try {
                for (float distance = 0; distance <= length + 0.25f; distance += 0.5f) {
                    var point = Graph.Shared.LocationByMoving(front, -distance, checkSwitchAgainstMovement: true);
                    if (!span.Contains(point)) return false;
                    if (planned.TryGetValue(point.segment.id, out var positions) &&
                        positions.Any(p => Math.Abs(p - point.WithEnd(TrackSegment.End.A).distance) < 0.5f)) return false;
                    if (reserved.TryGetValue(point.segment.id, out var intervals) && intervals.Any(i => {
                        float position = point.WithEnd(i.end).distance;
                        return position >= i.from && position <= i.to;
                    })) return false;
                }
                return NpcDeparturePlacement.Fits(front, length + 0.25f, ownCars);
            }
            catch (EndOfTrack) { return false; }
            catch (SwitchAgainstMovement) { return false; }
        }
        // Keep the largest contiguous cut that fits together. Virtual footprints
        // stop later cuts from reusing earlier space without uncoupling every car.
        var spans = interchange.TrackSpans.OrderBy(s => new[] { s.lower, s.upper }
            .Where(p => p.HasValue).Select(p => UnityEngine.Vector3.SqrMagnitude(
                p!.Value.GetPosition() - lead.LocationF.GetPosition())).DefaultIfEmpty(float.MaxValue).Min()).ToList();
        var result = new List<(Location location, List<CarDescriptor> descriptors)>();
        for (int index = 0; index < descriptors.Count;)
        {
            float length = 0;
            List<CarDescriptor> cut = new();
            Location? chosen = null;
            for (int count = descriptors.Count - index; count > 0 && !chosen.HasValue; count--)
            {
            cut = descriptors.Skip(index).Take(count).ToList();
            length = TrainController.ApproximateLength(cut);
            foreach (var span in spans)
            {
                // First try appending beside an exposed end of existing freight.
                var compact = TrainPlacementHelper.FindLocationForCut(controller, span.GetSegments().ToList(), length, buffer: 1);
                if (compact.HasValue && Safe(compact.Value, length, span)) chosen = compact;
                if (!chosen.HasValue)
                foreach (var boundary in new[] { span.lower, span.upper }.Where(p => p.HasValue)
                    .OrderBy(p => UnityEngine.Vector3.SqrMagnitude(p!.Value.GetPosition() - lead.LocationF.GetPosition())))
                {
                    try
                    {
                        var inward = boundary!.Value;
                        if (!span.Contains(Graph.Shared.LocationByMoving(inward, 1, checkSwitchAgainstMovement: true))) inward = inward.Flipped();
                        for (float offset = length + 1; offset < span.Length; offset += 1)
                        {
                            var front = Graph.Shared.LocationByMoving(inward, offset, checkSwitchAgainstMovement: true);
                            if (!span.Contains(front)) break;
                            if (!Safe(front, length, span)) continue;
                            chosen = front;
                            break;
                        }
                    }
                    catch (EndOfTrack) { }
                    catch (SwitchAgainstMovement) { }
                    if (chosen.HasValue) break;
                }
                if (chosen.HasValue) break;
            }
            }
            if (!chosen.HasValue) return null;
            result.Add((chosen.Value, cut));
            index += cut.Count;
            for (float d = 0; d <= length + 0.25f; d += 0.5f)
            {
                var point = Graph.Shared.LocationByMoving(chosen.Value, -d, checkSwitchAgainstMovement: true);
                if (!planned.TryGetValue(point.segment.id, out var positions)) planned[point.segment.id] = positions = new();
                positions.Add(point.WithEnd(TrackSegment.End.A).distance);
            }
        }
        return result;
    }

    internal static void JoinNearby(List<Car> cars, Interchange interchange)
    {
        if (cars.Count == 0) return;
        SecureFreight(cars);
        var newIds = cars.Select(c => c.id).ToHashSet();
        var parked = interchange.TrackSpans.SelectMany(s => TrainController.Shared.CarsOnSpan(s)).Distinct()
            .Where(c => !newIds.Contains(c.id) && c.EnumerateCoupled().All(v => !v.IsLocomotive && v.IsStopped() && !ThroughTrafficGuard.IsGenerated(v) && !AeLocalService.Owns(v))).ToList();
        foreach (var edge in new[] { (car: cars.First(), end: Car.LogicalEnd.A), (car: cars.Last(), end: Car.LogicalEnd.B) })
            foreach (var other in parked)
                foreach (var end in new[] { Car.LogicalEnd.A, Car.LogicalEnd.B }) {
                    if (edge.car.CoupledTo(edge.end) != null || cars.Any(c => c.EnumerateCoupled().Contains(other))) continue;
                    if (other.set.TryGetCoupledCar(other, other.LogicalToEnd(end), out _)) continue;
                    if (!interchange.TrackSpans.Any(s => s.Contains(edge.car.LocationFor(edge.end)) && s.Contains(other.LocationFor(end)))) continue;
                    if (UnityEngine.Vector3.Distance(edge.car.LocationFor(edge.end).GetPosition(), other.LocationFor(end).GetPosition()) > 2) continue;
                    float gap = Graph.Shared.GetDistanceBetweenClose(edge.car.LocationFor(edge.end), other.LocationFor(end));
                    if (Math.Abs(gap) > 2) continue;
                    edge.car.ApplyEndGearChange(edge.end, Car.EndGearStateKey.IsCoupled, boolValue: true);
                    other.ApplyEndGearChange(end, Car.EndGearStateKey.IsCoupled, boolValue: true);
                    edge.car.ApplyEndGearChange(edge.end, Car.EndGearStateKey.IsAirConnected, boolValue: true);
                    other.ApplyEndGearChange(end, Car.EndGearStateKey.IsAirConnected, boolValue: true);
                    edge.car.ApplyEndGearChange(edge.end, Car.EndGearStateKey.Anglecock, 1f);
                    other.ApplyEndGearChange(end, Car.EndGearStateKey.Anglecock, 1f);
                    // End-gear flags alone do not reconcile native integration sets.
                    // Run the same native reconciliation used after placement/coupling.
                    var joined = cars.Concat(other.EnumerateCoupled()).Distinct().ToList();
                    ReconcileSets.Invoke(TrainController.Shared, new object[] { joined });
                    SecureFreight(joined);
                    // Delivered cuts remain secured after joining.
                    NpcTrainOperations.SyncPlacement(joined);
                    ModDiagnosticLog.Write("YARD JOIN", $"Joined setout {edge.car.DisplayName} to {other.DisplayName}; gap={Math.Abs(gap):F2} m; coupled={edge.car.EnumerateCoupled().Contains(other)}.");
                    break;
                }
    }
    private static readonly System.Reflection.MethodInfo ReconcileSets = HarmonyLib.AccessTools.Method(
        typeof(TrainController), "UpdateSets", new[] { typeof(IEnumerable<Car>) }) ?? throw new MissingMethodException("TrainController.UpdateSets(IEnumerable<Car>)");
    internal static void SecureFreight(List<Car> cars) => NpcTrainOperations.Trusted(() =>
        TrainController.Shared.ApplyHandbrakesAsNeeded(cars, PlaceTrainHandbrakes.Automatic));

    internal static bool Tidy(NpcServiceRecord service, Interchange interchange, double now)
    {
        if (now < service.NextTransfer) return false;
        foreach (var id in service.TouchedYardCars.Where(id => !service.TidiedYardCars.Contains(id)).ToList())
        {
            service.TidiedYardCars.Add(id);
            if (!TrainController.Shared.TryGetCarForId(id, out var car) || car.IsInBardo || ThroughTrafficGuard.IsGenerated(car)) continue;
            var cut = car.EnumerateCoupled().ToList();
            if (cut.Any(c => c.IsLocomotive || !c.IsStopped() || ThroughTrafficGuard.IsGenerated(c) || AeLocalService.Owns(c) ||
                !interchange.TrackSpans.Any(s => s.Contains(c.LocationF) && s.Contains(c.LocationR)))) continue;
            foreach (var item in cut) service.TidiedYardCars.Add(item.id);
            var ids = cut.Select(c => c.id).ToList();
            var plan = Plan(service, interchange, cut.Select(c => c.Descriptor()).ToList(), ids, ids.ToHashSet());
            if (plan == null) continue;
            int moved = 0;
            NpcTrainOperations.Trusted(() =>
            {
                int offset = 0;
                foreach (var placement in plan)
                {
                    var placedIds = ids.Skip(offset).Take(placement.descriptors.Count).ToList();
                    // Native PlaceTrain treats the existing cut as an obstacle.
                    // Move existing cars only if this individual footprint is clear
                    // of the other cars that have not yet been moved.
                    if (NpcDeparturePlacement.Fits(placement.location, TrainController.ApproximateLength(placement.descriptors) + 0.25f, placedIds.ToHashSet()))
                    {
                        NpcDeparturePlacement.MoveExisting(placement.location, placedIds.Select(id => TrainController.Shared.CarForId(id)).ToList());
                        moved += placedIds.Count;
                    }
                    offset += placement.descriptors.Count;
                }
                foreach (var item in cut)
                {
                    var stringCars = item.EnumerateCoupled().ToList();
                    if (item != stringCars[0]) continue;
                    JoinNearby(stringCars, interchange);
                }
                NpcTrainOperations.SyncPlacement(cut.SelectMany(c => c.EnumerateCoupled()).Distinct());
            });
            service.NextTransfer = now + 10;
            NpcServiceStore.Save();
            ModDiagnosticLog.Write("YARD TIDY", $"{service.Id}: compacted {moved}/{cut.Count} touched freight cars; blocked footprints left in place; locomotives and moving cuts excluded.");
            return false;
        }
        service.YardTidied = true;
        NpcServiceStore.Save();
        return true;
    }
}
