using HarmonyLib;
using Railloader;
using System;
using UI.ContextMenu;
using UnityEngine;


namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(UI.ContextMenu.ContextMenu))]
//ContextMenuQuadrant quadrant, int index, float normalizedRadius = 1f
[HarmonyPatch(nameof(UI.ContextMenu.ContextMenu.PositionForItem), typeof(ContextMenuQuadrant), typeof(int), typeof(float))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class ContextMenu_PositionForItem_Patch
{
    static void Postfix(UI.ContextMenu.ContextMenu __instance, ref Vector2 __result, ContextMenuQuadrant quadrant, int index, float normalizedRadius = 1f)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled) return;

        float num = __instance._itemAngles[(quadrant, index)] * ((float)Math.PI / 180f);
        float num2 = __instance.radius * normalizedRadius;
        __result = new Vector2(1.3f * Mathf.Sin(num) * num2, Mathf.Cos(num) * num2);
    }
}
