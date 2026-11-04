using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class NpcServicePersistenceTests
{
    [Test]
    public void PoolUnitsAndOutboundAssignmentsRetainTheirAreaAndVehicleIdentity()
    {
        var state = new NpcServiceState();
        state.PoolPower.Add(new NpcPoolUnit { Id = "pool", InterchangeId = "andrews", AreaId = "andrews-area",
            ParkingLocation = "park", ExitLocation = "exit", PowerIds = new() { "engine", "tender", "mu" } });
        state.Services.Add(new NpcServiceRecord { PoolOutbound = true, LeadId = "assigned-engine", PowerIds = new() { "assigned-engine" } });
        var restored = JsonConvert.DeserializeObject<NpcServiceState>(JsonConvert.SerializeObject(state))!;
        Assert.Multiple(() => {
            Assert.That(restored.PoolPower[0].AreaId, Is.EqualTo("andrews-area"));
            Assert.That(restored.PoolPower[0].PowerIds, Is.EqualTo(new[] { "engine", "tender", "mu" }));
            Assert.That(restored.PoolPower[0].ExitLocation, Is.EqualTo("exit"));
            Assert.That(restored.Services[0].PoolOutbound, Is.True);
            Assert.That(restored.Services[0].LeadId, Is.EqualTo("assigned-engine"));
            Assert.That(JsonConvert.DeserializeObject<NpcServiceState>("{}")!.PoolPower, Is.Empty);
        });
    }
    [Test]
    public void RandomPlanProtectedTimetableAiCrewAndChargedOrdersSurviveSaveReload()
    {
        var state = new NpcServiceState { RandomScheduleDay = 7 };
        state.ProtectedTimetables["NPC-R-A"] = "NPC-R-A W 2F: SY 05:00, AN +90";
        state.AiCrews["NPC-R-A"] = "native-crew-id";
        state.RandomDepartures.Add(new NpcRandomDeparture { Symbol = "NPC-R-B", From = "AN", To = "SY", TrainClass = 2, Scheduled = 640000 });
        state.PulpwoodOrders.Add(new NpcPulpwoodOrder { Tag = "charged-order", Destination = "pulp.unloader", Interchange = "SY", Remaining = 2, Fulfilled = new HashSet<string> { "already-filled" } });
        state.Services.Add(new NpcServiceRecord { TrainSymbol = "NPC-R-A", CrewId = "native-crew-id", RandomFreight = true, ReservedDeliveryPounds = 120000 });
        var restored = JsonConvert.DeserializeObject<NpcServiceState>(JsonConvert.SerializeObject(state))!;
        Assert.Multiple(() =>
        {
            Assert.That(restored.RandomScheduleDay, Is.EqualTo(7));
            Assert.That(restored.RandomDepartures[0].Scheduled, Is.EqualTo(640000));
            Assert.That(restored.RandomDepartures[0].TrainClass, Is.EqualTo(2));
            Assert.That(restored.ProtectedTimetables["NPC-R-A"], Is.EqualTo(state.ProtectedTimetables["NPC-R-A"]));
            Assert.That(restored.AiCrews["NPC-R-A"], Is.EqualTo("native-crew-id"));
            Assert.That(restored.PulpwoodOrders[0].Remaining, Is.EqualTo(2));
            Assert.That(restored.PulpwoodOrders[0].Fulfilled.Add("already-filled"), Is.False);
            Assert.That(restored.Services[0].RandomFreight, Is.True);
            Assert.That(restored.Services[0].ReservedDeliveryPounds, Is.EqualTo(120000));
        });
    }

    [Test]
    public void HeldReservationsRetainIdentityDeadlineAndPayoutAcrossRepeatedReloads()
    {
        var state = new NpcServiceState();
        state.HeldDeliveries.Add(new NpcHeldDelivery
        {
            Id = "held-1", InterchangeId = "interchange", DestinationId = "mill.loader",
            CarTypeFilter = "boxcar", LoadId = "lumber", Description = "RR 123 — lumber",
            Tag = "delivery", NoPayment = false, MissedService = 86400,
            NextService = 172800, BonusStarts = 172800, Deadline = 187200,
            PremiumPercent = 100, BasePayment = 1000, BardoCarId = "car-1", CarId = "", WaybillCreated = 86400
        });
        state.Services.Add(new NpcServiceRecord { InboundPlanKnown = true, PlannedInboundCars = 2 });
        for (int i = 0; i < 3; i++) state = JsonConvert.DeserializeObject<NpcServiceState>(JsonConvert.SerializeObject(state))!;
        var held = state.HeldDeliveries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(held.BardoCarId, Is.EqualTo("car-1"));
            Assert.That(held.CarId, Is.Empty);
            Assert.That(held.DestinationId, Is.EqualTo("mill.loader"));
            Assert.That(held.LoadId, Is.EqualTo("lumber"));
            Assert.That(held.Deadline, Is.EqualTo(187200));
            Assert.That(NpcHeldDeliveryPolicy.Premium(held.BasePayment, held.PremiumPercent, 180000, held.BonusStarts, held.Deadline), Is.EqualTo(500));
            Assert.That(state.Services[0].PlannedInboundCars, Is.EqualTo(2));
            Assert.That(state.Services[0].InboundPlanKnown, Is.True);
        });
    }

    [Test]
    public void RailroadWideDelinquenciesAndPickupSnapshotsSurviveReload()
    {
        var state = new NpcServiceState();
        var expected = new NpcPickupSnapshot
        {
            CarId = "car-1", CarName = "RR 123", InterchangeId = "interchange-2",
            SpanIds = new List<string> { "pickup-span" }, WaybillCreated = 12345,
            Payout = 400, PayoutWasCredited = true
        };
        state.DelinquentPickups[expected.CarId] = expected;
        state.MissedPickupCars.Add(expected.CarId);
        state.ForfeitedPickupCars.Add(expected.CarId);
        state.ForfeitedPickupBills[expected.CarId] = new NpcPickupPayment
        { WaybillCreated = 12345, InterchangeId = "interchange-2", Amount = 400 };
        state.Services.Add(new NpcServiceRecord
        {
            Id = "service", PickupSnapshotTaken = true,
            PickupSnapshot = new List<NpcPickupSnapshot> { expected },
            Outbound = new List<NpcPickup> { new NpcPickup { CarId = expected.CarId, Queued = true, Missed = true } },
            MissesHandled = new HashSet<string> { expected.CarId }
        });
        var restored = JsonConvert.DeserializeObject<NpcServiceState>(JsonConvert.SerializeObject(state))!;
        Assert.Multiple(() =>
        {
            Assert.That(restored.DelinquentPickups["car-1"].InterchangeId, Is.EqualTo("interchange-2"));
            Assert.That(restored.DelinquentPickups["car-1"].SpanIds, Is.EqualTo(new[] { "pickup-span" }));
            Assert.That(restored.DelinquentPickups["car-1"].PayoutWasCredited, Is.True);
            Assert.That(restored.ForfeitedPickupBills["car-1"].Amount, Is.EqualTo(400));
            Assert.That(restored.Services[0].PickupSnapshotTaken, Is.True);
            Assert.That(restored.Services[0].PickupSnapshot[0].WaybillCreated, Is.EqualTo(12345));
            Assert.That(restored.Services[0].MissesHandled.Add("car-1"), Is.False);
            Assert.That(restored.MissedPickupCars.Contains("car-1"), Is.True);
            Assert.That(restored.ForfeitedPickupCars.Contains("car-1"), Is.True);
            Assert.That(restored.Services[0].Outbound[0].Queued && restored.Services[0].Outbound[0].Missed, Is.True);
        });
    }

    [TestCase("Approaching")]
    [TestCase("Setting out")]
    [TestCase("Picking up")]
    [TestCase("Departing")]
    public void ReloadPreservesIdentityOwnershipAndPendingWork(string phase)
    {
        var state = new NpcServiceState { LastScan = 86400 };
        state.ThroughDepartures["1:Z-01"] = 86400;
        state.Services.Add(new NpcServiceRecord
        {
            Id = "service-1", InterchangeId = "interchange-1", LeadId = "engine-1", Phase = phase,
            Scheduled = 86400, NextTransfer = 86520, TransferSecondsPerCar = 60, SetoutDone = 1, PickupDone = 1,
            OrdersPrepared = true, OutboundCollected = true, Suspended = true,
            PowerIds = new List<string> { "engine-1" }, Inbound = new List<string> { "in-1", "in-2" },
            Outbound = new List<NpcPickup> { new NpcPickup { CarId = "out-1" }, new NpcPickup { CarId = "out-2", Bardo = "loader" } }
        });
        var restored = JsonConvert.DeserializeObject<NpcServiceState>(JsonConvert.SerializeObject(state))!;
        Assert.That(restored.Services, Has.Count.EqualTo(1));
        var service = restored.Services[0];
        Assert.Multiple(() =>
        {
            Assert.That(service.Id, Is.EqualTo("service-1"));
            Assert.That(service.InterchangeId, Is.EqualTo("interchange-1"));
            Assert.That(service.Phase, Is.EqualTo(phase));
            Assert.That(service.Inbound.Skip(service.SetoutDone), Is.EqualTo(new[] { "in-2" }));
            Assert.That(service.Outbound.Skip(service.PickupDone).Select(c => c.CarId), Is.EqualTo(new[] { "out-2" }));
            Assert.That(service.Outbound[1].Bardo, Is.EqualTo("loader"));
            Assert.That(service.NextTransfer, Is.EqualTo(86520));
            Assert.That(service.TransferSecondsPerCar, Is.EqualTo(60));
            Assert.That(service.OutboundCollected && service.OrdersPrepared && service.Suspended, Is.True);
            Assert.That(restored.ThroughDepartures.ContainsKey("1:Z-01"), Is.True);
            Assert.That(restored.LastScan, Is.EqualTo(86400));
        });
    }

    [Test]
    public void MultipleServicesAndTheirClocksSurviveRepeatedReloads()
    {
        var state = new NpcServiceState();
        state.Services.Add(new NpcServiceRecord { Id = "a", InterchangeId = "a", NextTransfer = 120, TransferSecondsPerCar = 60 });
        state.Services.Add(new NpcServiceRecord { Id = "b", InterchangeId = "b", NextTransfer = 240, TransferSecondsPerCar = 120 });
        state.Services.Add(new NpcServiceRecord { Id = "through", TrainSymbol = "Z-01", StopIndex = 3 });
        for (int i = 0; i < 3; i++) state = JsonConvert.DeserializeObject<NpcServiceState>(JsonConvert.SerializeObject(state))!;
        Assert.That(state.Services.Select(s => s.Id), Is.EqualTo(new[] { "a", "b", "through" }));
        Assert.That(state.Services.Select(s => s.NextTransfer), Is.EqualTo(new[] { 120d, 240d, 0d }));
        Assert.That(state.Services[2].StopIndex, Is.EqualTo(3));
    }

    [Test]
    public void AnnouncementsSurviveReloadWithoutSuppressingOtherLegsOrServices()
    {
        var service = new NpcServiceRecord();
        service.Announcements.Add("depart-1");
        service.Announcements.Add("approach-1");
        var restored = JsonConvert.DeserializeObject<NpcServiceRecord>(JsonConvert.SerializeObject(service))!;
        Assert.That(restored.Announcements.Add("approach-1"), Is.False);
        Assert.That(restored.Announcements.Add("approach-2"), Is.True);
        Assert.That(new NpcServiceRecord().Announcements.Add("approach-1"), Is.True);
        Assert.That(JsonConvert.DeserializeObject<NpcServiceRecord>("{\"Id\":\"legacy\"}")!.Announcements, Is.Empty);
    }
}
