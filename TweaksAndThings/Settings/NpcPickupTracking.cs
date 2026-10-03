using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.State;
using Model;
using Model.Ops;

namespace RMROC451.TweaksAndThings;

internal static class NpcPickupTracking
{
    internal static void Snapshot(NpcServiceRecord service, Interchange interchange, IReadOnlyList<Car> cars)
    {
        service.PickupSnapshot = cars.Select(car =>
        {
            var wb = car.Waybill!.Value;
            var result = new NpcPickupSnapshot { CarId = car.id, CarName = car.DisplayName, InterchangeId = interchange.Identifier,
                DestinationId = wb.Destination.Identifier,
                SpanIds = SimulatedInterchangeService.PickupSpans(interchange, car).Where(s => s.Contains(car.OpsLocation)).Select(s => s.id).ToList(),
                WaybillCreated = wb.Created.TotalSeconds, PayoutWasCredited = wb.Completed,
                Payout = Math.Max(0, wb.PaymentOnArrival - wb.ConditionFineForCarCondition(car.Condition)) };
            if (NpcServiceStore.State.DelinquentPickups.TryGetValue(car.id, out var old) && old.WaybillCreated != result.WaybillCreated)
                Collected(car.id);
            if (NpcServiceStore.State.ForfeitedPickupBills.TryGetValue(car.id, out var forfeited) && !Matches(result, forfeited))
            {
                NpcServiceStore.State.ForfeitedPickupCars.Remove(car.id);
                NpcServiceStore.State.ForfeitedPickupBills.Remove(car.id);
            }
            return result;
        }).ToList();
        foreach (var missed in NpcServiceStore.State.DelinquentPickups.Values.Where(p => p.InterchangeId == interchange.Identifier).ToList())
            if (!service.PickupSnapshot.Any(p => p.CarId == missed.CarId) &&
                !NpcServiceStore.State.Services.Any(s => s.Id != service.Id && s.PoolOutbound && s.Outbound.Any(p => p.CarId == missed.CarId && !p.Missed)))
                service.PickupSnapshot.Add(missed);
        service.PickupSnapshotTaken = true;
        NpcServiceStore.Save();
    }

    internal static bool Available(NpcPickupSnapshot expected, bool queued = false)
    {
        if (!TrainController.Shared.TryGetCarForId(expected.CarId, out var car) || car.IsInBardo || !car.Waybill.HasValue) return false;
        var wb = car.Waybill.Value;
        if (!queued && (wb.Created.TotalSeconds != expected.WaybillCreated || wb.Destination.Identifier !=
            (expected.DestinationId.Length > 0 ? expected.DestinationId : expected.InterchangeId))) return false;
        var interchange = OpsController.Shared.AllInterchanges.FirstOrDefault(i => i.Identifier == expected.InterchangeId);
        return interchange != null && interchange.TrackSpans.Concat(interchange.Loaders.SelectMany(l => l.TrackSpans))
            .Any(s => expected.SpanIds.Contains(s.id) && s.Contains(car.OpsLocation));
    }

    internal static void Missed(NpcServiceRecord service, NpcPickupSnapshot expected)
    {
        if (!service.MissesHandled.Add(expected.CarId)) return;
        var state = NpcServiceStore.State;
        state.DelinquentPickups[expected.CarId] = expected;
        bool second = !state.MissedPickupCars.Add(expected.CarId);
        string consequence = "Return it to its pickup span before the next service, or its payout will be forfeited.";
        if (second && state.ForfeitedPickupCars.Add(expected.CarId))
        {
            int reclaim = expected.PayoutWasCredited ? expected.Payout : 0;
            if (state.PickupPayments.TryGetValue(expected.CarId, out var paid) && Matches(expected, paid)) reclaim = paid.Amount;
            state.ForfeitedPickupBills[expected.CarId] = new NpcPickupPayment
            { WaybillCreated = expected.WaybillCreated, InterchangeId = expected.InterchangeId, Amount = reclaim };
            if (reclaim > 0) StateManager.Shared.ApplyToBalance(-reclaim, Ledger.Category.Freight, null,
                "Second missed NPC pickup: " + expected.CarName, 0, quiet: true);
            if (TrainController.Shared.TryGetCarForId(expected.CarId, out var car) && car.Waybill.HasValue &&
                car.Waybill.Value.Created.TotalSeconds == expected.WaybillCreated && car.Waybill.Value.Destination.Identifier ==
                    (expected.DestinationId.Length > 0 ? expected.DestinationId : expected.InterchangeId))
            {
                var wb = car.Waybill.Value;
                wb.PaymentOnArrival = 0;
                car.SetWaybill(wb);
            }
            consequence = reclaim > 0 ? $"Second missed pickup: payout forfeited; {reclaim:C0} reclaimed." : "Second missed pickup: payout forfeited.";
        }
        else if (second) consequence = "Payout already forfeited; return the car to its pickup span.";
        NpcServiceStore.Save();
        NpcTrafficMessages.Send(service, "missed-" + expected.CarId, $"Missed pickup: {expected.CarName} at {service.InterchangeId}. {consequence}");
    }

    internal static bool Matches(NpcPickupSnapshot expected, NpcPickupPayment paid) =>
        expected.InterchangeId == paid.InterchangeId && expected.WaybillCreated == paid.WaybillCreated;

    internal static string NextAttemptText(NpcPickupSnapshot expected)
    {
        var state = NpcServiceStore.State;
        var interchange = OpsController.Shared.AllInterchanges.FirstOrDefault(i => i.Identifier == expected.InterchangeId);
        if (interchange == null) return "Next attempt unavailable";
        var service = state.Services.FirstOrDefault(s => s.InterchangeId == expected.InterchangeId &&
            s.PickupSnapshot.Any(p => p.CarId == expected.CarId) && !s.MissesHandled.Contains(expected.CarId));
        double next = service?.Scheduled ?? interchange.GetNextServiceTime(TimeWeather.Now, out _).TotalSeconds;
        if (service != null)
        {
            int index = service.PickupSnapshot.FindIndex(p => p.CarId == expected.CarId);
            double start = service.NextTransfer > 0 ? Math.Max(TimeWeather.Now.TotalSeconds, service.NextTransfer) : Math.Max(TimeWeather.Now.TotalSeconds, service.Scheduled);
            next = start + (Math.Max(0, service.Inbound.Count - service.SetoutDone) + Math.Max(0, index - service.PickupDone)) * service.TransferSecondsPerCar;
        }
        double remaining = Math.Max(0, next - TimeWeather.Now.TotalSeconds);
        return remaining <= 0 ? "Next attempt: awaiting service" : "Next attempt ~" + TimeSpan.FromSeconds(remaining).ToString(@"d\d\ hh\h\ mm\m");
    }

    internal static void Collected(string id)
    {
        var state = NpcServiceStore.State;
        state.MissedPickupCars.Remove(id);
        state.DelinquentPickups.Remove(id);
        // Keep forfeiture until this waybill is retired: moving the car must not
        // make an unpaid forfeited bill eligible for payment again.
        NpcServiceStore.Save();
    }

    internal static bool PaymentForfeited(string id, Waybill wb) => NpcServiceStore.Load() &&
        NpcServiceStore.State.ForfeitedPickupCars.Contains(id) &&
        NpcServiceStore.State.ForfeitedPickupBills.TryGetValue(id, out var expected) &&
        expected.WaybillCreated == wb.Created.TotalSeconds && expected.InterchangeId == wb.Destination.Identifier;
}
