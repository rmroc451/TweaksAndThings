using HarmonyLib;
using Model;
using Game.State;
using System.Linq;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(BaseLocomotive), "get_TractiveEffort")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcPoolPower_Traction_Patch
{
    private static void Postfix(BaseLocomotive __instance, ref float __result)
    {
        if (!StateManager.IsHost) return;
        // Runs in native physics for both manual and AE-controlled locomotives.
        // Pool engines themselves never supply player-controlled traction.
        if (NpcPoolPower.IsPooled(__instance))
        {
            if (!__instance.EnumerateCoupled().Any(c => c.IsLocomotive && !NpcPoolPower.IsPooled(c))) NpcPoolPower.Hold(__instance, 0);
            __result = 0;
            return;
        }
        if (NpcPoolPower.Hold(__instance, __result)) __result = 0;
    }
}
