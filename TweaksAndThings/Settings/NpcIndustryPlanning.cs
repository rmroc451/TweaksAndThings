using System;
using System.Linq;
using Game;
using Game.State;
using Model.Ops;

namespace RMROC451.TweaksAndThings;

internal static class NpcIndustryPlanning
{
    private static OpsController? owner;
    private static NpcIndustryPlanningGate gate = new();
    private static void Bind(OpsController ops)
    {
        if (ReferenceEquals(owner, ops)) return;
        owner = ops;
        gate = new NpcIndustryPlanningGate();
    }
    internal static void Begin(OpsController ops) { Bind(ops); gate.Begin(); }
    internal static void Complete(OpsController ops)
    {
        Bind(ops);
        gate.Complete((int)Math.Floor(TimeWeather.Now.TotalSeconds / 86400));
        ModDiagnosticLog.Write("DAY PLAN", "Industry evaluation completed: " + TimeWeather.Now + "; current-day timetable planning can evaluate readiness.");
    }
    internal static bool Ready(double now)
    {
        var ops = OpsController.Shared;
        if (ops == null) return false;
        Bind(ops);
        var industries = ops.AllIndustries;
        bool restored = StateManager.Shared?.HasRestoredProperties == true && !StateManager.IsUnloading;
        bool pending = industries?.Any(i => !i.ProgressionDisabled && !i.Contract.HasValue && i.NextContract.HasValue) == true;
        bool ready = gate.CanPlan((int)Math.Floor(now / 86400), restored, industries?.Length > 0, pending);
        if (!ready) ModDiagnosticLog.Write("DAY PLAN", $"Waiting for current-day industry evaluation; game={TimeWeather.Now}; restored={restored}; industries={industries?.Length ?? 0}; first-day contracts pending={pending}.",
            "industry-planning-wait", 60);
        return ready;
    }
}
