using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcTimetablePacingTests
{
    [Test]
    public void AllowanceAddsFifteenPercentAndTwoMinutes()
    { Assert.That(NpcTimetablePacingPolicy.BufferedTravelSeconds(1200), Is.EqualTo(1500).Within(0.01)); }

    [Test]
    public void AheadTrainPacesWithoutStopping()
    { Assert.That(NpcTimetablePacingPolicy.Speed(35, 600, 1500, 5000), Is.InRange(8d, 34.9d)); }

    [TestCase(35, 600, 500, 5000)]
    [TestCase(5, 600, 1500, 5000)]
    [TestCase(35, 600, 1500, 100)]
    public void LateTrainYardApproachAndLowNativeLimitRemainUnchanged(double speed, double travel, double due, double meters)
    { Assert.That(NpcTimetablePacingPolicy.Speed(speed, travel, due, meters), Is.EqualTo(speed)); }

    [Test]
    public void SignalStopIsNeverRaised()
    { Assert.That(NpcTimetablePacingPolicy.Speed(0, 600, 1500, 5000), Is.Zero); }
}
