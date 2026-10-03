using System;
using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class NpcTimetablePlanPolicy
{
    internal static int Interpolate(int from, int to, int elapsed, int total) =>
        from + (int)Math.Ceiling((to - from) * Math.Max(0, Math.Min(1, elapsed / (double)Math.Max(1, total))));
    internal readonly struct Link
    {
        internal readonly string From, To;
        internal readonly int Minutes;
        internal Link(string from, string to, int minutes) { From = from; To = to; Minutes = minutes; }
    }
    internal static List<string> Path(IEnumerable<Link> links, string from, string to)
    {
        var edges = links.ToList();
        var distance = new Dictionary<string, double> { [from] = 0 };
        var previous = new Dictionary<string, string>();
        var done = new HashSet<string>();
        while (true) {
            var next = distance.Where(p => !done.Contains(p.Key)).OrderBy(p => p.Value).FirstOrDefault();
            if (next.Key == null) return new();
            if (next.Key == to) break;
            done.Add(next.Key);
            foreach (var edge in edges.Where(e => e.From == next.Key || e.To == next.Key)) {
                string target = edge.From == next.Key ? edge.To : edge.From;
                double cost = next.Value + Math.Max(1, edge.Minutes);
                if (!distance.TryGetValue(target, out var old) || cost < old) { distance[target] = cost; previous[target] = next.Key; }
            }
        }
        var path = new List<string> { to };
        while (path.Last() != from) path.Add(previous[path.Last()]);
        path.Reverse(); return path;
    }

    // A sign change between common locations identifies a planned head-on meet.
    // Hold the newly planned train before the crossing, preserving published plans.
    internal static int Meet(IReadOnlyList<string> codes, IReadOnlyList<int> minutes,
        IReadOnlyDictionary<string, int> opposing, Func<string, bool> usable, int clearance = 5)
    {
        int previous = -1;
        for (int i = 1; i < codes.Count - 1; i++) {
            if (!opposing.TryGetValue(codes[i], out int other)) continue;
            if (previous >= 0 && minutes[previous] <= opposing[codes[previous]] && minutes[i] >= other) {
                for (int j = previous; j > 0; j--)
                    if (opposing.ContainsKey(codes[j]) && usable(codes[j])) return j;
                return -1;
            }
            if (Math.Abs(minutes[i] - other) <= clearance && usable(codes[i])) return i;
            previous = i;
        }
        return -1;
    }
}
