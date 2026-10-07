using TMPro;
using UI.EngineRoster;
using UnityEngine;
using UnityEngine.UI;

namespace RMROC451.TweaksAndThings;

internal static class EngineRosterColumns
{
    internal static void Apply(EngineRosterRow row)
    {
        var settings = TweaksAndThingsPlugin.Instance?.settings;
        if (settings == null || TweaksAndThingsPlugin.Instance?.IsEnabled != true) return;
        Set(row.nameLabel, settings.EngineRosterNameWidth);
        Set(row.crewLabel, settings.EngineRosterCrewWidth);
        Set(row.infoLabel, settings.EngineRosterStatusWidth);
    }
    private static void Set(TMP_Text text, int width)
    {
        var cell = text.transform;
        while (cell.parent != null && cell.parent.GetComponent<HorizontalLayoutGroup>() == null &&
            cell.parent.GetComponent<EngineRosterRow>() == null) cell = cell.parent;
        var element = cell.GetComponent<LayoutElement>() ?? cell.gameObject.AddComponent<LayoutElement>();
        element.minWidth = 40;
        element.preferredWidth = Mathf.Clamp(width, 60, 600);
        element.flexibleWidth = 0;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }
}
