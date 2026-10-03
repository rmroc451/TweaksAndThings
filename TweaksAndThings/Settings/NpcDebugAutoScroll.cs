using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RMROC451.TweaksAndThings;

internal sealed class NpcDebugAutoScroll : MonoBehaviour
{
    internal TMP_Text Label = null!;
    private string? previous;
    private void LateUpdate()
    {
        if (Label == null || Label.text == previous) return;
        previous = Label.text;
        var scroll = GetComponent<ScrollRect>();
        if (scroll == null) return;
        // Run after the periodic text updater; recalculate only when log content changes.
        Canvas.ForceUpdateCanvases();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 0;
    }
}
