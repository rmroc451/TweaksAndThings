using Core;
using Game;
using Game.State;
using HarmonyLib;
using Model.Ops;
using Network;
using Railloader;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(InterchangedIndustryLoader))]
[HarmonyPatch(nameof(InterchangedIndustryLoader.ServeInterchange), typeof(IIndustryContext), typeof(Interchange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class InterchangedIndustryLoader_ServiceInterchange_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<InterchangedIndustryLoader_ServiceInterchange_Patch>();
    /// <summary>
    /// Allow Interchange service for buying parts/fuel when negative balance, adding an additional 20% penalty
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="ctx"></param>
    /// <param name="interchange"></param>
    /// <returns></returns>
    public static bool Prefix(InterchangedIndustryLoader __instance, IIndustryContext ctx, Interchange interchange)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!StateManager.IsHost || !tweaksAndThings.IsEnabled() || !tweaksAndThings.ServiceFundPenalties()) return true;

        StateManager shared = StateManager.Shared;
        List<IOpsCar> list = (from car in EnumerateCars(__instance, ctx, requireWaybill: true)
                              where car.IsEmptyOrContains(__instance.load)
                              select car).ToList();
        int num2 = 0;
        int num3 = 0;
        int penalty = 0;
        GameDateTime returnTime = ctx.Now.AddingDays(23f / 24f);
        foreach (IOpsCar item3 in list)
        {
            (float quantity, float capacity) tuple = item3.QuantityOfLoad(__instance.load);
            float item = tuple.quantity;
            float item2 = tuple.capacity;
            int num4 = Mathf.RoundToInt((item2 - item) * __instance.load.costPerUnit);
            if (num4 > 0)
            {
                var canAfford = shared.CanAfford(num4);
                penalty = Mathf.CeilToInt(!canAfford ? num4 * 0.2f : 0);
                __instance.Industry.ApplyToBalance(-num4, __instance.ledgerCategory, null, 0, quiet: true);

                num2 += num4;
                num3++;
            }
            item3.Load(__instance.load, item2);
            item3.SetWaybill(null, __instance, "Full");
            ctx.MoveToBardo(item3);
            __instance.ScheduleReturnFromBardo(item3, returnTime);
        }
        if (num2 > 0)
        {
            __instance.Industry.ApplyToBalance(-num2, __instance.ledgerCategory, null, num3, quiet: true);
            string penaltyText = penalty > 0 ? $"; Overdraft fee of {penalty:C0}" : string.Empty;
            Multiplayer.Broadcast(string.Format("{6}: Ordered {0} of {1} at {2} for {3:C0}{5}. Expected return: {4}.", num3.Pluralize("car"), __instance.load.description, __instance.HyperlinkToThis, num2, 1.Pluralize("day"), penaltyText, __instance.HyperlinkToThis));
            if (penalty > 0)
                StateManager.Shared.ApplyToBalance(-penalty, __instance.ledgerCategory, null, $"Overdraft: {__instance.Industry.name}", 0, quiet: true);
        }

        return false;
    }

    private static IEnumerable<IOpsCar> EnumerateCars(InterchangedIndustryLoader __instance, IIndustryContext ctx, bool requireWaybill = false)
    {
        foreach (IOpsCar item in ctx.CarsAtPosition())
        {
            if (!CarTypeMatches(__instance.Interchange, item)) continue;

            if (requireWaybill)
            {
                try
                {
                    Waybill? waybill = item.Waybill;
                    string destId = waybill?.Destination.Identifier ?? string.Empty;
                    if (!waybill.HasValue || !destId.Equals(__instance.Identifier)) continue;

                } catch (Exception ex)
                {
                    _log.Error(ex, $"{item.DisplayName} issue detecting waybill");
                    continue;
                }
            }
            yield return item;
        }
    }

    private static bool CarTypeMatches(Interchange interchange, IOpsCar car)
    {
        string carType = car.CarType;
        return interchange.carTypeFilter.Matches(carType);
    }

}
