using System;

namespace RMROC451.TweaksAndThings;

internal static class NpcHeldDeliveryPolicy
{
    internal static int MissingReservations(int outstanding, int reserved) => Math.Max(0, outstanding - reserved);
    internal static double Deadline(double nextService, int hours) => nextService + Math.Max(1, Math.Min(72, hours)) * 3600d;
    internal static int Premium(int normalPayout, int percent, double arrival, double start, double deadline)
    {
        if (normalPayout <= 0 || deadline <= start || arrival >= deadline) return 0;
        double remaining = Math.Max(0, Math.Min(1, (deadline - arrival) / (deadline - start)));
        return (int)Math.Round(normalPayout * Math.Max(0, Math.Min(100, percent)) / 100d * remaining, MidpointRounding.AwayFromZero);
    }
}
