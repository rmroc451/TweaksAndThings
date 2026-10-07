using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public sealed class NpcWarpBoundaryPolicyTests
{
    [Test]
    public void TierZeroUsesNormalServiceAndNewDayReplacesItWithAiDispatch()
    {
        double? initial = NpcWarpBoundaryPolicy.ServiceTarget(85000, null, 100000);
        Assert.That(initial, Is.EqualTo(100000));
        Assert.That(NpcWarpBoundaryPolicy.SegmentEnd(85000, initial!.Value, null), Is.EqualTo(86400));
        Assert.That(NpcWarpBoundaryPolicy.ServiceTarget(86400, 95000, 100000), Is.EqualTo(95000));
        Assert.That(NpcWarpBoundaryPolicy.ServiceTarget(86400, null, 100000), Is.EqualTo(100000));
    }
    [Test]
    public void FallbackHandlesDueNowAndNoScheduledInterchanges()
    {
        Assert.That(NpcWarpBoundaryPolicy.ServiceTarget(85000, null, 85000), Is.EqualTo(85001));
        Assert.That(NpcWarpBoundaryPolicy.ServiceTarget(85000, null, null), Is.Null);
        Assert.That(NpcWarpBoundaryPolicy.ServiceTarget(85000, 84000, 100000), Is.EqualTo(85000));
    }
    [Test]
    public void OverdueNativeDailyServiceUsesTheUpcomingCalendarSlot()
    {
        Assert.That(NpcWarpBoundaryPolicy.UpcomingNormalService(85000, 21600), Is.EqualTo(108000));
        Assert.That(NpcWarpBoundaryPolicy.UpcomingNormalService(108000, 21600), Is.EqualTo(194400));
        Assert.That(NpcWarpBoundaryPolicy.UpcomingNormalService(85000, 100000), Is.EqualTo(100000));
        Assert.That(NpcWarpBoundaryPolicy.UpcomingNormalService(85000, 85000), Is.EqualTo(85000));
    }
    [Test]
    public void StopsAtMidnightBeforeFollowingDaysDispatch()
    {
        Assert.That(NpcWarpBoundaryPolicy.SegmentEnd(85000, 92000, 90000), Is.EqualTo(86400));
        Assert.That(NpcWarpBoundaryPolicy.MidnightBeforeDispatch(86400, 86400, 90000), Is.True);
        Assert.That(NpcWarpBoundaryPolicy.SegmentEnd(86400, 92000, 90000), Is.EqualTo(90000));
    }
    [Test]
    public void DispatchAtMidnightTakesPrecedenceOverContinuation() =>
        Assert.That(NpcWarpBoundaryPolicy.MidnightBeforeDispatch(86400, 86400, 86400), Is.False);
    [Test]
    public void DoesNotOvershootRequestedTimeOrEarlierDispatch()
    {
        Assert.That(NpcWarpBoundaryPolicy.SegmentEnd(85000, 86000, 90000), Is.EqualTo(86000));
        Assert.That(NpcWarpBoundaryPolicy.SegmentEnd(85000, 92000, 85500), Is.EqualTo(85500));
    }
}
