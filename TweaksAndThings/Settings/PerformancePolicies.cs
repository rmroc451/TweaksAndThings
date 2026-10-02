using System.Collections.Generic;

namespace RMROC451.TweaksAndThings;

internal static class PerformancePolicies
{
    internal static Dictionary<string, (bool spotted, bool filling)> RetainActiveCrewStatuses(
        IEnumerable<KeyValuePair<string, (bool spotted, bool filling)>> statuses,
        ISet<string> activeCarIds)
    {
        var retained = new Dictionary<string, (bool spotted, bool filling)>();
        foreach (var status in statuses)
        {
            if (activeCarIds.Contains(status.Key))
                retained.Add(status.Key, status.Value);
        }

        return retained;
    }

    internal static HashSet<string> SelectIds<TItem>(
        IEnumerable<TItem> items,
        Func<TItem, bool> shouldSelect,
        Func<TItem, string> getId)
    {
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (shouldSelect(item))
                selected.Add(getId(item));
        }

        return selected;
    }
}
