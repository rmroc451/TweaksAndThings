using System;
using System.Linq;
using HarmonyLib;
using Model.Definition.Data;
using TMPro;
using UI.Common;
using UI.Placer;
using UnityEngine;
using UnityEngine.UI;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(PlacerWindow), "_Show")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class PlacerWindow_RandomConsist_Patch
{
    private const string ButtonName = "TweaksAndThings Random Consist";

    private static void Postfix(PlacerWindow __instance)
    {
        if (__instance.placeButton == null) return;
        var parent = __instance.placeButton.transform.parent;
        var existing = parent.Find(ButtonName);
        if (existing != null) { existing.gameObject.SetActive(TweaksAndThingsPlugin.Instance?.IsEnabled == true); return; }
        var button = UnityEngine.Object.Instantiate(__instance.placeButton, parent, false);
        button.name = ButtonName;
        button.transform.SetSiblingIndex(__instance.placeButton.transform.GetSiblingIndex());
        button.enabled = true;
        button.interactable = true;
        button.onClick = new Button.ButtonClickedEvent();
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) { label.text = "Random consist…"; label.enableAutoSizing = true; }
        button.onClick.AddListener(() => Prompt(__instance));
        button.gameObject.SetActive(TweaksAndThingsPlugin.Instance?.IsEnabled == true);
    }

    private static void Prompt(PlacerWindow window)
    {
        if (window == null || TweaksAndThingsPlugin.Instance?.IsEnabled != true) return;
        // Use the same definitions and filter as the visible native library.
        var pool = window.PrefabStore.AllCarDefinitionInfos.Where(window.FiltersAllow).ToList();
        if (pool.Count == 0)
        {
            ModalAlertController.PresentOkay("Random consist", "No cars match the selected category.");
            return;
        }
        var costs = pool.Select(p => p.Definition.TryGetTenderIdentifier(out _) ? 2 : 1).ToList();
        ModalAlertController.Present("Generate random consist",
            $"Car count (1–{RandomConsistPolicy.MaximumCars}), including automatic tenders.\nReplaces the current preview using the selected category.\nClear the category filter to use all car types.",
            "20", new[] { (true, "Generate"), (false, "Cancel") }, result =>
            {
                if (!result.Item1 || window == null || TweaksAndThingsPlugin.Instance?.IsEnabled != true) return;
                if (!int.TryParse(result.Item2, out int count) ||
                    !RandomConsistPolicy.TryGenerate(costs, count, n => UnityEngine.Random.Range(0, n), out var choices))
                {
                    ModalAlertController.PresentOkay("Random consist",
                        $"Enter a whole number from 1 to {RandomConsistPolicy.MaximumCars}.\nA category containing only engines with tenders needs an even car count.");
                    return;
                }
                window._consist.Clear();
                foreach (int choice in choices)
                    window._consist.Add(new PlacerWindow.ConsistEntry { DefinitionInfo = pool[choice] });
                window.RebuildConsist();
            });
    }
}
