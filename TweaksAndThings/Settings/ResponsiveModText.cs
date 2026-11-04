using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RMROC451.TweaksAndThings;

internal sealed class ResponsiveModText : MonoBehaviour
{
    private readonly HashSet<int> configured = new();
    private readonly List<TMP_Text> texts = new();
    private float next;
    internal static void Install(RectTransform root)
    { if (root.GetComponent<ResponsiveModText>() == null) root.gameObject.AddComponent<ResponsiveModText>(); }
    private void OnEnable() => next = 0;
    private void OnRectTransformDimensionsChange() => next = 0;
    private void LateUpdate()
    {
        if (Time.realtimeSinceStartup < next) return;
        next = Time.realtimeSinceStartup + 0.5f;
        texts.Clear(); GetComponentsInChildren(true, texts);
        if (configured.Count > 4096) configured.Clear();
        foreach (var text in texts)
        {
            if (!configured.Add(text.GetInstanceID()) || text.GetComponentInParent<TMP_InputField>() != null) continue;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            if (text.GetComponentInParent<Button>() != null || text.GetComponentInParent<Dropdown>() != null ||
                text.GetComponentInParent<TMP_Dropdown>() != null)
            { text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = text.fontSize; continue; }
            var element = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 0; element.flexibleWidth = 1; element.preferredHeight = -1;
            var row = text.transform.parent;
            if (row != null && row.GetComponent<HorizontalLayoutGroup>() != null)
            {
                row.GetComponent<HorizontalLayoutGroup>().childControlHeight = true;
                var rowElement = row.GetComponent<LayoutElement>();
                if (rowElement != null) rowElement.preferredHeight = -1;
            }
        }
    }
}
