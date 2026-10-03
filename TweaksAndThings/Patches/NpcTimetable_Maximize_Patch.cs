using HarmonyLib;
using TMPro;
using UI.Common;
using UI.Timetable;
using UnityEngine;
using UnityEngine.UI;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(TimetableWindow), "Awake")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_Maximize_Patch
{
    private static void Postfix(TimetableWindow __instance)
    {
        var window = __instance.GetComponent<Window>();
        var buttonObject = new GameObject("Maximize timetable", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(window.titleLabel.transform.parent, false);
        buttonObject.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.sizeDelta = new Vector2(74, 24);
        rect.anchoredPosition = new Vector2(-38, 0);
        buttonObject.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1);
        var title = window.titleLabel.rectTransform;
        title.offsetMax = new Vector2(title.offsetMax.x - 80, title.offsetMax.y);
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(rect, false);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = window.titleLabel.font;
        label.fontSize = 12;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = "Maximize";
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        bool maximized = false;
        Vector2 priorSize = default, priorPosition = default;
        buttonObject.GetComponent<Button>().onClick.AddListener(() =>
        {
            var windowRect = window.GetComponent<RectTransform>();
            if (!maximized)
            {
                priorSize = window.GetContentSize();
                priorPosition = windowRect.anchoredPosition;
                var parent = (RectTransform)windowRect.parent;
                Vector2 trim = windowRect.rect.size - priorSize;
                window.SetContentSize(parent.rect.size - trim - new Vector2(16, 16));
                window.SetPosition(Window.Position.Center);
            }
            else { window.SetContentSize(priorSize); window.SetPositionRestoring(priorPosition); }
            maximized = !maximized;
            label.text = maximized ? "Restore" : "Maximize";
        });
    }
}
