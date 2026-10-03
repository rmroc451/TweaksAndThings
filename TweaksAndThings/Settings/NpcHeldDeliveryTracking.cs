using System;
using System.Linq;
using Game;
using Game.State;
using Model;
using Model.Ops;
using Model.Ops.Definition;
using UI.Builder;

namespace RMROC451.TweaksAndThings;

internal static class NpcHeldDeliveryTracking
{
    private static NpcServiceState? restoredState;
    internal static void EnsureOrdersAfterLoad()
    {
        if (ReferenceEquals(restoredState, NpcServiceStore.State)) return;
        restoredState = NpcServiceStore.State;
        foreach (var interchange in OpsController.Shared.AllInterchanges) Restore(interchange);
    }
    private static string Key(Order order) => order.Destination.Identifier + "|" + order.CarTypeFilter.queryString + "|" + order.Load?.id;
    private static string Key(NpcHeldDelivery held) => held.DestinationId + "|" + held.CarTypeFilter + "|" + (held.LoadId.Length == 0 ? null : held.LoadId);

    internal static void Restore(Interchange interchange)
    {
        if (!StateManager.IsHost || !NpcServiceStore.Load() || TrainController.Shared == null) return;
        ReconcileCars();
        foreach (var held in NpcServiceStore.State.HeldDeliveries.Where(h => h.InterchangeId == interchange.Identifier && h.CarId.Length == 0 && h.BardoCarId.Length > 0))
            if (!interchange.Orders.OfType<ReturnFromBardoOrder>().Any(o => o.CarId == held.BardoCarId)) interchange.OrderReturnFromBardo(held.BardoCarId);
        foreach (var group in NpcServiceStore.State.HeldDeliveries.Where(h => h.InterchangeId == interchange.Identifier && h.CarId.Length == 0 && h.BardoCarId.Length == 0).GroupBy(Key))
        {
            var held = group.First();
            int current = interchange.Orders.OfType<Order>().Where(o => Key(o) == group.Key).Sum(o => o.CarCount);
            int missing = NpcHeldDeliveryPolicy.MissingReservations(group.Count(), current);
            if (missing == 0) continue;
            try
            {
                var load = held.LoadId.Length == 0 ? null : CarPrototypeLibrary.instance.LoadForId(held.LoadId);
                if (held.LoadId.Length > 0 && load == null) throw new InvalidOperationException("Held load no longer exists: " + held.LoadId);
                interchange.AddOrder(new Order(new CarTypeFilter(held.CarTypeFilter), load,
                    OpsController.Shared.ResolveOpsCarPosition(held.DestinationId), missing, held.Tag, held.NoPayment));
            }
            catch (Exception ex) { TweaksAndThingsPlugin.LogException("Restore held delivery " + held.Id, ex); }
        }
    }

    internal static void CaptureRemaining(Interchange interchange, GameDateTime missedService, NpcInboundPlan? plan = null)
    {
        if (!NpcServiceStore.Load()) return;
        var settings = TweaksAndThingsPlugin.Instance!.settings!;
        // Promise the next daily service; a future extra service can advance this window.
        var next = interchange.GetNextServiceTime(missedService.AddingDays(1), out _, true);
        if (interchange.TryGetExtraScheduled(out var extra) && extra > missedService && extra < next) next = extra;
        foreach (var group in interchange.Orders.OfType<Order>().Where(o => o.CarCount > 0).GroupBy(Key))
        {
            var order = group.First();
            int reserved = NpcServiceStore.State.HeldDeliveries.Count(h => h.InterchangeId == interchange.Identifier && h.CarId.Length == 0 && h.BardoCarId.Length == 0 && Key(h) == group.Key);
            int missing = NpcHeldDeliveryPolicy.MissingReservations(group.Sum(o => o.CarCount), reserved);
            var detail = plan?.DescribeDeferred(order) ?? (order.CarTypeFilter.queryString, -1);
            int payment = order.NoPayment ? 0 : detail.Item2 >= 0 ? detail.Item2 : OpsController.Shared.PaymentForMove(interchange, order.Destination,
                order.Load == null ? 0 : (int)Math.Ceiling(order.Load.Pounds(order.Load.NominalQuantityPerCarLoad) / 2000));
            for (int i = 0; i < missing; i++) NpcServiceStore.State.HeldDeliveries.Add(new NpcHeldDelivery
            {
                Id = Guid.NewGuid().ToString("N"), InterchangeId = interchange.Identifier,
                DestinationId = order.Destination.Identifier, CarTypeFilter = order.CarTypeFilter.queryString,
                LoadId = order.Load?.id ?? string.Empty, Tag = order.Tag ?? string.Empty, NoPayment = order.NoPayment,
                Description = detail.Item1 + (order.Load == null ? " — empty" : " — " + order.Load.description),
                MissedService = missedService.TotalSeconds, NextService = next.TotalSeconds,
                BonusStarts = next.TotalSeconds, BasePayment = payment,
                Deadline = NpcHeldDeliveryPolicy.Deadline(next.TotalSeconds, settings.HeldDeliveryDeadlineHours),
                PremiumPercent = settings.HeldDeliveryPremiumPercent
            });
        }
        NpcServiceStore.Save();
    }

    internal static void RegisterCar(Car car, double serviceTime, bool loadingRetry = false)
    {
        if (!car.Waybill.HasValue) return;
        var wb = car.Waybill.Value;
        if (NpcServiceStore.State.HeldDeliveries.Any(h => h.CarId == car.id)) return;
        string load = car.GetLoadInfo(0)?.LoadId ?? string.Empty;
        var held = NpcServiceStore.State.HeldDeliveries.FirstOrDefault(h => h.CarId.Length == 0 && h.BardoCarId == car.id) ??
            NpcServiceStore.State.HeldDeliveries.FirstOrDefault(h => h.CarId.Length == 0 && h.BardoCarId.Length == 0 &&
            h.DestinationId == wb.Destination.Identifier && h.InterchangeId == wb.Origin?.Identifier &&
            h.LoadId == load && h.Tag == (wb.Tag ?? "") && new CarTypeFilter(h.CarTypeFilter).Matches(car.CarType) &&
            (wb.Created.TotalSeconds > h.MissedService || loadingRetry && wb.Created.TotalSeconds == h.MissedService));
        if (held == null) return;
        if (loadingRetry && wb.Created.TotalSeconds == held.MissedService && held.BardoCarId.Length == 0)
        { NpcServiceStore.State.HeldDeliveries.Remove(held); return; }
        held.CarId = car.id;
        held.WaybillCreated = wb.Created.TotalSeconds;
        held.NextService = serviceTime;
        held.BasePayment = wb.PaymentOnArrival;
        if (serviceTime < held.BonusStarts)
        { double window = held.Deadline - held.BonusStarts; held.BonusStarts = serviceTime; held.Deadline = serviceTime + window; }
        if (!held.Description.StartsWith(car.DisplayName + " — ", StringComparison.Ordinal)) held.Description = car.DisplayName + " — " + held.Description;
    }

    // Native auto-magic service can also satisfy reservations after a mode change.
    internal static void ReconcileCars()
    {
        if (!NpcServiceStore.State.HeldDeliveries.Any(h => h.CarId.Length == 0)) return;
        foreach (var car in TrainController.Shared.Cars.Where(c => !c.IsInBardo && c.Waybill.HasValue))
            RegisterCar(car, car.Waybill!.Value.Created.TotalSeconds);
    }

    internal static void HoldCar(NpcServiceRecord service, Interchange interchange, Car car)
    {
        if (!car.Waybill.HasValue) throw new InvalidOperationException("Cannot hold an inbound car without its waybill");
        var wb = car.Waybill.Value;
        var held = NpcServiceStore.State.HeldDeliveries.FirstOrDefault(h => h.CarId == car.id);
        if (held == null)
        {
            double next = interchange.GetNextServiceTime(new GameDateTime((float)service.Scheduled).AddingDays(1), out _, true).TotalSeconds;
            var settings = TweaksAndThingsPlugin.Instance!.settings!;
            held = new NpcHeldDelivery
            {
                Id = Guid.NewGuid().ToString("N"), InterchangeId = interchange.Identifier, DestinationId = wb.Destination.Identifier,
                CarTypeFilter = car.CarType, LoadId = car.GetLoadInfo(0)?.LoadId ?? "", Tag = wb.Tag ?? "",
                Description = car.DisplayName + " — " + car.CarType, BasePayment = wb.PaymentOnArrival,
                MissedService = service.Scheduled, NextService = next, BonusStarts = next,
                Deadline = NpcHeldDeliveryPolicy.Deadline(next, settings.HeldDeliveryDeadlineHours), PremiumPercent = settings.HeldDeliveryPremiumPercent
            };
            NpcServiceStore.State.HeldDeliveries.Add(held);
        }
        held.CarId = string.Empty;
        held.BardoCarId = car.id;
        NpcTrainOperations.Trusted(() => { NpcTrainOperations.Marker(car, false); TrainController.Shared.MoveToBardo(car.id, interchange.Identifier); });
        interchange.OrderReturnFromBardo(car.id);
        service.Inbound.Remove(car.id);
        ModDiagnosticLog.Write("HOLD", $"Arrival yard full: service={service.Id}, holding actual car={car.id}, destination={held.DestinationId}; native car, load and waybill retained in bardo.");
        NpcServiceStore.Save();
    }

    internal static string BonusSummary(NpcHeldDelivery held)
    {
        double now = TimeWeather.Now.TotalSeconds;
        int premium = held.NoPayment ? 0 : NpcHeldDeliveryPolicy.Premium(held.BasePayment, held.PremiumPercent, now, held.BonusStarts, held.Deadline);
        string remaining = "~" + Math.Round(Math.Max(0, held.Deadline - now) / 3600, MidpointRounding.AwayFromZero).ToString("00") + "h";
        return $"{remaining} remaining · payout ${held.BasePayment + premium:N0} (${held.BasePayment:N0} base + ${premium:N0} premium)" +
            (held.CarId.Length == 0 ? " · estimate until dispatch" : "") +
            (now >= held.Deadline ? " · premium expired" : now < held.BonusStarts ? " · bonus timer starts at next service" : "");
    }

    internal static string CarBonusSummary(string carId)
    {
        if (!NpcServiceStore.Load()) return "Unavailable";
        var held = NpcServiceStore.State.HeldDeliveries.FirstOrDefault(h => h.CarId == carId);
        return held == null ? "Delivery completed" : BonusSummary(held);
    }

    internal static void BuildList(UIPanelBuilder builder, string? industryId = null, string? interchangeId = null)
    {
        if (!NpcServiceStore.Load()) return;
        var rows = NpcServiceStore.State.HeldDeliveries.Where(h => (industryId == null || h.DestinationId.StartsWith(industryId + ".", StringComparison.Ordinal)) &&
            (interchangeId == null || h.InterchangeId == interchangeId)).ToList();
        if (rows.Count == 0) { if (industryId == null) builder.AddLabel("No held-over interchange deliveries."); return; }
        builder.AddSection("Missed interchange deliveries", list =>
        {
            list.AddLabel("Reserved cars remain on order; industries do not order replacements.");
            foreach (var held in rows.OrderBy(h => h.DestinationId).ThenBy(h => h.MissedService))
            {
                list.AddLabel(held.Description + " → " + DestinationName(held));
                list.AddLabel((held.CarId.Length == 0 ? "Held for next service" : "Dispatched; deliver to industry") +
                    $"; missed {new GameDateTime((float)held.MissedService)}; next service {new GameDateTime((float)held.NextService)}");
                list.AddLabel(() => BonusSummary(held), UIPanelBuilder.Frequency.Periodic);
                list.AddLabel("Deliver by " + new GameDateTime((float)held.Deadline));
                if (held.CarId.Length > 0 && TrainController.Shared.TryGetCarForId(held.CarId, out var car))
                    list.AddButtonCompact("Locate " + car.DisplayName, () => CameraSelector.shared.ZoomToCar(car));
            }
        });
    }

    private static string DestinationName(NpcHeldDelivery held)
    {
        try { return OpsController.Shared.ResolveOpsCarPosition(held.DestinationId).DisplayName; }
        catch { return held.DestinationId; }
    }
}
