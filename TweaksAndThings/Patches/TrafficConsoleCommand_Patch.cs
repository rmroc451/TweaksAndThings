using HarmonyLib;
using UI.Console;

namespace RMROC451.TweaksAndThings.Patches;

// UMM does not perform Railloader's attribute-based command registration.
[HarmonyPatch(typeof(CommandProcessor), nameof(CommandProcessor.ProcessCommand))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class TrafficConsoleCommand_Patch
{
    private static bool Prefix(string[] parts, ref string output, ref bool __result)
    {
        if (parts.Length > 0 && string.Equals(parts[0], "/npcPickups", System.StringComparison.OrdinalIgnoreCase))
        {
            NpcTrafficWindow.Show("delinquent");
            output = "Opened delinquent pickup list.";
            __result = true;
            return false;
        }
        if (parts.Length == 0 || !string.Equals(parts[0], "/npcTraffic", System.StringComparison.OrdinalIgnoreCase)) return true;
        output = NpcTrafficDiagnostics.Snapshot();
        TweaksAndThingsPlugin.LogDiagnostic(output);
        __result = true;
        return false;
    }
}
