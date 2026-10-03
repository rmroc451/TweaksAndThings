using System;
using System.Collections.Generic;

namespace RMROC451.TweaksAndThings;

internal static class NpcRandomTrafficPolicy
{
    // One to two trains per day at baseline, plus one per four active contract tiers.
    internal static (int min, int max) DailyRange(int tiers, double multiplier)
    {
        if (double.IsNaN(multiplier) || multiplier <= 0) return (0, 0);
        multiplier = Math.Min(10, multiplier);
        double growth = Math.Max(0, tiers) / 4d;
        return ((int)Math.Floor((1 + growth) * multiplier), (int)Math.Ceiling((2 + growth) * multiplier));
    }

    internal static List<double> DepartureTimes(Random random, int count, double start, double end)
    {
        var times = new List<double>();
        if (end <= start) return times;
        for (int i = 0; i < count; i++) times.Add(start + random.NextDouble() * (end - start));
        times.Sort();
        return times;
    }

    internal static string Clock(double seconds)
    {
        int minute = ((int)Math.Floor(seconds / 60) % 1440 + 1440) % 1440;
        return $"{minute / 60:00}:{minute % 60:00}";
    }

    internal static int OrderingFee(double quantity, double unitValue, int percent) =>
        (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(0, quantity) * Math.Max(0, unitValue) * Math.Max(0, Math.Min(100, percent)) / 100d));
}
