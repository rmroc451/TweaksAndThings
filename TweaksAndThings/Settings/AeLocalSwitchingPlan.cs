using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Track;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class AeLocalSwitchingPlan
{
    // Coupler topology, rather than the engine's facing or enumeration order,
    // defines the accessible prefix. Never extract a middle car magically.
    internal static List<Car> FromEnd(Car endCar)
    {
        var ordered = new List<Car>();
        Car? previous = null, current = endCar;
        while (current != null && !ordered.Contains(current))
        {
            ordered.Add(current);
            var next = new[] { Car.LogicalEnd.A, Car.LogicalEnd.B }.Select(current.CoupledTo)
                .FirstOrDefault(c => c != null && c != previous);
            previous = current; current = next;
        }
        return ordered;
    }
    internal static List<Car> PowerFirst(AeLocalJob job, BaseLocomotive lead)
    {
        var coupled = lead.EnumerateCoupled().ToList();
        var end = coupled.FirstOrDefault(c => job.PowerIds.Contains(c.id) &&
            (c.CoupledTo(Car.LogicalEnd.A) == null || c.CoupledTo(Car.LogicalEnd.B) == null));
        return end == null ? coupled : FromEnd(end);
    }
    internal static double RouteCost(Location start, Location target, out string detail)
    {
        detail = "disconnected";
        if (!NpcTrainOperations.Route(start, target, out var route, out float distance)) return double.PositiveInfinity;
        int reversals = 0, facing = 0, trailing = 0;
        for (int i = 1; i < route.Count; i++)
        {
            if (route[i].Direction != route[i - 1].Direction) reversals++;
            var node = route[i].Node;
            if (node == null || !Graph.Shared.DecodeSwitchAt(node, out var approach, out _, out _)) continue;
            if (route[i - 1].Location.segment == approach) facing++; else trailing++;
        }
        detail = $"{distance / 1609.344:F2} mi; {facing} facing/{trailing} trailing turnouts; {reversals} reversals";
        return distance + reversals * 150 + facing * 20;
    }
}
