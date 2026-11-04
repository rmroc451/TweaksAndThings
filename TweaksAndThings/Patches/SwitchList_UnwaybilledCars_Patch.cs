using System;
using HarmonyLib;
using Model;
using Model.Ops;
using UI.SwitchList;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(OpsCarList), "MakeEntry")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SwitchList_UnwaybilledCars_Patch
{
    private static bool Prefix(string carId, TrainController trainController, ref OpsCarList.Entry? __result)
    {
        var car = trainController.CarForId(carId);
        var ops = OpsController.Shared;
        if (car == null || ops == null) return true;
        bool repair = car.TryGetOverrideDestination(OverrideDestination.Repair, ops, out var repairDestination) && repairDestination.HasValue;
        if (!repair && car.GetWaybill(ops).HasValue) return true;
        var position = car.GetCenterPosition(Track.Graph.Shared);
        var location = ops.PositionForCar(car);
        var current = location.HasValue
            ? new OpsCarList.Entry.Location(location.Value.DisplayName, ops.ClosestArea(car)?.name, position, (int)position.x / 10, location.Value.Spans)
            : new OpsCarList.Entry.Location(ops.ClosestArea(car)?.name ?? "Unassigned track", null, position, (int)position.x / 10, Array.Empty<string>());
        var destination = new OpsCarList.Entry.Location("No waybill", "Unassigned", position, 0, Array.Empty<string>());
        if (repair)
        {
            var repairPosition = repairDestination!.Value.Item1;
            string kind = repairDestination.Value.Item2 == "overhaul" ? "Overhaul" : "Repair";
            destination = new OpsCarList.Entry.Location(kind + ": " + repairPosition.DisplayName,
                ops.AreaForCarPosition(repairPosition)?.name, repairPosition.GetCenter(), 0, repairPosition.Spans);
        }
        __result = new OpsCarList.Entry(car.id, car.SortName, current, destination, false);
        return false;
    }
}
