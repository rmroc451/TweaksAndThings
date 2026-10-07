using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

internal static class SimulationCameraTime
{
    internal static float NormalInputTime(float delta) => delta / NpcSimulationSpeed.InputTimingScale;
    internal static IEnumerable<CodeInstruction> Adjust(IEnumerable<CodeInstruction> instructions)
    {
        var frame = AccessTools.PropertyGetter(typeof(Time), nameof(Time.deltaTime));
        var physics = AccessTools.PropertyGetter(typeof(Time), nameof(Time.fixedDeltaTime));
        var normalize = AccessTools.Method(typeof(SimulationCameraTime), nameof(NormalInputTime));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(frame) || instruction.Calls(physics))
                yield return new CodeInstruction(OpCodes.Call, normalize);
        }
    }
}

[HarmonyPatch(typeof(Cameras.StrategyCameraController), "UpdateCameraPosition")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulationSpeedRestore_StrategyCamera_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SimulationCameraTime.Adjust(instructions);
}

[HarmonyPatch(typeof(Cameras.StrategyCameraController), "MoveAboveGround")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulationSpeedRestore_CameraGround_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SimulationCameraTime.Adjust(instructions);
}

[HarmonyPatch(typeof(Cameras.StrategyCameraController), "SnapToGround")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulationSpeedRestore_CameraSnap_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SimulationCameraTime.Adjust(instructions);
}

[HarmonyPatch(typeof(Cameras.MouseLookInput), "UpdateInput")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulationSpeedRestore_MouseLook_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SimulationCameraTime.Adjust(instructions);
}

[HarmonyPatch(typeof(Cameras.StationaryCameraController), "LateUpdate")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulationSpeedRestore_StationaryCamera_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SimulationCameraTime.Adjust(instructions);
}

[HarmonyPatch(typeof(Character.CharacterCameraController), "UpdateZoom")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulationSpeedRestore_CharacterZoom_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SimulationCameraTime.Adjust(instructions);
}

[HarmonyPatch(typeof(UI.GameInput), "Update")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SimulationSpeedRestore_InputCounters_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => SimulationCameraTime.Adjust(instructions);
}
