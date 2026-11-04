using System;
using HarmonyLib;
using RMROC451.TweaksAndThings;
using UI.Console;

namespace RMROC451.TweaksAndThings.Patches;

// The game DLL loader does not perform Relay's attribute-based command registration.
[HarmonyPatch(typeof(CommandProcessor), nameof(CommandProcessor.ProcessCommand))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class TrafficConsoleCommand_Patch
{
    private static bool Prefix(string[] parts, ref string output, ref bool __result)
    {
        if (parts.Length > 0 && string.Equals(parts[0], "/npcPickups", System.StringComparison.OrdinalIgnoreCase))
        {
            if (!TweaksAndThingsPlugin.Instance.NpcInterchangeTrafficEnabled()) { output = "NPC interchange traffic is disabled in the F10 settings."; __result = true; return false; }
            NpcTrafficWindow.Show("delinquent");
            output = "Opened delinquent pickup list.";
            __result = true;
            return false;
        }
        if (parts.Length > 0 && string.Equals(parts[0], "/dmvisuals", System.StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 2)
                output = "Usage: /dmvisuals force|normal|status";
            else if (string.Equals(parts[1], "force", System.StringComparison.OrdinalIgnoreCase))
                output = DoubleMainlineRuntime.SetRendererVisibilityOverride(true);
            else if (string.Equals(parts[1], "normal", System.StringComparison.OrdinalIgnoreCase))
                output = DoubleMainlineRuntime.SetRendererVisibilityOverride(false);
            else if (string.Equals(parts[1], "status", System.StringComparison.OrdinalIgnoreCase))
                output = $"Forced second-mainline visibility is {(DoubleMainlineSettings.ForceGeneratedTrackVisible ? "on" : "off")}.";
            else
                output = "Usage: /dmvisuals force|normal|status";
            __result = true;
            return false;
        }
        if (parts.Length > 0 && string.Equals(parts[0], "/dmgen", System.StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 2)
                output = "Usage: /dmgen pause|step <group>|next|status|resume";
            else if (string.Equals(parts[1], "pause", System.StringComparison.OrdinalIgnoreCase))
                output = DoubleMainlineRuntime.PauseGeneration();
            else if (string.Equals(parts[1], "step", System.StringComparison.OrdinalIgnoreCase))
                output = parts.Length >= 3
                    ? DoubleMainlineRuntime.BeginSegmentStepping(parts[2])
                    : "Usage: /dmgen step <group>";
            else if (string.Equals(parts[1], "next", StringComparison.OrdinalIgnoreCase))
                output = DoubleMainlineRuntime.StepNextGeneratedSegment();
            else if (string.Equals(parts[1], "status", StringComparison.OrdinalIgnoreCase))
                output = DoubleMainlineRuntime.SegmentSteppingStatus();
            else if (string.Equals(parts[1], "resume", StringComparison.OrdinalIgnoreCase))
                output = DoubleMainlineRuntime.ResumeGeneration();
            else
                output = "Usage: /dmgen pause|step <group>|next|status|resume";
            __result = true;
            return false;
        }
        if (parts.Length == 0 || !string.Equals(parts[0], "/npcTraffic", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (!TweaksAndThingsPlugin.Instance.NpcInterchangeTrafficEnabled()) { output = "NPC interchange traffic is disabled in the F10 settings."; __result = true; return false; }
        output = NpcTrafficDiagnostics.Snapshot();
        TweaksAndThingsPlugin.LogDiagnostic(output);
        __result = true;
        return false;
    }
}
