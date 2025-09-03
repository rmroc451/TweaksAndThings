using HarmonyLib;
using Railloader;
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
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;

        __instance._window.Title = __instance._window.Title.Split(':')[0].Trim();
        if (!tweaksAndThings.IsEnabled()) return true;

        var hiddenEntries = rows.Where(r => r.Engine.locomotiveControl.air.IsCutOut && !r.IsSelected && !r.IsFavorite).Select(r => r.Engine.id) ?? Enumerable.Empty<string>();

        if (hiddenEntries.Any()) __instance._window.Title =string.Format("{0} : {1}", __instance._window.Title, $"Hidden MU Count [{hiddenEntries.Count()}]");

        rows = rows.Where(r => !hiddenEntries.Contains(r.Engine.id)).ToList();

        return true;
    }
}
