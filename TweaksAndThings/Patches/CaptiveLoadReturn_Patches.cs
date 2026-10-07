using System;
using System.Linq;
using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Model;
using Model.Ops;

namespace RMROC451.TweaksAndThings.Patches;

internal static class CaptiveLoadSource
{
    internal const string Key = "RMROC451.TweaksAndThings.OrderSourceInterchange";
    internal static string Source(Waybill? bill)
    {
        if (bill?.Origin.HasValue != true) return "";
        string origin = bill.Value.Origin!.Value.Identifier;
        var interchange = OpsController.Shared.AllInterchanges.FirstOrDefault(i => i.Identifier == origin);
        if (interchange != null) return interchange.Identifier;
        // Captive loading orders can name the supplier component as the origin.
        var loader = OpsController.Shared.AllIndustries.SelectMany(i => i.Components).OfType<InterchangedIndustryLoader>()
            .FirstOrDefault(c => c.Identifier == origin);
        return loader?.Interchange?.Identifier ?? "";
    }
}

[HarmonyPatch(typeof(CarExtensions), nameof(CarExtensions.SetWaybill))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class CaptiveLoad_RecordSource_Patch
{
    private static void Postfix(Car car, Waybill? waybill)
    {
        if (!StateManager.IsHost || OpsController.Shared == null || !waybill.HasValue) return;
        string source = CaptiveLoadSource.Source(waybill);
        if (source.Length > 0 && !OpsController.Shared.AllInterchanges.Any(i => i.Identifier == waybill.Value.Destination.Identifier))
            car.KeyValueObject[CaptiveLoadSource.Key] = Value.String(source);
    }
}

// Captive company cars briefly lose their waybill while refilled in Bardo.
// Record the servicing interchange before either native or overdraft servicing.
[HarmonyPatch(typeof(InterchangedIndustryLoader), nameof(InterchangedIndustryLoader.ServeInterchange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class CaptiveLoad_ServiceSource_Patch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(InterchangedIndustryLoader __instance, IIndustryContext ctx, Interchange interchange)
    {
        if (!StateManager.IsHost) return;
        foreach (var car in ctx.CarsAtPosition().Where(c => c.Waybill?.Destination.Identifier == __instance.Identifier &&
            interchange.carTypeFilter.Matches(c.CarType) && c.IsEmptyOrContains(__instance.load)))
            OpsController.Shared.ModelCarForCar(car).KeyValueObject[CaptiveLoadSource.Key] = Value.String(interchange.Identifier);
    }
}

[HarmonyPatch(typeof(OpsController), "WaybillCarToInterchange")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class CaptiveLoad_ReturnSource_Patch
{
    private static void Prefix(OpsController __instance, IOpsCar car, out string __state)
    {
        __state = "";
        if (!StateManager.IsHost) return;
        var model = __instance.ModelCarForCar(car);
        __state = model.KeyValueObject[CaptiveLoadSource.Key].IsNull ? CaptiveLoadSource.Source(car.Waybill) :
            model.KeyValueObject[CaptiveLoadSource.Key].StringValue;
    }
    private static void Postfix(OpsController __instance, IOpsCar car, OpsCarPosition carPosition, bool noPayment, string __state)
    {
        if (!StateManager.IsHost || __state.Length == 0 || !car.Waybill.HasValue) return;
        var source = __instance.AllInterchanges.FirstOrDefault(i => i.Identifier == __state);
        if (source == null) { ModDiagnosticLog.Write("CAPTIVE RETURN", $"Source interchange {__state} is missing for {car.Id}; retaining native return waybill."); return; }
        var bill = car.Waybill.Value;
        OpsCarPosition destination = source;
        if (bill.Destination.Identifier == destination.Identifier) return;
        bill.Destination = destination;
        bill.PaymentOnArrival = noPayment ? 0 : __instance.PaymentForMove(carPosition, destination, car.WeightInTons);
        bill.GraceDays = __instance.CalculateGraceDays(carPosition, destination);
        __instance.ModelCarForCar(car).SetWaybill(bill);
        ModDiagnosticLog.Write("CAPTIVE RETURN", $"{car.Id}: unloading return routed to source interchange {source.Identifier}.");
    }
}
