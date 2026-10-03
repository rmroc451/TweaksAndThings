using System.Collections.Generic;
using System.Linq;
using Track;
using Track.Search;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcPassingSidings
{
    private static Graph? cachedGraph;
    private static string graphKey = string.Empty;
    private static readonly Dictionary<string, (float limit, float expires)> cache = new();
    internal static float Limit(IEnumerable<RouteSearch.Step> route)
    {
        var steps = route.ToList();
        var segments = Graph.Shared.Segments.Where(s => s.GroupEnabled && s.turntable == null).ToList();
        string enabled = string.Join(",", segments.Select(s => s.id));
        if (cachedGraph != Graph.Shared || enabled != graphKey)
        { cache.Clear(); cachedGraph = Graph.Shared; graphKey = enabled; }
        string key = string.Join(",", steps.Select(s => s.Location.segment.id + ":" + s.Node?.id));
        if (cache.TryGetValue(key, out var found) && found.expires > Time.realtimeSinceStartup) return found.limit;
        if (cache.Count >= 64) cache.Clear();
        var byId = segments.ToDictionary(s => s.id);
        var nodes = segments.SelectMany(s => new[] { s.a, s.b }).Distinct().ToDictionary(n => n.id);
        float limit = (float)NpcPassingSidingPolicy.Longest(segments.Select(s =>
                new NpcPassingSidingPolicy.Edge(s.id, s.a.id, s.b.id, s.GetLength())),
            new HashSet<string>(steps.Select(s => s.Location.segment.id)),
            (node, a, b) => nodes[node].SegmentCanReachSegment(byId[a], byId[b]),
            junctions: new HashSet<string>(steps.Where(s => s.Node != null && Graph.Shared.DecodeSwitchAt(s.Node, out _, out _, out _)).Select(s => s.Node!.id)),
            foulingClearance: (a, b) => System.Math.Max(20, Graph.Shared.CalculateFoulingDistance(nodes[a]) + Graph.Shared.CalculateFoulingDistance(nodes[b])));
        cache[key] = (limit, Time.realtimeSinceStartup + 30);
        return limit;
    }

    internal static float Length(IEnumerable<Model.CarDescriptor> cars) => TrainController.ApproximateLength(cars);
}
