using HarmonyLib;
using Model;
using Model.Ops;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(PassengerStop), nameof(PassengerStop.LoadCar))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class PassengerStop_LoadCar_ThroughTraffic_Patch
{
    private static bool Prefix(Car car, ref bool __result)
    {
        if (!ThroughTrafficGuard.IsRestricted(car)) return true;
        __result = false;
        return false;
    }
}
