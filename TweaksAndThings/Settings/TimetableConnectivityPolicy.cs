using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class TimetableConnectivityPolicy
{
    internal readonly struct Edge
    {
        internal readonly string Id, A, B;
        internal readonly bool Enabled, Owned;
        internal Edge(string id, string a, string b, bool enabled, bool owned)
        { Id = id; A = a; B = b; Enabled = enabled; Owned = owned; }
    }

    internal static Dictionary<string, int> Components(IEnumerable<Edge> source, out HashSet<int> owned)
    {
        var edges = source.Where(e => e.Enabled).ToList();
        var nodes = new Dictionary<string, List<Edge>>();
        foreach (var edge in edges)
            foreach (var node in new[] { edge.A, edge.B })
            {
                if (!nodes.TryGetValue(node, out var list)) nodes[node] = list = new();
                list.Add(edge);
            }
        var result = new Dictionary<string, int>();
        owned = new HashSet<int>();
        int component = 0;
        foreach (var first in edges)
        {
            if (result.ContainsKey(first.Id)) continue;
            var queue = new Queue<Edge>();
            queue.Enqueue(first);
            result[first.Id] = component;
            while (queue.Count > 0)
            {
                var edge = queue.Dequeue();
                if (edge.Owned) owned.Add(component);
                foreach (var neighbor in nodes[edge.A].Concat(nodes[edge.B]))
                    if (!result.ContainsKey(neighbor.Id))
                    {
                        result[neighbor.Id] = component;
                        queue.Enqueue(neighbor);
                    }
            }
            component++;
        }
        return result;
    }
}
