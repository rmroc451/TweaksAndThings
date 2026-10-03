using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Game;
using Game.State;
using Model;
using Model.Ops;
using UI;
using UI.Builder;
using UI.Common;

namespace RMROC451.TweaksAndThings;

internal static class NpcPulpwoodOrdering
{
    private static readonly ConditionalWeakTable<IndustryUnloader, UIState<int>> quantities = new();
    internal static void Build(Industry industry, UIPanelBuilder builder)
    {
        foreach (var destination in industry.Components.OfType<IndustryUnloader>().Where(c =>
            !c.ProgressionDisabled && c.load != null && c.load.id.IndexOf("pulpwood", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            var count = quantities.GetValue(destination, _ => new UIState<int>(1));
            var load = destination.load;
            int percent = TweaksAndThingsPlugin.Instance.settings.PulpwoodOrderingFeePercent;
            double unitValue = Math.Max(load.costPerUnit, load.payPerQuantity);
            builder.AddSection("Order pulpwood from interchange", section =>
            {
                section.AddField("Cars", section.AddInputField(count.Value.ToString(), value =>
                { if (int.TryParse(value, out int cars)) count.Value = Math.Max(1, Math.Min(50, cars)); }, "1–50", 2));
                section.AddField("Ordering fee", () => $"{percent}% of load value: {NpcRandomTrafficPolicy.OrderingFee(load.NominalQuantityPerCarLoad * count.Value, unitValue, percent):C0}", UIPanelBuilder.Frequency.Periodic);
                section.AddLabel("Fee is estimated at booking and adjusted to the actual car capacity when filled. Next interchange service; normal waybills and yard capacity apply. Pending and held cars count toward demand.");
                section.AddButtonCompact("Order pulpwood", () => Order(destination, count.Value)).Disable(!StateManager.IsHost);
                if (!StateManager.IsHost) section.AddLabel("Ask the host to place orders.");
            });
        }
    }

    private static void Order(IndustryUnloader destination, int requested)
    {
        if (!StateManager.IsHost || !NpcServiceStore.Load()) return;
        var load = destination.load;
        var ctx = destination.CreateContext(TimeWeather.Now, 0);
        // Include native orders, waybilled cars and the held-over orders already restored to the backend.
        double unmet = destination.maxStorage * destination.Industry.GetContractMultiplier() - ctx.QuantityInStorage(load) - ctx.QuantityOnOrder(load);
        int count = Math.Min(requested, Math.Max(0, (int)Math.Ceiling(unmet / load.NominalQuantityPerCarLoad)));
        if (count == 0) { Toast.Present("Pulpwood demand is already covered by storage and pending deliveries."); return; }
        double unitValue = Math.Max(load.costPerUnit, load.payPerQuantity);
        int percent = TweaksAndThingsPlugin.Instance.settings.PulpwoodOrderingFeePercent;
        if (unitValue <= 0 && percent > 0) { Toast.Present("This pulpwood load has no defined purchase or delivery value for calculating its ordering fee."); return; }
        var interchange = OpsController.Shared.InterchangeForPosition(destination, null);
        if (interchange == null || interchange.Disabled || interchange.ProgressionDisabled)
        { Toast.Present("No enabled interchange can serve this industry."); return; }
        int feePerCar = NpcRandomTrafficPolicy.OrderingFee(load.NominalQuantityPerCarLoad, unitValue, percent);
        var record = new NpcPulpwoodOrder { Tag = "TAT-PULP-" + Guid.NewGuid().ToString("N"), Destination = destination.Identifier,
            Interchange = interchange.Identifier, Remaining = count, FeePercent = percent, UnitValue = unitValue, EstimatedFeePerCar = feePerCar };
        // Explicit imports bypass the load's automatic-import eligibility without changing its global definition.
        interchange.AddOrder(new Order(destination.carTypeFilter, load, destination, count, record.Tag, false));
        int fee = feePerCar * count;
        destination.Industry.ApplyToBalance(-fee, Ledger.Category.Freight, "Pulpwood interchange ordering fee", count, quiet: true);
        NpcServiceStore.State.PulpwoodOrders.Add(record);
        NpcServiceStore.Save();
        Network.Multiplayer.Broadcast($"Ordered {count} pulpwood cars for {destination.Industry.name}; {fee:C0} ordering fee. Next service at {NpcRandomTrafficPolicy.Clock(interchange.GetNextServiceTime(TimeWeather.Now, out _).TotalSeconds)}.");
        ModDiagnosticLog.Write("PULPWOOD", $"destination={record.Destination}; interchange={record.Interchange}; cars={count}; fee={fee}; tag={record.Tag}");
    }

    internal static void Restore(Interchange interchange)
    {
        if (!StateManager.IsHost || !NpcServiceStore.Load()) return;
        foreach (var record in NpcServiceStore.State.PulpwoodOrders.Where(o => o.Interchange == interchange.Identifier).ToList())
        {
            foreach (var car in TrainController.Shared.Cars.Where(c => c.Waybill?.Tag == record.Tag))
                if (record.Fulfilled.Add(car.id))
                {
                    record.Remaining = Math.Max(0, record.Remaining - 1);
                    int actualFee = NpcRandomTrafficPolicy.OrderingFee(car.GetLoadInfo(0)?.Quantity ?? 0, record.UnitValue, record.FeePercent);
                    int adjustment = record.EstimatedFeePerCar - actualFee;
                    if (adjustment != 0)
                    {
                        var industry = OpsController.Shared.Areas.SelectMany(a => a.Industries)
                            .FirstOrDefault(i => i.Components.Any(c => c.Identifier == record.Destination));
                        industry?.ApplyToBalance(adjustment, Ledger.Category.Freight, "Pulpwood ordering fee capacity adjustment", 0, quiet: true);
                    }
                }
            if (record.Remaining == 0) { NpcServiceStore.State.PulpwoodOrders.Remove(record); continue; }
            var unloader = OpsController.Shared.Areas.SelectMany(a => a.Industries).SelectMany(i => i.Components)
                .OfType<IndustryUnloader>().FirstOrDefault(c => c.Identifier == record.Destination);
            if (unloader == null || unloader.ProgressionDisabled) continue;
            int pending = interchange.Orders.OfType<Order>().Where(o => o.Tag == record.Tag).Sum(o => o.CarCount);
            pending = Math.Max(pending, NpcServiceStore.State.HeldDeliveries.Count(h => h.Tag == record.Tag && h.CarId.Length == 0));
            if (pending < record.Remaining)
                interchange.AddOrder(new Order(unloader.carTypeFilter, unloader.load, unloader, record.Remaining - pending, record.Tag, false));
        }
        NpcServiceStore.Save();
    }
}
