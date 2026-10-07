using System.Linq;
using HarmonyLib;
using Model.AI;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

// RouteSearch uses maximumLength to choose where a reversing move can stop.
// Reserve the turnout's fouling envelope in addition to the complete power/cut.
[HarmonyPatch(typeof(AutoEngineerPlanner), "CalculateTotalLength")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_ReversalLength_Patch
{
    private static void Postfix(AutoEngineerPlanner __instance, ref float __result)
    {
        var lead = __instance._locomotive;
        if (AeLocalService.Job(lead) == null) return;
        var cars = lead.EnumerateCoupled().ToList();
        var nodes = cars.SelectMany(c => new[] { c.LocationF.segment.a, c.LocationF.segment.b,
            c.LocationR.segment.a, c.LocationR.segment.b });
        if (__instance._route != null) nodes = nodes.Concat(__instance._route.Select(s => s.Node));
        float fouling = nodes.Where(n => n != null).Distinct().Select(NpcTrackClearance.Fouling).DefaultIfEmpty(0).Max();
        __result = Mathf.Max(__result, cars.Sum(c => c.carLength)) + fouling + 8;
    }
}

// Native AE accepts a waypoint anywhere underneath the train. Local spotting
// requires the complete delivery/deferred cut on its intended track instead.
[HarmonyPatch(typeof(AutoEngineerPlanner), "IsWaypointSatisfied")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_SpotCompletion_Patch
{
    private static void Postfix(AutoEngineerPlanner __instance, ref bool __result)
    {
        var lead = __instance._locomotive;
        var job = AeLocalService.Job(lead);
        if (job == null || job.Phase != "Moving" || job.CoupleTo.Length > 0) return;
        if (job.AfterMove == "Deliver" || job.AfterMove == "Stage cut" || job.AfterMove == "Yard drop" || job.AfterMove == "At yard")
            __result = AeLocalService.SpotSatisfied(job, lead);
    }
}
