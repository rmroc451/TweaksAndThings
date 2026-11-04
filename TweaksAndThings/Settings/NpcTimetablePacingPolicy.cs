using System;

namespace RMROC451.TweaksAndThings;

internal static class NpcTimetablePacingPolicy
{
    internal static double BufferedTravelSeconds(double seconds) => Math.Max(60, seconds) * 1.15 + 120;
    internal static double Speed(double allowedMph, double travelSeconds, double secondsUntilDue, double metersRemaining)
    {
        if (allowedMph <= 0 || metersRemaining < 300 || travelSeconds <= 0 || secondsUntilDue - travelSeconds <= 120)
            return allowedMph;
        double ratio = Math.Max(0.65, Math.Min(1, travelSeconds / Math.Max(1, secondsUntilDue - 120)));
        // Pace without stopping or increasing any lower native safety limit.
        return Math.Min(allowedMph, Math.Max(Math.Min(8, allowedMph), allowedMph * ratio));
    }
}
