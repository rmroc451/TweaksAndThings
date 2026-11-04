using System.Reflection;
using System.Linq;
using HarmonyLib;
using Track.Signals;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RMROC451.TweaksAndThings;

/// <summary>Provides a player-view button that temporarily brings the native CTC panel to camera.</summary>
internal static class DoubleMainlineHudOverlay
{
    private const string ButtonName = "RMROC451.CtcHudButton";
    private static CompassHUD? observedHud;
    private static GameObject? buttonObject;
    private static Transform? originalParent;
    private static GameObject? overlayRoot;
    private static bool originalActive;
    private static Vector3 originalPosition;
    private static Quaternion originalRotation;
    private static Vector3 originalScale;
    private static bool overlayOpen;

    internal static void Update(bool enabled)
    {
        var hud = Object.FindObjectsByType<CompassHUD>(FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null && candidate.gameObject.activeInHierarchy);
        if (!enabled || hud == null)
        {
            CloseOverlay();
            if (buttonObject != null) Object.Destroy(buttonObject);
            buttonObject = null;
            observedHud = null;
            return;
        }

        if (hud != observedHud || buttonObject == null)
        {
            CloseOverlay();
            if (buttonObject != null) Object.Destroy(buttonObject);
            observedHud = hud;
            CreateButton(hud);
        }
    }

    private static void CreateButton(CompassHUD hud)
    {
        var button = new GameObject(ButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        button.transform.SetParent(hud.transform, false);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);
        rect.sizeDelta = new Vector2(112f, 42f);
        var image = button.GetComponent<Image>();
        image.color = new Color(0.11f, 0.15f, 0.19f, 0.94f);
        var control = button.GetComponent<Button>();
        control.targetGraphic = image;
        control.interactable = true;
        control.navigation = new Navigation { mode = Navigation.Mode.None };
        control.onClick.AddListener(ToggleOverlay);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(button.transform, false);
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
        label.text = "CTC PANEL";
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.fontSize = 18f;
        label.color = Color.white;
        button.transform.SetAsLastSibling();
        buttonObject = button;
    }

    private static void ToggleOverlay()
    {
        if (overlayOpen) CloseOverlay();
        else OpenOverlay();
    }

    private static void OpenOverlay()
    {
        var panel = CTCPanelController.Shared ?? Object.FindObjectsByType<CTCPanelController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).FirstOrDefault();
        var camera = Camera.main ?? Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .Where(candidate => candidate != null && candidate.isActiveAndEnabled)
            .OrderByDescending(candidate => candidate.depth)
            .FirstOrDefault();
        if (panel == null || camera == null)
        {
            ModLog.Warning("Could not open CTC HUD overlay: panel or player camera was not found.");
            SetButtonLabel("CTC UNAVAILABLE");
            return;
        }
        var panelRoot = typeof(CTCPanelController).GetField("ctcGameObject", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(panel) as GameObject;
        if (panelRoot == null)
        {
            ModLog.Warning("Could not open CTC HUD overlay: the panel root was not found.");
            SetButtonLabel("CTC UNAVAILABLE");
            return;
        }

        var transform = panelRoot.transform;
        overlayRoot = panelRoot;
        originalActive = panelRoot.activeSelf;
        originalParent = transform.parent;
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
        transform.SetParent(camera.transform, false);
        transform.localPosition = new Vector3(0.45f, -0.22f, 1.35f);
        transform.localRotation = Quaternion.identity;
        transform.localScale = originalScale * 0.35f;
        panelRoot.SetActive(true);
        overlayOpen = true;
        SetButtonLabel("CLOSE CTC");
    }

    private static void CloseOverlay()
    {
        if (!overlayOpen) return;
        var panelRoot = overlayRoot;
        if (panelRoot != null)
        {
            var transform = panelRoot.transform;
            transform.SetParent(originalParent, true);
            transform.position = originalPosition;
            transform.rotation = originalRotation;
            transform.localScale = originalScale;
            panelRoot.SetActive(originalActive);
        }
        originalParent = null;
        overlayRoot = null;
        overlayOpen = false;
        SetButtonLabel("CTC PANEL");
    }

    private static void SetButtonLabel(string text)
    {
        var label = buttonObject == null ? null : buttonObject.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (label != null) label.text = text;
    }
}
