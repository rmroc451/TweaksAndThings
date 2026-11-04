using Game.Messages;
using Game.State;
using HarmonyLib;
using TMPro;
using UI;
using UI.Builder;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(TimeWindow), nameof(TimeWindow.BuildContent), new[] { typeof(UIPanelBuilder) })]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcInterchangeTimeWindow_Patch
{
    private static void Prefix(UIPanelBuilder builder)
    {
        if (!TweaksAndThingsPlugin.Instance.NpcInterchangeTrafficEnabled() ||
            !StateManager.CheckAuthorizedToSendMessage(default(WaitTime))) return;

        builder.AddSection("Tweaks & Things");
        if (NpcInterchangeWarp.Enabled)
        {
            builder.AddButton("Next interchange AI", NpcInterchangeWarp.Request)
                .Tooltip("Next interchange AI", "Wait until the earliest needed AI interchange train spawns. With no AI demand, use the normal interchange service time. At midnight, finish industry evaluation and publish the current day's timetables before continuing to the recalculated dispatch time.");
            builder.AddLabel(NpcInterchangeWarp.Summary, UIPanelBuilder.Frequency.Periodic);
        }
        builder.HVScrollView(panel => NpcSimulationSpeed.Build(panel)).Height(280);
    }
}
