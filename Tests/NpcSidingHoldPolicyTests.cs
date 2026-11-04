using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcSidingHoldPolicyTests
{
    [TestCase(0.3)]
    [TestCase(2.3)]
    [TestCase(15)]
    public void NearlyStoppedTrainCannotLeaveSiding(double speed) =>
        Assert.That(NpcSidingHoldPolicy.MayRequestDeparture(0, speed, true, false), Is.False);
    [Test] public void ApproachingOpposingTrainKeepsHoldEvenWhenSwitchIsEmpty() =>
        Assert.That(NpcSidingHoldPolicy.MayRequestDeparture(0, 0, true, true), Is.False);
    [Test] public void FoulingTailKeepsHold() =>
        Assert.That(NpcSidingHoldPolicy.MayRequestDeparture(0, 0, false, false), Is.False);
    [TestCase(35)]
    [TestCase(-25)]
    public void ReplannedRouteCannotTurnAnotherLocationIntoCompletedSidingStop(double distance) =>
        Assert.That(NpcSidingHoldPolicy.MayRequestDeparture(distance, 0, true, false), Is.False);
    [Test] public void FullStopWithBothThroatsClearCanRequestAtomicNextCorridorGrant() =>
        Assert.That(NpcSidingHoldPolicy.MayRequestDeparture(1, 0, true, false), Is.True);
    [Test] public void NativeBrakingStopShortStillReleasesWhenWholeConsistIsSafelyInsideSiding() =>
        Assert.That(NpcSidingHoldPolicy.MayRequestDeparture(8, 0, true, false), Is.True);
}
