using System;
using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class NpcPowerSelection
{
    // Capacities are usable HP after each engine's own weight on each route grade.
    internal static List<int>? Select(IReadOnlyList<double[]> capacities, double[] demand, int maxUnits = 16,
        IReadOnlyList<bool>? diesels = null, int targetDiesels = 0)
    {
        if (capacities.Count == 0) return null;
        if (demand.All(d => d <= 0)) return new List<int> { 0 };
        // Exhaustively check one and two units before the larger-combination
        // heuristic, including complementary engines with a negative constraint.
        for (int count = 1; count <= Math.Min(2, maxUnits); count++)
        {
            List<int>? pair = null;
            double pairScore = double.MaxValue;
            int pairDeviation = int.MaxValue;
            for (int a = 0; a < capacities.Count; a++)
                for (int b = count == 1 ? a : 0; b < (count == 1 ? a + 1 : capacities.Count); b++)
                {
                    if (!Enumerable.Range(0, demand.Length).All(p => capacities[a][p] + (count == 2 ? capacities[b][p] : 0) + 0.0001 >= demand[p])) continue;
                    double score = Enumerable.Range(0, demand.Length).Where(p => demand[p] > 0)
                        .Sum(p => (capacities[a][p] + (count == 2 ? capacities[b][p] : 0)) / demand[p]);
                    int deviation = diesels == null ? 0 : Math.Abs((diesels[a] ? 1 : 0) + (count == 2 && diesels[b] ? 1 : 0) - targetDiesels);
                    if (deviation < pairDeviation || deviation == pairDeviation && score < pairScore)
                    { pairDeviation = deviation; pairScore = score; pair = count == 1 ? new List<int> { a } : new List<int> { a, b }; }
                }
            if (pair != null) return pair;
        }
        List<int>? best = null;
        double bestScore = double.MaxValue;
        int bestDeviation = int.MaxValue;
        for (int lead = 0; lead < capacities.Count; lead++)
        {
            var selected = new List<int> { lead };
            var total = (double[])capacities[lead].Clone();
            while (selected.Count <= maxUnits)
            {
                if (Enumerable.Range(0, demand.Length).All(p => total[p] + 0.0001 >= demand[p]))
                {
                    double score = Enumerable.Range(0, demand.Length).Where(p => demand[p] > 0).Sum(p => total[p] / demand[p]);
                    int deviation = diesels == null ? 0 : Math.Abs(selected.Count(i => diesels[i]) - targetDiesels);
                    if (selected.Count < (best?.Count ?? int.MaxValue) || selected.Count == best?.Count &&
                        (deviation < bestDeviation || deviation == bestDeviation && score < bestScore))
                    { best = selected.ToList(); bestScore = score; bestDeviation = deviation; }
                    break;
                }
                if (selected.Count == maxUnits) break;
                int next = -1;
                double scoreNext = double.MaxValue;
                for (int i = 0; i < capacities.Count; i++)
                {
                    if (Enumerable.Range(0, demand.Length).Any(p => demand[p] > 0 && capacities[i][p] <= 0)) continue;
                    bool completes = Enumerable.Range(0, demand.Length).All(p => total[p] + capacities[i][p] >= demand[p]);
                    double score = completes
                        ? Enumerable.Range(0, demand.Length).Where(p => demand[p] > 0).Sum(p => (total[p] + capacities[i][p]) / demand[p])
                        : 1000000 - Enumerable.Range(0, demand.Length).Where(p => demand[p] > total[p]).Sum(p => Math.Min(1, capacities[i][p] / (demand[p] - total[p])));
                    if (score < scoreNext) { scoreNext = score; next = i; }
                }
                if (next < 0) break;
                selected.Add(next);
                for (int p = 0; p < demand.Length; p++) total[p] += capacities[next][p];
            }
        }
        return best;
    }
}
