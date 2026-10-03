using System;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcRandomTrafficPolicyTests
{
    [TestCase(0, 1, 1, 2)]
    [TestCase(4, 1, 2, 3)]
    [TestCase(20, 2, 12, 14)]
    [TestCase(20, 0, 0, 0)]
    [TestCase(-8, 1, 1, 2)]
    public void DailyRangeScalesWithContractTiersAndModifier(int tiers, double modifier, int minimum, int maximum)
    { Assert.That(NpcRandomTrafficPolicy.DailyRange(tiers, modifier), Is.EqualTo((minimum, maximum))); }

    [Test]
    public void RandomDeparturesStayInRemainingDayAndAreChronological()
    {
        var times = NpcRandomTrafficPolicy.DepartureTimes(new Random(123), 30, 36000, 86400);
        Assert.That(times, Has.Count.EqualTo(30));
        Assert.That(times, Is.Ordered);
        Assert.That(times, Has.All.GreaterThanOrEqualTo(36000).And.LessThan(86400));
    }

    [TestCase(0, "00:00")]
    [TestCase(3660, "01:01")]
    [TestCase(86399, "23:59")]
    [TestCase(86460, "00:01")]
    public void ClockUsesPaddedHoursMinutesAcrossMidnight(double seconds, string expected)
    { Assert.That(NpcRandomTrafficPolicy.Clock(seconds), Is.EqualTo(expected)); }

    [TestCase(100000, 0.05, 10, 500)]
    [TestCase(1, 1, 10, 1)]
    [TestCase(100000, 0.05, 0, 0)]
    public void OrderingFeeIsPercentOfLoadValueRoundedUp(double quantity, double unitValue, int percent, int fee)
    { Assert.That(NpcRandomTrafficPolicy.OrderingFee(quantity, unitValue, percent), Is.EqualTo(fee)); }
}
