using System;
using System.Collections.Generic;

namespace RMROC451.TweaksAndThings;

internal static class NpcTimetableProtection
{
    internal static bool Merge<T>(IDictionary<string, T> proposed, IReadOnlyDictionary<string, T> snapshots, Func<T, T> clone)
    {
        bool changed = false;
        foreach (var saved in snapshots)
        {
            if (proposed.TryGetValue(saved.Key, out var current) && EqualityComparer<T>.Default.Equals(current, saved.Value)) continue;
            proposed[saved.Key] = clone(saved.Value);
            changed = true;
        }
        return changed;
    }
}
