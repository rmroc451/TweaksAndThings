using HarmonyLib;
using Railloader;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UI.Builder;
using UI.PreferencesWindow;
using UnityEngine.InputSystem;


namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(BindingsWindow))]
[HarmonyPatch(nameof(BindingsWindow.Build), typeof(UIPanelBuilder))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class BindingsWindow_Build_Patch
{
    public static bool Prefix(BindingsWindow __instance, UIPanelBuilder builder)
    {
        return true;
        //TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        //if (!tweaksAndThings.IsEnabled) return true;

        //(string title, InputAction[] actions)[] rebindableActions = BindingsWindow.RebindableActions;


        
        //HashSet<InputAction> conflicts = BindingsWindow.FindConflicts(rebindableActions.SelectMany(((string title, InputAction[] actions) t) => t.actions));
        //__instance._conflicts = conflicts;
        //__instance._builder = builder;
        //builder.AddTabbedPanels(__instance._selectedTabState, delegate (UITabbedPanelBuilder uITabbedPanelBuilder)
        //{
        //    (string, InputAction[])[] array = rebindableActions;
        //    for (int i = 0; i < array.Length; i++)
        //    {
        //        var (text, actions) = array[i];
        //        uITabbedPanelBuilder.AddTab(text, text, delegate (UIPanelBuilder uIPanelBuilder)
        //        {
        //            uIPanelBuilder.VScrollView(delegate (UIPanelBuilder uIPanelBuilder2)
        //            {
        //                InputAction[] array2 = actions;
        //                foreach (InputAction val in array2)
        //                {
        //                    //uIPanelBuilder2.AddInputBindingControl(val, conflicts.Contains(val), DidRebind);
        //                }
        //            });
        //        });
        //    }
        //});
        //return false;
    }
}
