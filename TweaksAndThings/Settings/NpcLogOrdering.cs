using System;
using System.Linq;
using Game;
using Game.State;
using Model.Ops;
using UI.Builder;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcLogOrdering
{
    internal const string TagPrefix = "TAT-LOG-";
    private static float nextCheck;
    internal static bool Eligible(IndustryUnloader unloader) => unloader.load != null &&
        (unloader.Industry.identifier + " " + unloader.Industry.name).IndexOf("whittier", StringComparison.OrdinalIgnoreCase) >= 0 &&
        unloader.load.id.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0;

    internal static void Build(Industry industry, UIPanelBuilder builder)
    {
        if (!industry.Components.OfType<IndustryUnloader>().Any(Eligible) || !NpcServiceStore.Load()) return;
        builder.AddSection("Interchange logs", section =>
        {
            section.AddFieldToggle("Order logs from interchange", () => NpcServiceStore.State.LogImportIndustries.Contains(industry.identifier), value =>
            {
                if (!StateManager.IsHost || !NpcServiceStore.Load()) return;
                if (value) NpcServiceStore.State.LogImportIndustries.Add(industry.identifier);
                else NpcServiceStore.State.LogImportIndustries.Remove(industry.identifier);
                NpcServiceStore.Save(); nextCheck = 0;
            }).Disable(!StateManager.IsHost);
            section.AddLabel("Automatically covers unmet logs demand. Pending and held deliveries count toward demand. 50% of each imported log car's normal delivery payout is deducted at the end of its delivery day; no charge at ordering or interchange service. Disabling stops new orders, not existing ones.");
            section.AddField("Pending end-of-day deduction", () => NpcServiceStore.State.LogImportFees
                .Where(f => !f.Settled && f.Industry == industry.identifier).Sum(f => f.Amount).ToString("C0"), UIPanelBuilder.Frequency.Periodic);
        });
    }

    internal static void Tick()
    {
        if (!StateManager.IsHost || !NpcIndustryPlanning.Ready(TimeWeather.Now.TotalSeconds) ||
            Time.realtimeSinceStartup < nextCheck || !NpcServiceStore.Load()) return;
        nextCheck = Time.realtimeSinceStartup + 30;
        foreach (var unloader in OpsController.Shared.AllIndustries
            .Where(i => NpcServiceStore.State.LogImportIndustries.Contains(i.identifier) && !i.ProgressionDisabled)
            .SelectMany(i => i.Components).OfType<IndustryUnloader>().Where(c => !c.ProgressionDisabled && Eligible(c)))
        {
            var ctx = unloader.CreateContext(TimeWeather.Now, 0);
            var load = unloader.load;
            if (load.NominalQuantityPerCarLoad <= 0) continue;
            double unmet = unloader.maxStorage * unloader.Industry.GetContractMultiplier() - ctx.QuantityInStorage(load) - ctx.QuantityOnOrder(load);
            int cars = Math.Min(50, Math.Max(0, (int)Math.Ceiling(unmet / load.NominalQuantityPerCarLoad)));
            if (cars == 0) continue;
            var interchange = OpsController.Shared.InterchangeForPosition(unloader, null);
            if (interchange == null || interchange.Disabled || interchange.ProgressionDisabled) continue;
            var record = new NpcPulpwoodOrder { Tag = TagPrefix + Guid.NewGuid().ToString("N"),
                Destination = unloader.Identifier, Interchange = interchange.Identifier, Remaining = cars, DeferredLogFee = true };
            interchange.AddOrder(new Order(unloader.carTypeFilter, load, unloader, cars, record.Tag, false));
            NpcServiceStore.State.PulpwoodOrders.Add(record);
            ModDiagnosticLog.Write("LOG IMPORT", $"Ordered {cars} log cars for {unloader.Industry.name}; payment deduction deferred until delivery-day settlement.");
        }
        NpcServiceStore.Save();
    }

    internal static void Delivered(IndustryContext context, IOpsCar car, Waybill waybill)
    {
        if (!StateManager.IsHost || waybill.Tag?.StartsWith(TagPrefix, StringComparison.Ordinal) != true || !NpcServiceStore.Load()) return;
        string key = LogImportPolicy.BillKey(car.Id, waybill.Created.TotalSeconds, waybill.Destination.Identifier);
        if (NpcServiceStore.State.LogImportFees.Any(f => f.BillKey == key)) return;
        NpcServiceStore.State.LogImportFees.Add(new NpcLogImportFee { BillKey = key,
            Industry = context._industry.identifier, DeliveryDay = (int)Math.Floor(context.Now.TotalSeconds / 86400),
            Amount = LogImportPolicy.Fee(waybill.PaymentOnArrival) });
        NpcServiceStore.Save();
    }

    internal static void Settle()
    {
        if (!StateManager.IsHost || !NpcServiceStore.Load()) return;
        int day = (int)Math.Floor(TimeWeather.Now.TotalSeconds / 86400);
        foreach (var group in NpcServiceStore.State.LogImportFees.Where(f => LogImportPolicy.Due(f.DeliveryDay, day, f.Settled)).GroupBy(f => f.Industry))
        {
            int amount = group.Sum(f => f.Amount);
            if (amount > 0) StateManager.Shared.ApplyToBalance(-amount, Ledger.Category.Freight, null,
                "Whittier sawmill imported logs — end-of-day deduction", group.Count(), quiet: true);
            foreach (var fee in group) fee.Settled = true;
            ModDiagnosticLog.Write("LOG IMPORT", $"Settled {group.Count()} imported log deliveries for {group.Key}; deduction={amount:C0}.");
        }
        NpcServiceStore.Save();
    }
}
