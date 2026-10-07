using System;

namespace RMROC451.TweaksAndThings;

// Timetable minutes are daily clock times. Keep paths continuous through midnight,
// then clip translated copies to the visible day rather than drawing across it.
internal static class TimetableChartGeometry
{
    internal static double Milepost(double a, double b, double distanceA, double distanceB)
    {
        double between = Math.Abs(b - a) * 1609.344;
        // Interpolate between surveyed markers; extrapolate outside their pair.
        if (distanceA + distanceB <= between + 50 || Math.Abs(distanceA - distanceB) < between - 50)
            // Equal extra distance down a siding cancels in this projection.
            return a + (b - a) * (distanceA - distanceB + between) / Math.Max(1, 2 * between);
        return a + Math.Sign(a - b) * distanceA / 1609.344;
    }
    internal static double[] DistanceRows(System.Collections.Generic.IReadOnlyList<double> gaps, double pixelsPerMeter)
    {
        var rows = new double[gaps.Count + 1];
        for (int i = 0; i < gaps.Count; i++) rows[i + 1] = rows[i] + Math.Max(0, gaps[i]) * pixelsPerMeter;
        return rows;
    }

    internal static double RowPosition(System.Collections.Generic.IReadOnlyList<double> rows, double row)
    {
        if (rows.Count == 0) return 0;
        row = Math.Max(0, Math.Min(rows.Count - 1, row));
        int lower = (int)Math.Floor(row), upper = Math.Min(rows.Count - 1, lower + 1);
        return rows[lower] + (rows[upper] - rows[lower]) * (row - lower);
    }
    internal static double Unwrap(double minutes, double previous)
    {
        while (minutes < previous) minutes += 1440;
        return minutes;
    }

    internal static bool Clip(double x1, double y1, double x2, double y2,
        out double a, out double b, out double c, out double d)
        => ClipRange(x1, y1, x2, y2, 0, 1440, out a, out b, out c, out d);

    internal static bool ClipRange(double x1, double y1, double x2, double y2, double minimum, double maximum,
        out double a, out double b, out double c, out double d)
    {
        a = x1; b = y1; c = x2; d = y2;
        if (x1 == x2) return x1 >= minimum && x1 <= maximum;
        double low = Math.Max(0, Math.Min((minimum - x1) / (x2 - x1), (maximum - x1) / (x2 - x1)));
        double high = Math.Min(1, Math.Max((minimum - x1) / (x2 - x1), (maximum - x1) / (x2 - x1)));
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
