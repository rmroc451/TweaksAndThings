using Core;
using Game;
using Game.State;
using HarmonyLib;
using Model.Ops;
using Network;
using RMROC451.TweaksAndThings.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(InterchangedIndustryLoader))]
[HarmonyPatch(nameof(InterchangedIndustryLoader.ServeInterchange), typeof(IIndustryContext), typeof(Interchange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class InterchangedIndustryLoader_ServeInterchange_Patch
{
    /// <summary>
    /// Preserves the mod's servicing overdraft behavior for interchange orders.
    /// </summary>
    internal static bool Prefix(InterchangedIndustryLoader __instance, IIndustryContext ctx, Interchange interchange)
    {
        var plugin = TweaksAndThingsPlugin.Instance;
        if (!StateManager.IsHost || !plugin.IsEnabled() || !plugin.ServiceFundPenalties()) return true;

        var cars = EnumerateCars(__instance, ctx, interchange, requireWaybill: true)
            .Where(car => car.IsEmptyOrContains(__instance.load))
            .ToList();
        if (cars.Count == 0) return false;

        var orders = cars.Select(car =>
        {
            var load = car.QuantityOfLoad(__instance.load);
            int cost = Mathf.RoundToInt((load.capacity - load.quantity) * __instance.load.costPerUnit);
            return (Car: car, Capacity: load.capacity, Cost: cost);
        }).ToList();

        int totalCost = orders.Sum(order => order.Cost);
        int penalty = FeaturePolicies.CalculateServicingOverdraftFee(totalCost, StateManager.Shared.CanAfford(totalCost));
        GameDateTime returnTime = ctx.Now.AddingDays(23f / 24f);

        foreach (var order in orders)
        {
            order.Car.Load(__instance.load, order.Capacity);
            order.Car.SetWaybill(null, __instance, "Full");
            ctx.MoveToBardo(order.Car);
            __instance.ScheduleReturnFromBardo(order.Car, returnTime);
        }

        if (totalCost > 0)
        {
            __instance.Industry.ApplyToBalance(-totalCost, __instance.ledgerCategory, null, cars.Count, quiet: true);
            if (penalty > 0)
            {
                StateManager.Shared.ApplyToBalance(-penalty, __instance.ledgerCategory, null,
                    $"Overdraft: {__instance.Industry.name}", 0, quiet: true);
            }

            string penaltyText = penalty > 0 ? $"; Overdraft fee of {penalty:C0}" : string.Empty;
            Multiplayer.Broadcast(
                $"{__instance.HyperlinkToThis}: Ordered {cars.Count.Pluralize("car")} of {__instance.load.description} " +
                $"at {__instance.HyperlinkToThis} for {totalCost:C0}{penaltyText}. Expected return: 1 day.");
        }

        return false;
    }

    private static IEnumerable<IOpsCar> EnumerateCars(
        InterchangedIndustryLoader loader,
        IIndustryContext context,
        Interchange interchange,
        bool requireWaybill)
    {
        foreach (IOpsCar car in context.CarsAtPosition())
        {
            if (!interchange.carTypeFilter.Matches(car.CarType)) continue;

            if (requireWaybill)
            {
                try
                {
                    Waybill? waybill = car.Waybill;
                    string destinationId = waybill?.Destination.Identifier ?? string.Empty;
                    if (!waybill.HasValue || !destinationId.Equals(loader.Identifier, StringComparison.Ordinal)) continue;
                }
                catch (Exception exception)
                {
                    Log.ForContext<InterchangedIndustryLoader_ServeInterchange_Patch>()
                        .Warning(exception, "{Car} issue detecting interchange service waybill", car.DisplayName);
                    continue;
                }
            }

            yield return car;
        }
    }
}
