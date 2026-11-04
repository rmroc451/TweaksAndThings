using Game.State;
using HarmonyLib;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(StateManager), "OnPropertiesDidRestore")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulationSpeedRestore_Patch
{
    private static void Postfix() { NpcSimulationSpeed.Reset(); NpcTrafficRouting.Reset(); }
}
