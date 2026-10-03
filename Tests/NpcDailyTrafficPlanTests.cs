using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcDailyTrafficPlanTests
{
    [Test]
    public void FutureTrainsRetainProtectedSchedulesWithoutAllocatingCarsOrCrews()
    {
        var state = new NpcServiceState();
        state.TrafficPlans.Add(new NpcTrafficPlan { Symbol = "NI001", ReturnSymbol = "NI001R", InterchangeId = "SY", Dispatch = 90000, ServiceTime = 95000 });
        state.ProtectedTimetables["NI001"] = "NI001 W 3F: AN 01:00, SY +60";
        state.ProtectedTimetables["NI001R"] = "NI001R E 3F: SY 02:00, AN +60";
        state.BoardMarkerDays["board:NI001"] = 1;
        state.RandomDepartures.Add(new NpcRandomDeparture { Symbol = "NR001", FreightDefinitions = new List<string> { "boxcar", "hopper", "boxcar" } });
        var loaded = JsonConvert.DeserializeObject<NpcServiceState>(JsonConvert.SerializeObject(state))!;
        Assert.That(NpcTrafficPlanPolicy.Owns(loaded, "NI001"), Is.True);
        Assert.That(NpcTrafficPlanPolicy.Owns(loaded, "NI001R"), Is.True);
        Assert.That(loaded.Services, Is.Empty);
        Assert.That(loaded.AiCrews, Is.Empty);
        Assert.That(loaded.TrafficPlans[0].Dispatched, Is.False);
        Assert.That(loaded.BoardMarkerDays["board:NI001"], Is.EqualTo(1));
        Assert.That(loaded.RandomDepartures[0].FreightDefinitions, Is.EqualTo(new[] { "boxcar", "hopper", "boxcar" }));
    }

    [Test]
    public void DispatchClaimsOnlyTheMatchingInterchangeAndService()
    {
        var state = new NpcServiceState();
        var expected = new NpcTrafficPlan { Symbol = "NI1", InterchangeId = "SY", ServiceTime = 5000 };
        state.TrafficPlans.Add(expected);
        state.TrafficPlans.Add(new NpcTrafficPlan { Symbol = "NI2", InterchangeId = "AN", ServiceTime = 5000 });
        state.TrafficPlans.Add(new NpcTrafficPlan { Symbol = "NI3", InterchangeId = "SY", ServiceTime = 8000 });
        Assert.That(NpcTrafficPlanPolicy.ForInterchange(state, "SY", 5000.5), Is.SameAs(expected));
        expected.Dispatched = true;
        Assert.That(NpcTrafficPlanPolicy.ForInterchange(state, "SY", 5000), Is.Null);
        Assert.That(NpcTrafficPlanPolicy.Owns(state, "unowned"), Is.False);
    }

    [Test]
    public void ActiveReturnSchedulesAreNotMistakenForOrphans()
    {
        var state = new NpcServiceState();
        state.Services.Add(new NpcServiceRecord { TrainSymbol = "NI1", ReturnTrainSymbol = "NI1R" });
        Assert.That(NpcTrafficPlanPolicy.Owns(state, "NI1R"), Is.True);
        state.Services.Clear();
        Assert.That(NpcTrafficPlanPolicy.Owns(state, "NI1R"), Is.False);
    }

    [TestCase(86300, 87000, 80000, true)]
    [TestCase(87000, 90000, 80000, false)]
    [TestCase(86000, 87000, 86400, true)]
    [TestCase(85000, 86000, 86400, false)]
    public void PlanningIncludesTonightDeparturesForTomorrowMorning(double departure, double service, double now, bool expected)
        => Assert.That(NpcTrafficPlanPolicy.InPlanningDay(departure, service, now), Is.EqualTo(expected));

    [Test]
    public void BoardMarkersAreAddedOncePerBoardPerDayEvenIfPlayerDeletesThem()
    {
        var ledger = new Dictionary<string, int>();
        Assert.That(NpcBoardMarkerPolicy.TryClaim(ledger, "boardA", "NI1", 1), Is.True);
        Assert.That(NpcBoardMarkerPolicy.TryClaim(ledger, "boardA", "NI1", 1), Is.False);
        var loaded = JsonConvert.DeserializeObject<Dictionary<string, int>>(JsonConvert.SerializeObject(ledger))!;
        Assert.That(NpcBoardMarkerPolicy.TryClaim(loaded, "boardA", "NI1", 1), Is.False);
        Assert.That(NpcBoardMarkerPolicy.TryClaim(loaded, "boardB", "NI1", 1), Is.True);
        Assert.That(NpcBoardMarkerPolicy.TryClaim(loaded, "boardA", "NI1", 2), Is.True);
    }

    [Test]
    public void EndOfDayCleanupTargetsOnlyModOwnedOldMarkers()
    {
        Assert.That(NpcBoardMarkerPolicy.Expired(NpcBoardMarkerPolicy.Key(1, "NI1"), 1, 2), Is.True);
        Assert.That(NpcBoardMarkerPolicy.Expired(NpcBoardMarkerPolicy.Key(2, "NI1"), 2, 2), Is.False);
        Assert.That(NpcBoardMarkerPolicy.Expired("marker-player-owned", 1, 2), Is.False);
    }

    [Test]
    public void OldSavesDefaultToEmptyPlansAndMarkerLedger()
    {
        var state = JsonConvert.DeserializeObject<NpcServiceState>("{\"RandomScheduleDay\":3}")!;
        Assert.That(state.TrafficPlans, Is.Empty);
        Assert.That(state.BoardMarkerDays, Is.Empty);
    }
}
