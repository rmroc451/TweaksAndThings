using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class SimulationSpeedPolicyTests
{
    [Test]
    public void FastClockRequiresEveryControllingEngineToBeAutomatic()
    {
        Assert.That(SimulationSpeedPolicy.AllAutomatic(new[] { true, true }), Is.True);
        Assert.That(SimulationSpeedPolicy.AllAutomatic(new[] { true, false }), Is.False);
        Assert.That(SimulationSpeedPolicy.AllAutomatic(new[] { false, true }), Is.False);
        Assert.That(SimulationSpeedPolicy.AllAutomatic(System.Array.Empty<bool>()), Is.True);
    }
    [TestCase(true, false, false, false, false)]
    [TestCase(false, true, false, false, false)]
    [TestCase(false, false, true, false, false)]
    [TestCase(false, false, false, true, false)]
    [TestCase(false, false, false, false, true)]
    public void NpcAndPooledPowerNeverBlockFastClock(bool npc, bool pooled, bool mu, bool hidden, bool expected) =>
        Assert.That(SimulationSpeedPolicy.RequiresAutomatic(npc, pooled, mu, hidden), Is.EqualTo(expected));
    [TestCase(2, 2)] [TestCase(4, 4)] [TestCase(8, 8)] [TestCase(1000, 1)] [TestCase(0, 1)]
    public void OnlySupportedPhysicsRatesAreAccepted(int input, int expected)
        => Assert.That(SimulationSpeedPolicy.Rate(input), Is.EqualTo(expected));
    [Test]
    public void SwitchingWindowsStopAtTheirStartAndCannotBeSkippedFromInside()
    {
        Assert.That(SimulationSpeedPolicy.SwitchingBoundary(90, 100, 200), Is.EqualTo(100));
        Assert.That(SimulationSpeedPolicy.SwitchingBoundary(150, 100, 200), Is.EqualTo(150));
        Assert.That(SimulationSpeedPolicy.SwitchingBoundary(200, 100, 200), Is.Null);
        Assert.That(SimulationSpeedPolicy.SwitchingBoundary(90, 100, 100), Is.Null);
    }
    [Test]
    public void StopClockWrapsToTomorrowInsteadOfTravelingBackwards()
    {
        Assert.That(SimulationSpeedPolicy.StopClock("01:00", 86300), Is.EqualTo(90000));
        Assert.That(SimulationSpeedPolicy.StopClock("01:00", 0), Is.EqualTo(3600));
        Assert.That(SimulationSpeedPolicy.StopClock("25:00", 0), Is.Null);
        Assert.That(SimulationSpeedPolicy.StopClock("12:60", 0), Is.Null);
    }
}
