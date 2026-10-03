using NUnit.Framework;
using RMROC451.TweaksAndThings;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class ThroughTrafficPolicyTests
{
    [TestCase("Z-01", "Z-", true)]
    [TestCase("z-freight", "Z-", true)]
    [TestCase("01Z", "Z-", false)]
    [TestCase("Z-01", "", false)]
    [TestCase(null, "Z-", false)]
    public void IsMarkedTrain_UsesCaseInsensitiveSymbolPrefix(string? symbol, string? prefix, bool expected)
    {
        Assert.That(ThroughTrafficPolicy.IsMarkedTrain(symbol, prefix), Is.EqualTo(expected));
    }

    [TestCase("Z-01", "Z-", true, true, true, true)]
    [TestCase("Z-01", "Z-", false, true, true, false)]
    [TestCase("Z-01", "Z-", true, false, true, false)]
    [TestCase("Z-01", "Z-", true, true, false, false)]
    [TestCase("01", "Z-", true, true, true, false)]
    public void IsEligibleTrain_RequiresPrefixFirstClassAndSignalsAtBothEnds(
        string? symbol, string? prefix, bool firstClass, bool originSignals, bool destinationSignals, bool expected)
    {
        Assert.That(ThroughTrafficPolicy.IsEligibleTrain(symbol, prefix, firstClass, originSignals, destinationSignals), Is.EqualTo(expected));
    }

    [TestCase(600, 600, 0, true)]
    [TestCase(600, 605, 5, false)]
    [TestCase(605, 600, 5, true)]
    [TestCase(606, 600, 5, false)]
    [TestCase(2, 1438, 5, true)]
    [TestCase(600, 606, 5, false)]
    [TestCase(1438, 2, 0, false)]
    public void IsDepartureDue_OpensTheScheduledServiceWindow(int now, int scheduled, int grace, bool expected)
    {
        Assert.That(ThroughTrafficPolicy.IsDepartureDue(now, scheduled, grace), Is.EqualTo(expected));
    }

    [TestCase(0, 60, 0)]
    [TestCase(60, 60, 1)]
    [TestCase(121, 60, 3)]
    [TestCase(121, 0, 0)]
    public void PassengerCarsForDemand_SizesCapacityToRouteDemand(int demand, int capacity, int expectedCars)
    {
        Assert.That(ThroughTrafficPolicy.PassengerCarsForDemand(demand, capacity), Is.EqualTo(expectedCars));
    }

    [Test]
    public void HorsepowerPerTonneForGrade_IncreasesWithSteeperGradeAndHigherSpeed()
    {
        var moderate = ThroughTrafficPolicy.HorsepowerPerTonneForGrade(1, 20);
        var steep = ThroughTrafficPolicy.HorsepowerPerTonneForGrade(2, 20);
        var faster = ThroughTrafficPolicy.HorsepowerPerTonneForGrade(1, 40);

        Assert.That(moderate, Is.GreaterThan(0));
        Assert.That(steep, Is.EqualTo(moderate * 2).Within(0.0001));
        Assert.That(faster, Is.EqualTo(moderate * 2).Within(0.0001));
    }

    [TestCase(600, 604, 5, true)]
    [TestCase(600, 605, 5, true)]
    [TestCase(600, 606, 5, false)]
    [TestCase(1438, 2, 5, true)]
    [TestCase(600, 596, 5, true)]
    public void IsWithinGracePeriod_HandlesLatenessAndMidnight(int scheduled, int actual, int grace, bool expected)
    {
        Assert.That(ThroughTrafficPolicy.IsWithinGracePeriod(scheduled, actual, grace), Is.EqualTo(expected));
    }

    [Test]
    public void PassengerCarSettlement_PaysOnTimeAndChargesTheDeliveredPassengersWhenLate()
    {
        Assert.That(ThroughTrafficPolicy.PassengerCarSettlement(50, onTime: true, dollarsPerPassenger: 3), Is.EqualTo(150));
        Assert.That(ThroughTrafficPolicy.PassengerCarSettlement(50, onTime: false, dollarsPerPassenger: 3), Is.EqualTo(-150));
    }
}
