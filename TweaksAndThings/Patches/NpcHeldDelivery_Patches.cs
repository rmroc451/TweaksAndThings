using System.Linq;
using Game;
using Game.State;
using HarmonyLib;
using Model.Ops;
using UI.Builder;
using UI.CompanyWindow;
using UI.CarInspector;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(Interchange), nameof(Interchange.PrepareToService))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcHeldDelivery_Restore_Patch
{
    private static void Postfix(Interchange __instance)
    {
        if (!SimulatedInterchangeService.Owns(__instance) || SimulatedInterchangeService.Loading != null)
            NpcHeldDeliveryTracking.Restore(__instance);
        NpcPulpwoodOrdering.Restore(__instance);
    }
}

[HarmonyPatch(typeof(Interchange), nameof(Interchange.ServeInterchange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcHeldDelivery_PulpwoodFulfilled_Patch
{
    private static void Postfix(Interchange __instance) => NpcPulpwoodOrdering.Restore(__instance);
}

[HarmonyPatch(typeof(LocationsPanelBuilder.IndustryDetailBuilder), nameof(LocationsPanelBuilder.IndustryDetailBuilder.AddOrdersSection))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcHeldDelivery_IndustryPanel_Patch
{
    private static void Postfix(LocationsPanelBuilder.IndustryDetailBuilder __instance, UIPanelBuilder builder)
    {
        builder.RebuildOnInterval(5);
        NpcHeldDeliveryTracking.BuildList(builder, __instance._industry.identifier);
        NpcPulpwoodOrdering.Build(__instance._industry, builder);
    }
}

[HarmonyPatch(typeof(IndustryContext), nameof(IndustryContext.PayWaybill))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcHeldDelivery_Premium_Patch
{
    private static void Postfix(IndustryContext __instance, IOpsCar car, Waybill waybill)
    {
        if (!StateManager.IsHost || !NpcServiceStore.Load()) return;
        var held = NpcServiceStore.State.HeldDeliveries.FirstOrDefault(h => h.CarId == car.Id &&
            h.WaybillCreated == waybill.Created.TotalSeconds && h.DestinationId == waybill.Destination.Identifier);
        if (held == null) return;
        int premium = NpcHeldDeliveryPolicy.Premium(waybill.PaymentOnArrival, held.PremiumPercent, __instance.Now.TotalSeconds, held.BonusStarts, held.Deadline);
        if (held.NoPayment) premium = 0;
        if (premium > 0)
        {
            __instance._industry.ApplyToBalance(premium, Ledger.Category.Freight, "Held interchange delivery premium", 1, quiet: true);
            Network.Multiplayer.Broadcast($"{car.DisplayName}: received ${premium} held-delivery premium for arrival before {new GameDateTime((float)held.Deadline)}.");
        }
        ModDiagnosticLog.Write("PREMIUM", $"game={__instance.Now}; car={car.Id}; destination={held.DestinationId}; base={waybill.PaymentOnArrival}; extra={premium}; start={held.BonusStarts}; deadline={held.Deadline}");
        NpcServiceStore.State.HeldDeliveries.Remove(held);
        NpcServiceStore.Save();
    }
}

[HarmonyPatch(typeof(CarInspector), "PopulateWaybillPanel")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcHeldDelivery_CarPanel_Patch
{
    private static void Prefix(CarInspector __instance, UIPanelBuilder builder)
    {
        if (!NpcServiceStore.Load()) return;
        var held = NpcServiceStore.State.HeldDeliveries.FirstOrDefault(h => h.CarId == __instance._car.id);
        if (held != null) builder.AddField("Held delivery", () => NpcHeldDeliveryTracking.CarBonusSummary(__instance._car.id), UIPanelBuilder.Frequency.Periodic)
            .Tooltip("Delivery premium", "The extra bonus decreases with elapsed game time after the next interchange service. Deliver to the waybill destination before the deadline.");
    }
}
