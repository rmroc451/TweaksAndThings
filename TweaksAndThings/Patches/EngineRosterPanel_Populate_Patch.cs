using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UI;
using UI.EngineRoster;

namespace RMROC451.TweaksAndThings.Patches;


[HarmonyPatch(typeof(EngineRosterPanel))]
[HarmonyPatch(nameof(EngineRosterPanel.Populate))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class EngineRosterPanel_Populate_Patch
{
    private static bool Prefix(EngineRosterPanel __instance, ref List<RosterRowData> rows)
    {
        try
        {
            TweaksAndThingsPlugin? tweaksAndThings = TweaksAndThingsPlugin.Instance;

            if (__instance._window != null && __instance._window.Title != null)
                __instance._window.Title = __instance._window.Title.Split(':')[0].Trim();
            if (!tweaksAndThings.IsEnabled())
            {
                return true;
            }

            var hiddenEntries = PerformancePolicies.SelectIds(
                rows,
                r => r.Engine != null && r.Engine.locomotiveControl != null && r.Engine.locomotiveControl.air != null &&
                    r.Engine.locomotiveControl.air.IsCutOut && !r.IsSelected && !r.IsFavorite,
                r => r.Engine.id);

            if (hiddenEntries.Count > 0 && __instance._window != null)
                __instance._window.Title = string.Format("{0} : {1}", __instance._window.Title, $"Hidden MU Count [{hiddenEntries.Count}]");

            rows = rows.Where(r => r.Engine == null || !hiddenEntries.Contains(r.Engine.id)).ToList();
        }
        catch (System.Exception ex)
        {
            ModLog.Error(ex, "Customizing engine roster rows failed; retaining the game's original rows");
            ModDiagnosticLog.Write("ENGINE ROSTER", "Panel population customization failed; retaining original rows.\n" + ex);
        }

        return true;
    }
}
