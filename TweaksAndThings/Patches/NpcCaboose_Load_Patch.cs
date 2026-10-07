using HarmonyLib;
using Model;
using Model.Ops;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(Model.Ops.CarExtensions), nameof(Model.Ops.CarExtensions.SetLoadInfo))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcCaboose_Load_Patch
{
    private static bool Prefix(Car __0, CarLoadInfo? __2) =>
        !NpcServiceCaboose.IsServiceCar(__0) || !__2.HasValue || __2.Value.LoadId != "crew-hours" || __2.Value.Quantity <= 0;
}
