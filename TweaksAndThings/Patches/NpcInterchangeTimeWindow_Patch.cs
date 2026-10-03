using Game.Messages;
using Game.State;
using HarmonyLib;
using TMPro;
using UI;
using UI.Builder;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(TimeWindow), "Build")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcInterchangeTimeWindow_Patch
{
    private static bool Prefix(TimeWindow __instance, UIPanelBuilder builder)
    {
        if (TweaksAndThingsPlugin.Instance?.IsEnabled != true) return true;
        bool authorized = StateManager.CheckAuthorizedToSendMessage(default(WaitTime));
        __instance._window.SetContentSize(authorized ? new Vector2(650, 550) : new Vector2(300, 150));
        builder.Spacing = 8;
        builder.AddLabel(__instance.GetTimeString, UIPanelBuilder.Frequency.Fast).HorizontalTextAlignment(HorizontalAlignmentOptions.Center);
        if (authorized)
        {
            builder.AddSection("Pass Time");
            builder.AddLabel("<style=Footnote>Does not affect train movement. Stops when NPC traffic spawns.");
            builder.HStack(row =>
            {
                row.AddButton("Wait 5 min", () => TimeWindow.Wait(1f / 12));
                row.AddButton("15 min", () => TimeWindow.Wait(0.25f));
                row.AddButton("1 hr", () => TimeWindow.Wait(1));
                row.AddButton("6 hr", () => TimeWindow.Wait(6));
            });
            if (NpcInterchangeWarp.Enabled)
            {
                builder.AddButton("Next interchange AI", NpcInterchangeWarp.Request)
                    .Tooltip("Next interchange AI", "Wait until the earliest needed AI interchange train spawns, including its travel lead time.");
                builder.AddLabel(NpcInterchangeWarp.Summary, UIPanelBuilder.Frequency.Periodic);
            }
            else builder.AddButton("Sleep", () =>
            {
                __instance.GetSleepValues(out _, out _, out float hours);
                TimeWindow.Wait(hours);
            });
            builder.HVScrollView(panel => NpcSimulationSpeed.Build(panel)).Height(280);
        }
        builder.AddExpandingVerticalSpacer();
        return false;
    }
}
