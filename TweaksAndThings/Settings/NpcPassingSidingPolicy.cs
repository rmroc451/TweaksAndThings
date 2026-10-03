using System;
using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class NpcPassingSidingPolicy
{
    internal readonly struct Edge
    {
        internal readonly string Id, A, B;
        internal readonly double Length;
        internal Edge(string id, string a, string b, double length) { Id = id; A = a; B = b; Length = length; }
        internal string Other(string node) => node == A ? B : A;
    }

    // A siding leaves the selected route at one switch and rejoins at another.
    // Follow through branch junctions too; dead-end spurs are not passing tracks.
    internal static double Longest(IEnumerable<Edge> graph, HashSet<string> route,
        Func<string, string, string, bool> canTraverse, double clearance = 20,
        HashSet<string>? junctions = null, Func<string, string, double>? foulingClearance = null,
        Action<string, string, double>? found = null)
    {
        var edges = graph.ToList();
        var adjacency = edges.SelectMany(e => new[] { (node: e.A, edge: e), (node: e.B, edge: e) })
            .GroupBy(e => e.node).ToDictionary(g => g.Key, g => g.Select(e => e.edge).ToList());
        var routeNodes = new HashSet<string>(edges.Where(e => route.Contains(e.Id)).SelectMany(e => new[] { e.A, e.B }));
        if (junctions != null) routeNodes.IntersectWith(junctions);
        double longest = 0;
        foreach (var start in routeNodes)
        {
            if (!adjacency.TryGetValue(start, out var incident) || incident.Count < 3) continue;
            foreach (var first in incident.Where(e => !route.Contains(e.Id)))
            {
                if (!incident.Any(e => route.Contains(e.Id) && canTraverse(start, e.Id, first.Id))) continue;
                var pending = new List<(string node, Edge incoming, double length)> { (first.Other(start), first, first.Length) };
                var best = new Dictionary<(string node, string edge), double>();
                var endpoints = new Dictionary<string, double>();
                while (pending.Count > 0)
                {
                    int index = 0;
                    for (int i = 1; i < pending.Count; i++) if (pending[i].length < pending[index].length) index = i;
                    var state = pending[index];
                    pending.RemoveAt(index);
                    var key = (state.node, state.incoming.Id);
                    if (best.TryGetValue(key, out double previous) && previous <= state.length) continue;
                    best[key] = state.length;
                    if (routeNodes.Contains(state.node))
                    {
                        if (state.node != start && adjacency[state.node].Count >= 3 &&
                            adjacency[state.node].Any(e => route.Contains(e.Id) && canTraverse(state.node, state.incoming.Id, e.Id)) &&
                            (!endpoints.TryGetValue(state.node, out double known) || state.length < known))
                            endpoints[state.node] = state.length;
                        continue;
                    }
                    foreach (var next in adjacency[state.node])
                        if (next.Id != state.incoming.Id && !route.Contains(next.Id) && canTraverse(state.node, state.incoming.Id, next.Id))
                            pending.Add((next.Other(state.node), next, state.length + next.Length));
                }
                foreach (var endpoint in endpoints) {
                    double usable = Math.Max(0, endpoint.Value - (foulingClearance?.Invoke(start, endpoint.Key) ?? clearance));
                    longest = Math.Max(longest, usable);
                    found?.Invoke(start, endpoint.Key, usable);
                }
            }
        }
        return longest;
    }
}
