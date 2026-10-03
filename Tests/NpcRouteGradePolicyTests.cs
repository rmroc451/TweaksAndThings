using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcRouteGradePolicyTests
{
    private static NpcRouteGradePolicy.Sample S(double length, double grade, int speed = 35) => new(length, grade, speed);
    [Test] public void ShortGradeSpikeDoesNotApplyToWholeTrain() =>
        Assert.That(NpcRouteGradePolicy.Requirements(new[] { S(100, 0), S(10, 20), S(100, 0) }, 100)[35], Is.EqualTo(2));
    [Test] public void SustainedClimbRetainsRulingGrade() =>
        Assert.That(NpcRouteGradePolicy.Requirements(new[] { S(100, 3), S(100, 3) }, 100)[35], Is.EqualTo(3));
    [Test] public void DownhillCarsOffsetUphillCars() =>
        Assert.That(NpcRouteGradePolicy.Requirements(new[] { S(50, -2), S(50, 2) }, 100)[35], Is.Zero);
    [Test] public void ShortRouteUsesItsActualLength() =>
        Assert.That(NpcRouteGradePolicy.Requirements(new[] { S(20, 2) }, 100)[35], Is.EqualTo(2));
}
