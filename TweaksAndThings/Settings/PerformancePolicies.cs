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
}
