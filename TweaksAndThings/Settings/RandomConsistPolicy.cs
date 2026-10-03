using System;
using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class RandomConsistPolicy
{
    internal const int MaximumCars = 200;

    internal static bool TryGenerate(IReadOnlyList<int> costs, int count, Func<int, int> randomIndex, out List<int> choices)
    {
        choices = new List<int>();
        if (count < 1 || count > MaximumCars || costs.Count == 0 || costs.Any(c => c < 1 || c > 2)) return false;
        if (!costs.Contains(1) && count % 2 != 0) return false;
        int remaining = count;
        while (remaining > 0)
        {
            var eligible = Enumerable.Range(0, costs.Count).Where(i => costs[i] <= remaining).ToList();
            if (eligible.Count == 0) return false;
            int choice = eligible[randomIndex(eligible.Count)];
            choices.Add(choice);
            remaining -= costs[choice];
        }
        return true;
    }
}
