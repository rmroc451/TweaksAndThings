using System.Linq;
using System.Collections.Generic;
using Model;
using HarmonyLib;
using Model.AI;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

// RouteSearch uses maximumLength to choose where a reversing move can stop.
// Reserve the turnout's fouling envelope in addition to the complete power/cut.
[HarmonyPatch(typeof(AutoEngineerPlanner), nameof(AutoEngineerPlanner.CalculateTotalLength), new[] { typeof(List<Car>) })]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_ReversalLength_Patch
{
    private static void Postfix(List<Car> __0, ref float __result)
    {
        var cars = __0;
        if (cars == null || cars.Count == 0) return;
        var lead = cars.OfType<BaseLocomotive>().FirstOrDefault();
        if (lead == null || AeLocalService.Job(lead) == null) return;
        var nodes = cars.SelectMany(c => new[] { c.LocationF.segment.a, c.LocationF.segment.b,
            c.LocationR.segment.a, c.LocationR.segment.b });
        float fouling = nodes.Where(n => n != null).Distinct().Select(NpcTrackClearance.Fouling).DefaultIfEmpty(0).Max();
        __result = Mathf.Max(__result, cars.Sum(c => c.carLength)) + fouling + 8;
    }
}

// Native AE accepts a waypoint anywhere underneath the train. Local spotting
// requires the complete delivery/deferred cut on its intended track instead.
[HarmonyPatch(typeof(AutoEngineerPlanner), nameof(AutoEngineerPlanner.CheckWaypointArrival), new[] { typeof(OrderWaypoint) })]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class AeLocal_SpotCompletion_Patch
{
    private static void Postfix(AutoEngineerPlanner __instance, ref WaypointArrival __result)
    {
        if (__result != WaypointArrival.Arrived) return;
        var lead = __instance._locomotive;
        var job = AeLocalService.Job(lead);
        if (job == null || job.Phase != "Moving" || job.CoupleTo.Length > 0) return;
        if (job.AfterMove == "Deliver" || job.AfterMove == "Stage cut" || job.AfterMove == "Yard drop" || job.AfterMove == "At yard")
            if (!AeLocalService.SpotSatisfied(job, lead)) __result = WaypointArrival.NotYet;
    }
}
