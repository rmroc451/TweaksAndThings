using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class NpcHeldDeliveryPolicyTests
{
    [TestCase(0, 1000)]
    [TestCase(3600, 750)]
    [TestCase(7200, 500)]
    [TestCase(10800, 250)]
    [TestCase(14400, 0)]
    [TestCase(20000, 0)]
    [TestCase(-3600, 1000)]
    public void DoublePayoutExtraTapersLinearlyOverFourGameHours(double elapsed, int extra) =>
        Assert.That(NpcHeldDeliveryPolicy.Premium(1000, 100, 86400 + elapsed, 86400, 100800), Is.EqualTo(extra));

    [Test]
    public void ConfiguredDeadlineAndPremiumAreRespected()
    {
        double deadline = NpcHeldDeliveryPolicy.Deadline(86400, 8);
        Assert.That(deadline, Is.EqualTo(115200));
        Assert.That(NpcHeldDeliveryPolicy.Premium(1000, 50, 100800, 86400, deadline), Is.EqualTo(250));
        Assert.That(NpcHeldDeliveryPolicy.Premium(0, 100, 86400, 86400, deadline), Is.Zero);
        Assert.That(NpcHeldDeliveryPolicy.Premium(1000, 0, 86400, 86400, deadline), Is.Zero);
        Assert.That(NpcHeldDeliveryPolicy.Premium(1000, 100, 86400, 86400, 86400), Is.Zero);
    }

    [TestCase(3, 3, 0)]
    [TestCase(3, 1, 2)]
    [TestCase(1, 3, 0)]
    public void ReservationsAreReplenishedWithoutDuplicatingAlreadyPendingOrders(int outstanding, int reserved, int missing) =>
        Assert.That(NpcHeldDeliveryPolicy.MissingReservations(outstanding, reserved), Is.EqualTo(missing));

    [Test]
    public void ConsistDropsRandomlySelectedCarsOnlyUntilDestinationFits()
    {
        var cars = new List<int> { 1, 2, 3, 4 };
        var deferred = new List<int>();
        int removed = NpcServicePolicy.TrimRandomToFit(cars, cut => cut.Sum() <= 5, count => count - 1, deferred.Add);
        Assert.That(cars, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(deferred, Is.EqualTo(new[] { 4, 3 }));
        Assert.That(removed, Is.EqualTo(2));
    }

    [Test]
    public void CompletelyFullYardDefersEveryCarAndPreventsEmptyDeliveryDispatch()
    {
        var cars = new List<string> { "a", "b", "c" };
        var deferred = new List<string>();
        NpcServicePolicy.TrimRandomToFit(cars, _ => false, _ => 0, deferred.Add);
        Assert.That(deferred, Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(cars, Is.Empty);
        Assert.That(NpcServicePolicy.HasServiceDemand(cars.Count, 0), Is.False);
        Assert.That(NpcServicePolicy.HasServiceDemand(cars.Count, 2), Is.True, "Pickup-only services still have work");
    }

    [TestCase("Approaching", false, "In transit")]
    [TestCase("Approaching", true, "Approaching")]
    [TestCase("Departing", false, "In transit (return)")]
    [TestCase("Departing", true, "Approaching exit")]
    [TestCase("Setting out", false, "Setting out")]
    public void HudDistinguishesDistantTravelFromTheFinalApproach(string phase, bool near, string label) =>
        Assert.That(NpcServicePolicy.TravelLabel(phase, near), Is.EqualTo(label));
}
