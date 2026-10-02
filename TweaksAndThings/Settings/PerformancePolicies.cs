using System.Collections.Generic;
using System;

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

    internal static (bool HasNeedsOiling, bool HasHotbox, float? LowestOil) SummarizeOilingConsist(
        IEnumerable<(bool NeedsOiling, bool HasHotbox, float Oiled)> cars)
    {
        bool hasNeedsOiling = false;
        bool hasHotbox = false;
        float? lowestOil = null;
        foreach (var car in cars)
        {
            hasNeedsOiling |= car.NeedsOiling;
            hasHotbox |= car.HasHotbox;
            if (!lowestOil.HasValue || car.Oiled < lowestOil.Value)
                lowestOil = car.Oiled;
        }

        return (hasNeedsOiling, hasHotbox, lowestOil);
    }

    internal static (int PlayerCount, List<string> SelectedNames) SummarizePlayers(
        IEnumerable<(string Name, bool Selected)> players)
    {
        int playerCount = 0;
        var selectedNames = new List<string>();
        foreach (var player in players)
        {
            playerCount++;
            if (player.Selected)
                selectedNames.Add(player.Name);
        }

        return (playerCount, selectedNames);
    }

    internal static HashSet<TValue> CollectDistinctWhen<TItem, TValue>(
        IEnumerable<TItem> items,
        Func<TItem, TValue> selectValue,
        Func<TValue, bool> shouldInclude)
    {
        var values = new HashSet<TValue>();
        foreach (var item in items)
        {
            TValue value = selectValue(item);
            if (shouldInclude(value))
                values.Add(value);
        }

        return values;
    }

    internal static bool ShouldReportConsistOiling(
        bool hasNeedsOiling,
        bool hasHotbox,
        Func<bool> hotboxRequirementIsFulfilled) =>
        hasNeedsOiling || (hasHotbox && hotboxRequirementIsFulfilled());
}
