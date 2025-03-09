using HarmonyLib;
using Helpers;
using Railloader;
using Serilog;
using System;
using System.Collections.Generic;
using UI;
using UI.ContextMenu;
using UnityEngine;
using UnityEngine.UI;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(UI.ContextMenu.ContextMenu))]
[HarmonyPatch(nameof(UI.ContextMenu.ContextMenu.Show), typeof(string))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class ContextMenu_Show_Patch
{
    static bool Prefix(UI.ContextMenu.ContextMenu __instance, string centerText)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled) return true;

        if (!__instance.GetRootCanvas(out var rootCanvas))
        {
            Log.Warning("Couldn't get root canvas");
            return true;
        }
        __instance.CancelHideCoroutine();
        if (__instance.contentRectTransform.childCount >= 1) __instance.contentRectTransform.GetChild(0).DestroyAllChildren(); //YOINK DEM CIRCLES!
        __instance.SetupTemplate(rootCanvas);
        __instance.centerLabel.text = centerText;
        Canvas componentInParent = ((Component)__instance.contentRectTransform).GetComponentInParent<Canvas>();
        Vector3 mousePosition = Input.mousePosition;
        Vector2 val = componentInParent.ScreenToCanvasPosition(mousePosition).XY();
        Vector2 renderingDisplaySize = rootCanvas.renderingDisplaySize;
        float num = __instance.radius + 50f;
        if (val.x < num)
        {
            val.x = num;
        }
        if (val.x > renderingDisplaySize.x - num)
        {
            val.x = renderingDisplaySize.x - num;
        }
        if (val.y < num)
        {
            val.y = num;
        }
        if (val.y > renderingDisplaySize.y - num)
        {
            val.y = renderingDisplaySize.y - num;
        }
        __instance.contentRectTransform.anchoredPosition = val;
        __instance.BuildItemAngles();

        ((MonoBehaviour)__instance).StartCoroutine(__instance.AnimateButtonsShown());
        ((Component)__instance.contentRectTransform).gameObject.SetActive(true);
        UI.ContextMenu.ContextMenu.IsShown = true;
        __instance._blocker = __instance.CreateBlocker(rootCanvas);
        GameInput.RegisterEscapeHandler(GameInput.EscapeHandler.Transient, delegate
        {
            __instance.Hide();
            return true;
        });

        return false;
    }
}


[HarmonyPatch(typeof(UI.ContextMenu.ContextMenu))]
[HarmonyPatch(nameof(UI.ContextMenu.ContextMenu.DefaultAngleForItem), typeof(ContextMenuQuadrant), typeof(int), typeof(int))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class ContextMenu_DefaultAngleForItem_Patch
{
    static bool Prefix(UI.ContextMenu.ContextMenu __instance, ref float __result, ContextMenuQuadrant quadrant, int index, int quadrantItemCount)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled) return true;


        int num = quadrant switch
        {
            ContextMenuQuadrant.General => 0,
            ContextMenuQuadrant.Unused1 => 90,
            ContextMenuQuadrant.Brakes => 180,
            ContextMenuQuadrant.Unused2 => -90,
            _ => throw new ArgumentOutOfRangeException("quadrant", quadrant, null),
        };
        if (quadrantItemCount <= 1)
        {
            __result = num;
            return false;
        }
        int num2 = ((quadrantItemCount <= 3) ? 30 : (90 / (quadrantItemCount - 1)));
        __result = (float)num + -0.5f * (float)((quadrantItemCount - 1) * num2) + (float)(num2 * index);
        return false;
    }
}


[HarmonyPatch(typeof(UI.ContextMenu.ContextMenu))]
[HarmonyPatch(nameof(UI.ContextMenu.ContextMenu.AddButton), typeof(ContextMenuQuadrant), typeof(string), typeof(Sprite), typeof(Action))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class ContextMenu_AddButton_Patch
{
    static bool Prefix(UI.ContextMenu.ContextMenu __instance, ContextMenuQuadrant quadrant, string title, Sprite sprite, Action action)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled) return true;


        List<ContextMenuItem> list = __instance._quadrants[(int)quadrant];
        int index = list.Count;
        ContextMenuItem contextMenuItem = UnityEngine.Object.Instantiate<ContextMenuItem>(__instance.itemPrefab, (Transform)(object)__instance.contentRectTransform);
        contextMenuItem.image.sprite = sprite;
        contextMenuItem.label.text = title;
        contextMenuItem.OnClick = delegate
        {
            action();
            __instance.Hide((quadrant, index));
        };
        ((Component)contextMenuItem).gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        list.Add(contextMenuItem);
        return false;
    }
}