using NUnit.Framework;
using System.Linq;

namespace RMROC451.TweaksAndThings.Tests;

public class TimetableConnectivityTests
{
    private static TimetableConnectivityPolicy.Edge[] Railroad(bool bridge) => new[]
    {
        new TimetableConnectivityPolicy.Edge("ewh", "Whittier", "Ela", true, true),
        new TimetableConnectivityPolicy.Edge("sylva", "Whittier", "Sylva", true, false),
        new TimetableConnectivityPolicy.Edge("bridge", "Ela", "ElaWest", bridge, false),
        new TimetableConnectivityPolicy.Edge("bryson", "ElaWest", "Bryson", true, false),
        new TimetableConnectivityPolicy.Edge("andrews", "Bryson", "Andrews", true, false)
    };

    [Test]
    public void BrokenBridgeHidesDisconnectedTerritoryButRetainsConnectedLockedSylva()
    {
        var map = TimetableConnectivityPolicy.Components(Railroad(false), out var owned);
        Assert.That(owned.Contains(map["sylva"]), Is.True);
        Assert.That(owned.Contains(map["bryson"]), Is.False);
        Assert.That(map.ContainsKey("bridge"), Is.False);
    }

    [Test]
    public void RepairingBridgeConnectsLockedTerritoryWithoutPurchasingIt()
    {
        var map = TimetableConnectivityPolicy.Components(Railroad(true), out var owned);
        Assert.That(owned.Contains(map["andrews"]), Is.True);
        Assert.That(map["andrews"], Is.EqualTo(map["ewh"]));
    }

    [Test]
    public void ReverseStartUsesItsOwnPhysicalComponent()
    {
        var edges = Railroad(false).Select(e => new TimetableConnectivityPolicy.Edge(e.Id, e.A, e.B, e.Enabled, e.Id == "bryson"));
        var map = TimetableConnectivityPolicy.Components(edges, out var owned);
        Assert.That(owned.Contains(map["andrews"]), Is.True);
        Assert.That(owned.Contains(map["sylva"]), Is.False);
    }
}
