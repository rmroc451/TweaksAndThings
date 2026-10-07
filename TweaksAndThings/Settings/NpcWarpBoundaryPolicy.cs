using System;

namespace RMROC451.TweaksAndThings;

internal static class NpcWarpBoundaryPolicy
{
    internal static double UpcomingNormalService(double now, double due) =>
        due < now ? due + (Math.Floor((now - due) / 86400) + 1) * 86400 : due;
    internal static double? ServiceTarget(double now, double? simulated, double? native) =>
        simulated.HasValue ? Math.Max(now, simulated.Value) : native.HasValue ? Math.Max(now + 1, native.Value) : null;
    internal static double NextMidnight(double now) => (Math.Floor(now / 86400) + 1) * 86400;
    internal static double SegmentEnd(double cursor, double requestedEnd, double? dispatch) =>
        Math.Min(requestedEnd, Math.Min(NextMidnight(cursor), dispatch ?? double.PositiveInfinity));
    internal static bool MidnightBeforeDispatch(double end, double midnight, double? dispatch) =>
        Math.Abs(end - midnight) < 0.01 && (!dispatch.HasValue || dispatch.Value > midnight + 0.01);
}
