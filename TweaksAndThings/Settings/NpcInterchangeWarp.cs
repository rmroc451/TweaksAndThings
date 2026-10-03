using System.Collections;
using Game;
using Game.Messages;
using Game.State;
using Model.Ops;
using UI.Common;

namespace RMROC451.TweaksAndThings;

internal static class NpcInterchangeWarp
{
    private static bool preparing;
    internal static bool Enabled => TweaksAndThingsPlugin.Instance?.IsEnabled == true &&
        TweaksAndThingsPlugin.Instance.settings.InterchangeService == InterchangeServiceMode.Simulated;

    internal static string Summary()
    {
        if (preparing) return "Calculating next AI dispatch…";
        if (OpsController.Shared == null || !NpcServiceStore.Load()) return "Load a railroad to see the next dispatch.";
        var next = SimulatedInterchangeService.NextNeededDispatch(TweaksAndThingsPlugin.Instance.settings, TimeWeather.Now.TotalSeconds);
        return next.HasValue ? "Next needed AI spawn: " + NpcRandomTrafficPolicy.Clock(next.Value) : "No pending AI dispatch; click to refresh demand.";
    }

    internal static void Request()
    {
        if (!StateManager.IsHost || StateManager.Shared == null || StateManager.Shared.IsWaiting || preparing) return;
        StateManager.Shared.StartCoroutine(Prepare());
    }

    private static IEnumerator Prepare()
    {
        preparing = true;
        try
        {
            var settings = TweaksAndThingsPlugin.Instance.settings;
            if (!Enabled || OpsController.Shared == null || !NpcServiceStore.Load()) yield break;
            while (!SimulatedInterchangeService.PrepareWarp(settings))
            {
                if (!Enabled || StateManager.Shared == null || StateManager.IsUnloading || OpsController.Shared == null) yield break;
                NpcTrainOperations.AdvancePlanning(); yield return null;
            }
            double now = TimeWeather.Now.TotalSeconds;
            var next = SimulatedInterchangeService.NextNeededDispatch(settings, now, refreshDemand: true);
            if (!next.HasValue) { Toast.Present("No interchange AI service needs dispatch. See interchange status for demand or route blockers."); yield break; }
            if (next.Value <= now + 1)
            { SimulatedInterchangeService.DispatchAtWarpBoundary(settings); yield break; }
            // A one-second tolerance covers the float hours used by the native wait message.
            StateManager.ApplyLocal(new WaitTime { Hours = (float)((next.Value - now + 1) / 3600) });
        }
        finally { preparing = false; }
    }
}
