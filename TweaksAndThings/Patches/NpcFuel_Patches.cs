using HarmonyLib;
using Model;
using Model.Physics;
using Model.Ops;
using Game.State;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(SteamEngine), "get_CoalConsumptionRate")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcFuel_Coal_Patch
{
    private static bool Prefix(SteamEngine __instance, ref float __result)
    { if (!ThroughTrafficGuard.IsGenerated(__instance.GetComponent<SteamLocomotive>())) return true; __result = 0; return false; }
}

[HarmonyPatch(typeof(SteamEngine), "get_WaterConsumptionRate")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcFuel_Water_Patch
{
    private static bool Prefix(SteamEngine __instance, ref float __result)
    { if (!ThroughTrafficGuard.IsGenerated(__instance.GetComponent<SteamLocomotive>())) return true; __result = 0; return false; }
}

[HarmonyPatch(typeof(PrimeMover), "get_FuelConsumptionRate")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcFuel_Diesel_Patch
{
    private static bool Prefix(PrimeMover __instance, ref float __result)
    { if (!ThroughTrafficGuard.IsGenerated(__instance.GetComponent<DieselLocomotive>())) return true; __result = 0; return false; }
}

internal static class NpcFuelRecovery
{
    internal static void FillEmpty(Car? car)
    {
        if (!StateManager.IsHost || car == null) return;
        for (int index = 0; index < car.Definition.LoadSlots.Count; index++)
        {
            var slot = car.Definition.LoadSlots[index];
            if ((slot.RequiredLoadIdentifier != "coal" && slot.RequiredLoadIdentifier != "water" && slot.RequiredLoadIdentifier != "diesel-fuel") ||
                (car.GetLoadInfo(index)?.Quantity ?? 0) > 0.001f) continue;
            int slotIndex = index;
            NpcTrainOperations.Trusted(() => car.SetLoadInfo(slotIndex, new CarLoadInfo(slot.RequiredLoadIdentifier, slot.MaximumCapacity)));
        }
    }
}

[HarmonyPatch(typeof(SteamLocomotive), "PeriodicUpdate")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcFuel_SteamRecovery_Patch
{
    private static void Prefix(SteamLocomotive __instance)
    { if (ThroughTrafficGuard.IsGenerated(__instance)) NpcFuelRecovery.FillEmpty(__instance.FuelCar()); }
}

[HarmonyPatch(typeof(DieselLocomotive), "PeriodicUpdate")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcFuel_DieselRecovery_Patch
{
    private static void Prefix(DieselLocomotive __instance)
    { if (ThroughTrafficGuard.IsGenerated(__instance)) NpcFuelRecovery.FillEmpty(__instance); }
}
