using System;

namespace RMROC451.TweaksAndThings;

internal static class NpcPoolPowerPolicy
{
    internal static bool BlocksConnection(bool pooled, bool sameUnit, string key, bool trusted) =>
        !trusted && pooled && sameUnit &&
        (key.EndsWith(".coupled", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".cutLever", StringComparison.OrdinalIgnoreCase));

    // Returning toward the native area is allowed if a saved unit already
    // overlaps its boundary. Moving farther away is inhibited.
    internal static bool BlocksMovement(bool inside, bool futureInside, float distance, float futureDistance) =>
        inside ? !futureInside : !futureInside && futureDistance >= distance;
}
