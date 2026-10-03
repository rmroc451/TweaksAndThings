using System;
using System.Linq;
using Game.State;
using HarmonyLib;
using Model.Ops;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(IndustryContext), nameof(IndustryContext.PayWaybill))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcPickupPayment_Patch
{
    private static bool Prefix(IndustryContext __instance, IOpsCar car, Waybill waybill, out int __state)
    {
        __state = 0;
        if (!StateManager.IsHost || !NpcServiceStore.Load() ||
            !OpsController.Shared.AllInterchanges.Any(i => i.Identifier == waybill.Destination.Identifier)) return true;
        if (NpcPickupTracking.PaymentForfeited(car.Id, waybill)) return false;
        int bonus = __instance._industry.HasActiveContract(__instance.Now)
            ? __instance._industry.Contract.Value.TimelyDeliveryBonus(Mathf.FloorToInt(__instance.Now.DaysSince(waybill.Created)), waybill.PaymentOnArrival) : 0;
        __state = Math.Max(0, waybill.PaymentOnArrival + bonus - waybill.ConditionFineForCarCondition(car.Condition));
        return true;
    }

    private static void Postfix(IOpsCar car, Waybill waybill, int __state)
    {
        if (__state <= 0) return;
        NpcServiceStore.State.PickupPayments[car.Id] = new NpcPickupPayment
        { WaybillCreated = waybill.Created.TotalSeconds, InterchangeId = waybill.Destination.Identifier, Amount = __state };
        NpcServiceStore.Save();
    }
}
