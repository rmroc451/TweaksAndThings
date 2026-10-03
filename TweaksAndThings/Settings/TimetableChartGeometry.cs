using System;

namespace RMROC451.TweaksAndThings;

// Timetable minutes are daily clock times. Keep paths continuous through midnight,
// then clip translated copies to the visible day rather than drawing across it.
internal static class TimetableChartGeometry
{
    internal static double Unwrap(double minutes, double previous)
    {
        while (minutes < previous) minutes += 1440;
        return minutes;
    }

    internal static bool Clip(double x1, double y1, double x2, double y2,
        out double a, out double b, out double c, out double d)
    {
        a = x1; b = y1; c = x2; d = y2;
        if (x1 == x2) return x1 >= 0 && x1 <= 1440;
        double low = Math.Max(0, Math.Min((0 - x1) / (x2 - x1), (1440 - x1) / (x2 - x1)));
        double high = Math.Min(1, Math.Max((0 - x1) / (x2 - x1), (1440 - x1) / (x2 - x1)));
        if (low > high) return false;
        a = x1 + (x2 - x1) * low; b = y1 + (y2 - y1) * low;
        c = x1 + (x2 - x1) * high; d = y1 + (y2 - y1) * high;
        return true;
    }

    internal static string Clock(double minutes)
    {
        int value = ((int)minutes % 1440 + 1440) % 1440;
        return $"{value / 60:00}:{value % 60:00}";
    }
}
