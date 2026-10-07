using System.Collections;
using Game;
using Game.Messages;
using Game.State;
using Model.Ops;
using UI.Common;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class NpcInterchangeWarp
{
    private static bool preparing;
    internal static bool ContinueAcrossMidnight;
    internal static bool Enabled => TweaksAndThingsPlugin.Instance?.IsEnabled == true &&
        TweaksAndThingsPlugin.Instance.settings.InterchangeService == InterchangeServiceMode.Simulated;

    internal static string Summary()
    {
        if (preparing) return "Calculating next AI dispatch…";
        if (OpsController.Shared == null || !NpcServiceStore.Load()) return "Load a railroad to see the next dispatch.";
        var next = NextTarget(TweaksAndThingsPlugin.Instance.settings, TimeWeather.Now.TotalSeconds, false, out bool simulated);
        return next.HasValue ? (simulated ? "Next needed AI spawn: " : "Next normal interchange service (no AI demand): ") +
            NpcRandomTrafficPolicy.Clock(next.Value) : "No enabled interchange service is scheduled.";
    }

    internal static double? NextTarget(Settings settings, double now, bool refreshDemand, out bool simulated)
    {
        var needed = SimulatedInterchangeService.NextNeededDispatch(settings, now, refreshDemand);
        simulated = needed.HasValue;
        var native = needed.HasValue ? null : OpsController.Shared.EnabledInterchanges
            .Select(i => (double?)NormalServiceTime(i, now)).Min();
        return NpcWarpBoundaryPolicy.ServiceTarget(now, needed, native);
    }

    private static double NormalServiceTime(Interchange interchange, double now)
    {
        var due = interchange.GetNextServiceTime(new GameDateTime((float)now), out var style);
        // Native scheduling can return an overdue uncompleted service. An old
        // extra is not a repeating appointment; use the next daily slot instead.
        if (style == Interchange.NextServiceStyle.Extra && due.TotalSeconds < now)
            due = interchange.GetNextServiceTime(new GameDateTime((float)now), out _, dailyOnly: true);
        return NpcWarpBoundaryPolicy.UpcomingNormalService(now, due.TotalSeconds);
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
            var next = NextTarget(settings, now, true, out bool simulated);
            if (!next.HasValue) { Toast.Present("No enabled interchange has a scheduled service."); yield break; }
            if (simulated && next.Value <= now + 1)
            { SimulatedInterchangeService.DispatchAtWarpBoundary(settings); yield break; }
            ModDiagnosticLog.Write("TIMEWARP", (simulated ? "Waiting for AI dispatch: " : "No AI demand; waiting for normal interchange service: ") + new GameDateTime((float)next.Value));
            // A one-second tolerance covers the float hours used by the native wait message.
            ContinueAcrossMidnight = true;
            StateManager.ApplyLocal(new WaitTime { Hours = (float)((next.Value - now + 1) / 3600) });
        }
        finally { preparing = false; }
    }
}
