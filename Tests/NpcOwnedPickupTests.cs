using NUnit.Framework;
using Newtonsoft.Json;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcOwnedPickupTests
{
    [TestCase(true, "buy", true, true)]
    [TestCase(true, "buy", false, false)]
    [TestCase(true, "sell", false, true)]
    [TestCase(false, "", false, true)]
    public void LoaderShipmentsDoNotSellOrdinaryOwnedCars(bool owned, string tag, bool loader, bool expected)
    {
        Assert.That(NpcServicePolicy.CanCollectOwnedCar(owned, tag, loader), Is.EqualTo(expected));
    }
    [Test] public void LoaderDestinationAndPickupTracksSurviveReload()
    {
        var snapshot = new NpcPickupSnapshot { InterchangeId = "andrews.t1", DestinationId = "andrews.coal", SpanIds = new() { "coal-track" } };
        var loaded = JsonConvert.DeserializeObject<NpcPickupSnapshot>(JsonConvert.SerializeObject(snapshot))!;
        Assert.That(loaded.DestinationId, Is.EqualTo("andrews.coal"));
        Assert.That(loaded.SpanIds, Is.EqualTo(new[] { "coal-track" }));
        Assert.That(JsonConvert.DeserializeObject<NpcPickupSnapshot>("{\"InterchangeId\":\"andrews.t1\"}")!.DestinationId, Is.Empty);
    }
}
