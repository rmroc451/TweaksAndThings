using System;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class NpcServicePolicyTests
{
    [TestCase(0, 0, false)]
    [TestCase(1, 0, true)]
    [TestCase(0, 1, true)]
    [TestCase(2, 3, true)]
    public void ServiceRequiresDeliveryOrPickupDemand(int inbound, int pickup, bool expected) =>
        Assert.That(NpcServicePolicy.HasServiceDemand(inbound, pickup), Is.EqualTo(expected));
    [TestCase(500, 100, 1500, 2, 1)]
    [TestCase(1000, 100, 1500, 2, 2)]
    [TestCase(2000, 100, 1500, 2, 4)]
    [TestCase(1000, 100, 200, 2, 0)]
    public void PowerSizingAddsEnginesAndAccountsForEachEngineWeight(double freight, double engine, double hp, double hpt, int expected) =>
        Assert.That(NpcServicePolicy.RequiredPowerUnits(freight, engine, hp, hpt), Is.EqualTo(expected));
    [TestCase(100, 200, new double[] { 150, 120 }, 120)]
    [TestCase(86300, 87000, new double[] { 86450, 86900 }, 86450)]
    [TestCase(100, 200, new double[] { 50, 150 }, 100)]
    [TestCase(100, 200, new double[] { 200 }, 200)]
    public void WarpStopsAtEarliestDispatchIncludingOverdueAndMidnight(double now, double end, double[] times, double expected) =>
        Assert.That(NpcServicePolicy.WarpBoundary(now, end, times), Is.EqualTo(expected));

    [Test]
    public void WarpWithoutDispatchWithinIntervalIsUnchanged()
    {
        Assert.That(NpcServicePolicy.WarpBoundary(100, 200, new[] { 250d, double.NaN }), Is.Null);
        Assert.That(NpcServicePolicy.WarpBoundary(100, 100, new[] { 100d }), Is.Null);
        Assert.That(NpcServicePolicy.WarpBoundary(100, 50, new[] { 75d }), Is.Null);
        Assert.That(NpcServicePolicy.WarpBoundary(100, 200, System.Array.Empty<double>()), Is.Null);
    }
    [TestCase(10, 100, 50, true)]
    [TestCase(50, 100, 50, false)]
    [TestCase(100, 50, 75, false)]
    [TestCase(86390, 86410, 86400, true)]
    [TestCase(0, 180000, 172800, true)]
    public void SchedulerDetectsCrossedTimesWithoutRepeatingTheBoundary(double previous, double now, double due, bool expected) =>
        Assert.That(NpcServicePolicy.Crossed(previous, now, due), Is.EqualTo(expected));

    [Test]
    public void DispatchAccountsForTravelAndStartsOnThePreviousDayWhenNecessary()
    {
        Assert.That(NpcServicePolicy.DispatchTime(86460, 900), Is.EqualTo(85260));
        Assert.That(NpcServicePolicy.DispatchTime(36000, 1800), Is.EqualTo(33900));
    }

    [TestCase(0, 0)]
    [TestCase(1, 120)]
    [TestCase(2, 240)]
    public void TransferDurationIsTwoGameMinutesPerCar(int cars, int seconds) =>
        Assert.That(NpcServicePolicy.NextTransfer(1000, cars), Is.EqualTo(1000 + seconds));

    [TestCase(false, 120)]
    [TestCase(true, 60)]
    public void NearbyCabooseHalvesBothKindsOfTransfer(bool nearby, double expectedRate)
    {
        double rate = NpcServicePolicy.TransferRate(nearby);
        Assert.That(rate, Is.EqualTo(expectedRate));
        Assert.That(NpcServicePolicy.NextTransfer(1000, 2, rate), Is.EqualTo(1000 + 2 * expectedRate));
    }

    [Test]
    public void CabooseArrivingAndLeavingPreservesWorkAlreadyDone()
    {
        // Two-car group: 240 seconds initially. After 60 seconds of work, only the remaining 180 are halved.
        double deadline = NpcServicePolicy.RescaleDeadline(60, 240, 120, 60);
        Assert.That(deadline, Is.EqualTo(150));
        // After another 30 seconds, removing the caboose doubles only the remaining minute.
        Assert.That(NpcServicePolicy.RescaleDeadline(90, deadline, 60, 120), Is.EqualTo(210));
        Assert.That(NpcServicePolicy.RescaleDeadline(300, deadline, 60, 120), Is.EqualTo(deadline));
    }

    [Test]
    public void TimewarpCrossesMultipleBatchDeadlinesAndIncludesTheLastSingleCar()
    {
        double clock = 1000;
        int completed = 0;
        while (completed < 5)
        {
            int count = Math.Min(NpcServicePolicy.BatchSize, 5 - completed);
            clock = NpcServicePolicy.NextTransfer(clock, count);
            completed += count;
        }
        Assert.That(clock, Is.EqualTo(1600));
        Assert.That(completed, Is.EqualTo(5));
    }

    [Test]
    public void TwoInterchangesHaveIndependentTransferClocks()
    {
        double assisted = NpcServicePolicy.NextTransfer(1000, 2, NpcServicePolicy.TransferRate(true));
        double normal = NpcServicePolicy.NextTransfer(1000, 2, NpcServicePolicy.TransferRate(false));
        Assert.That(assisted, Is.EqualTo(1120));
        Assert.That(normal, Is.EqualTo(1240));
        Assert.That(NpcServicePolicy.NextTransfer(assisted, 1, 60), Is.EqualTo(1180));
        Assert.That(normal, Is.EqualTo(1240));
    }

    [TestCase("Setting out", 2, 3, "Setting out")]
    [TestCase("Setting out", 0, 3, "Picking up")]
    [TestCase("Picking up", 2, 0, "Setting out")]
    [TestCase("Picking up", 0, 0, "Departing")]
    [TestCase("Setting out", 0, 0, "Departing")]
    [TestCase("Picking up", 0, 1, "Picking up")]
    public void ServiceDoesNotDepartUntilBothTransfersFinish(string phase, int setouts, int pickups, string expected) =>
        Assert.That(NpcServicePolicy.NextPhase(phase, setouts, pickups), Is.EqualTo(expected));

    [Test]
    public void NpcFreightIsExemptFromCabooseSpeedRestrictions()
    {
        Assert.That(NpcServicePolicy.SpeedLimit(45, true, true, false, true, false, npc: true), Is.EqualTo(45));
        Assert.That(NpcServicePolicy.SpeedLimit(45, true, true, false, true, false, npc: false), Is.EqualTo(20));
        Assert.That(NpcServicePolicy.SpeedLimit(45, true, true, true, true, false), Is.EqualTo(45));
        Assert.That(NpcServicePolicy.SpeedLimit(45, true, true, false, true, true), Is.EqualTo(45));
        Assert.That(NpcServicePolicy.SpeedLimit(45, false, true, false, true, false), Is.EqualTo(45));
        Assert.That(NpcServicePolicy.SpeedLimit(15, true, true, false, true, false), Is.EqualTo(15));
    }

    [Test]
    public void PowerUsesPoundsAndMetricTonnesRatherThanRawCarWeight()
    {
        Assert.That(NpcServicePolicy.Horsepower(10000, 37.5f), Is.EqualTo(1000));
        Assert.That(NpcServicePolicy.Tonnes(22046.2262f), Is.EqualTo(10).Within(0.00001));
    }

    [TestCase(100, 200, 100, 20, true, 120)]
    [TestCase(100, 200, 100, 20, false, 180)]
    [TestCase(300, 50, 100, 20, true, 130)]
    public void SpawnRankingUsesShortestConnectedDistanceFromEitherTrackEnd(double a, double b, double length, double distance, bool fromA, double expected) =>
        Assert.That(NpcServicePolicy.DistanceToPoint(a, b, length, distance, fromA), Is.EqualTo(expected));
}
