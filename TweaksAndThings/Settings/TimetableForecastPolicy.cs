using System;

namespace RMROC451.TweaksAndThings;

internal static class TimetableForecastPolicy
{
    internal static double? TravelSeconds(double meters, double currentMph, double trackMph, bool knownDeparture)
    {
        if (meters <= 1) return 0;
        if (currentMph < 0.5 && !knownDeparture) return null;
        double speed = currentMph >= 0.5 ? Math.Min(currentMph, trackMph) : trackMph;
        return speed > 0 ? meters / (speed * 0.44704) : (double?)null;
    }
    internal static double Departure(double arrival, double dwell, double scheduled, double meet, double ready)
        => Math.Max(Math.Max(arrival + Math.Max(0, dwell), scheduled), Math.Max(meet, ready));
    internal static double ClockNear(double clockMinutes, double referenceMinutes)
    {
        double value = Math.Floor(referenceMinutes / 1440) * 1440 + (clockMinutes % 1440 + 1440) % 1440;
        if (value - referenceMinutes > 720) value -= 1440;
        if (referenceMinutes - value > 720) value += 1440;
        return value;
    }
}
