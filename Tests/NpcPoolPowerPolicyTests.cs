using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class NpcPoolPowerPolicyTests
{
    [TestCase("_f.coupled")]
    [TestCase("_r.coupled")]
    [TestCase("f.cutLever")]
    [TestCase("r.cutLever")]
    public void InternalPowerConnectionsAreLockedForUsersButAvailableToService(string key)
    {
        Assert.That(NpcPoolPowerPolicy.BlocksConnection(true, true, key, false), Is.True);
        Assert.That(NpcPoolPowerPolicy.BlocksConnection(true, true, key, true), Is.False);
        Assert.That(NpcPoolPowerPolicy.BlocksConnection(true, false, key, false), Is.False);
    }

    [TestCase("_f.airConnected")]
    [TestCase("_r.airConnected")]
    [TestCase("f.anglecock")]
    [TestCase("r.anglecock")]
    public void AirConnectionsRemainUsable(string key)
    {
        Assert.That(NpcPoolPowerPolicy.BlocksConnection(true, true, key, false), Is.False);
    }

    [TestCase(true, true, 50f, 60f, false)]
    [TestCase(true, false, 90f, 110f, true)]
    [TestCase(false, true, 110f, 90f, false)]
    [TestCase(false, false, 120f, 110f, false)]
    [TestCase(false, false, 110f, 120f, true)]
    [TestCase(false, false, 110f, 110f, true)]
    public void AreaBoundaryAllowsInternalMovementAndRecovery(bool inside, bool futureInside, float distance, float futureDistance, bool blocked)
    {
        Assert.That(NpcPoolPowerPolicy.BlocksMovement(inside, futureInside, distance, futureDistance), Is.EqualTo(blocked));
    }
}
