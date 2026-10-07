using System;
using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class StartingProgressionPolicy
{
    internal const string OriginalOrigin = "TNT-EAST-WHITTIER";
    internal static Dictionary<string, string[]> Validate(IReadOnlyDictionary<string, string[]> graph)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (!graph.ContainsKey(id)) throw new ArgumentException("Missing progression prerequisite: " + id);
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new ArgumentException("Progression dependency cycle at " + id);
            foreach (string parent in graph[id]) Visit(parent);
            visiting.Remove(id); visited.Add(id);
        }
        foreach (string id in graph.Keys) Visit(id);
        return graph.ToDictionary(p => p.Key, p => p.Value.Distinct().ToArray(), StringComparer.Ordinal);
    }
    internal static Dictionary<string, string[]> ForOrigin(IReadOnlyDictionary<string, string[]> native, string origin)
    {
        var graph = Validate(native);
        if (origin == OriginalOrigin) return graph;
        if (!graph.ContainsKey(origin)) throw new ArgumentException("Unknown starting section: " + origin);
        var path = new List<string>();
        string current = origin;
        while (true)
        {
            path.Add(current);
            var parents = graph[current];
            if (parents.Length == 0) break;
            if (parents.Length != 1) throw new ArgumentException("This start has multiple required approach sections and cannot safely reverse expansion: " + current);
            current = parents[0];
        }
        graph[OriginalOrigin] = new[] { path.Last() };
        for (int i = 0; i < path.Count; i++) graph[path[i]] = i == 0 ? Array.Empty<string>() : new[] { path[i - 1] };
        // Off-path dependencies are retained. Reject cycles instead of silently dropping requirements.
        return Validate(graph);
    }
    internal static HashSet<string> Select(IReadOnlyDictionary<string, string[]> graph, IEnumerable<string> selected, string id)
    {
        Validate(graph);
        var result = new HashSet<string>(selected, StringComparer.Ordinal);
        void Include(string key)
        {
            if (!graph.ContainsKey(key)) throw new ArgumentException("Unknown progression item: " + key);
            foreach (string parent in graph[key]) Include(parent);
            result.Add(key);
        }
        Include(id); return result;
    }
    internal static HashSet<string> Remove(IReadOnlyDictionary<string, string[]> graph, IEnumerable<string> selected, string id)
    {
        var result = new HashSet<string>(selected, StringComparer.Ordinal);
        result.Remove(id);
        bool changed;
        do { changed = false; foreach (string key in result.ToArray())
            if (!graph.ContainsKey(key) || graph[key].Any(parent => !result.Contains(parent))) { result.Remove(key); changed = true; }
        } while (changed);
        return result;
    }
}
