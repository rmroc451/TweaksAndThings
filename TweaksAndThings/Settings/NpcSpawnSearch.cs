using System;

namespace RMROC451.TweaksAndThings;

internal static class NpcSpawnSearch
{
    // Bracket the first free cut along connected track, then refine its leading edge.
    // Scan in short steps because occupancy can introduce more than one free interval.
    internal static double? FirstFit(Func<double, bool> fits, double maximum, double step = 10, double precision = 0.25)
    {
        if (maximum < 0 || step <= 0 || precision <= 0) return null;
        if (fits(0)) return 0;
        double previous = 0;
        while (previous < maximum)
        {
            double next = Math.Min(maximum, previous + step);
            if (fits(next))
            {
                double low = previous, high = next;
                while (high - low > precision)
                {
                    double middle = (low + high) / 2;
                    if (fits(middle)) high = middle; else low = middle;
                }
                return high;
            }
            previous = next;
        }
        return null;
    }
}
