using System;

namespace RMROC451.TweaksAndThings;

internal static class SimulationSpeedPolicy
{
    internal static int Rate(int value) => value == 2 || value == 4 || value == 8 ? value : 1;
    internal static double? SwitchingBoundary(double now, double arrival, double departure)
    {
        if (departure <= arrival || departure <= now) return null;
        return Math.Max(now, arrival);
    }
    internal static double? StopClock(string text, double now)
    {
        var pieces = text.Trim().Split(':');
        if (pieces.Length != 2 || !int.TryParse(pieces[0], out int hour) || !int.TryParse(pieces[1], out int minute) ||
            hour < 0 || hour > 23 || minute < 0 || minute > 59) return null;
        double target = Math.Floor(now / 86400) * 86400 + hour * 3600 + minute * 60;
        return target <= now ? target + 86400 : target;
    }
}
