using System;

namespace RMROC451.TweaksAndThings;

internal static class NpcSidingHoldPolicy
{
    // A momentary slowdown is not a completed meet stop. Neither route replanning
    // nor a switch being empty for one update is permission to leave the refuge.
    internal static bool MayRequestDeparture(double distanceToStop, double speedMph,
        bool tailClear, bool opposingApproach) => distanceToStop <= 25 && distanceToStop >= -2 &&
        Math.Abs(speedMph) < 0.1 && tailClear && !opposingApproach;
    internal static double StopDistance(double remaining, double clearance) => Math.Max(0, remaining - clearance);
}
