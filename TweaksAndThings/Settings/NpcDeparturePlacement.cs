using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Track;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcDeparturePlacement
{
    internal static bool Fits(Location front, float length, HashSet<string>? ownCars = null)
    {
        if (ownCars == null) return NpcTrainOperations.CanPlaceAt(front, length);
        var found = new HashSet<Car>();
        try
        {
            for (float distance = 0; distance <= length; distance += 1)
            {
                var point = Graph.Shared.LocationByMoving(front, -distance, checkSwitchAgainstMovement: true);
                if (!point.IsValid || !point.segment.GroupEnabled) return false;
                TrainController.Shared.CheckForCarsAtPoint(point.GetPosition(), 0, found, point);
                if (found.Any(c => !ownCars.Contains(c.id))) return false;
            }
            return true;
        }
        catch (EndOfTrack) { return false; }
        catch (SwitchAgainstMovement) { return false; }
    }

    internal static bool Find(ref Location spawn, Location target, float length, HashSet<string>? ownCars = null)
    {
        if (!spawn.IsValid || !target.IsValid || !spawn.segment.GroupEnabled) return false;
        spawn = NpcTrainOperations.FacingRoute(spawn, target);
        var original = spawn;
        // Move the locomotive inward from the terminal, reserving space behind it for the ENTIRE train.
        var seeds = new List<Location>();
        var segments = Graph.Shared.Segments.Where(s => s.GroupEnabled).ToList();
        var degree = segments.SelectMany(s => new[] { s.a, s.b }).GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
        if (degree.TryGetValue(original.segment.a, out int degreeA) && degreeA == 1) seeds.Add(new Location(original.segment, 0, TrackSegment.End.A));
        if (degree.TryGetValue(original.segment.b, out int degreeB) && degreeB == 1) seeds.Add(new Location(original.segment, 0, TrackSegment.End.B));
        if (seeds.Count == 0) seeds.Add(original);
        seeds.AddRange(segments.SelectMany(s => new[] { TrackSegment.End.A, TrackSegment.End.B }
            .Where(end => degree[end == TrackSegment.End.A ? s.a : s.b] == 1).Select(end => new Location(s, 0, end)))
            .OrderBy(p => Vector3.SqrMagnitude(p.GetPosition() - original.GetPosition())));
        foreach (var seed in seeds)
        {
            try
            {
                var inward = NpcTrainOperations.FacingRoute(seed, target);
                if (!NpcTrainOperations.Route(inward, target, out _, out float distance) || distance < 200) continue;
                var offset = NpcSpawnSearch.FirstFit(d =>
                {
                    try
                    {
                        var point = Graph.Shared.LocationByMoving(inward, (float)d, checkSwitchAgainstMovement: false);
                        return Fits(point, length + 20, ownCars);
                    }
                    catch (EndOfTrack) { return false; }
                    catch (SwitchAgainstMovement) { return false; }
                }, Math.Max(0, distance - 200));
                if (!offset.HasValue) continue;
                var candidate = Graph.Shared.LocationByMoving(inward, (float)offset.Value, checkSwitchAgainstMovement: false);
                candidate = NpcTrainOperations.FacingRoute(candidate, target);
                if (!Fits(candidate, length + 20, ownCars)) continue;
                spawn = candidate;
                ModDiagnosticLog.Write("SPAWN", $"Adjusted departure {Graph.Shared.LocationToString(original)} -> {Graph.Shared.LocationToString(spawn)}; full train {length / 1609.344:N2} mi plus staging clearance.");
                return true;
            }
            catch (EndOfTrack) { }
            catch (SwitchAgainstMovement) { }
        }
        return false;
    }

    internal static void MoveExisting(Location front, List<Car> cars)
    {
        NpcTrainOperations.Trusted(() =>
        {
            var controller = TrainController.Shared;
            var moved = controller.HandleCreateCarsAsTrain(front, cars.Select(c => c.Descriptor()).ToList(), cars.Select(c => c.id).ToList(), null);
            TrainController.ConnectCars(moved);
            TrainController.FillAir(moved);
            moved[0].set.SetVelocity(0f, moved);
            NpcTrainOperations.SyncPlacement(moved);
            foreach (var car in moved) car.SetHandbrake(false);
        });
    }

    internal static bool FaceDeparture(NpcServiceRecord service, Location destination)
    {
        var lead = TrainController.Shared.CarForId(service.LeadId);
        var forward = NpcTrainOperations.FacingRoute(lead.LocationF, destination);
        if (forward.end == lead.LocationF.end) return true;
        var coupled = lead.EnumerateCoupled().ToList();
        var ids = new HashSet<string>(coupled.Select(c => c.id));
        var rear = NpcTrainOperations.FacingRoute(NpcTrainOperations.Tail(service).LocationB.Flipped(), destination);
        float length = TrainController.ApproximateLength(coupled.Select(c => c.Descriptor()));
        if (!Fits(rear, length, ids)) return false;
        var ordered = service.PowerIds.Select(id => TrainController.Shared.CarForId(id)).Where(c => c != null).ToList();
        ordered.AddRange(coupled.Where(c => !service.PowerIds.Contains(c.id)));
        MoveExisting(rear, ordered);
        return true;
    }
}
