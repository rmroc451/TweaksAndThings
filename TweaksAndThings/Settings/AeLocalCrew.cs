using System;
using System.Linq;
using Game.Messages;
using Game.State;
using Model;
using Model.Ops;
using RMROC451.TweaksAndThings.Extensions;

namespace RMROC451.TweaksAndThings;

internal static class AeLocalCrew
{
    internal static void Ensure(AeLocalJob job, BaseLocomotive lead)
    {
        var manager = StateManager.Shared.PlayersManager;
        if (!string.IsNullOrEmpty(lead.trainCrewId) && manager.TrainCrewForId(lead.trainCrewId, out var current))
        { job.CrewId = current.Id; return; }
        if (job.CrewId.Length > 0 && manager.TrainCrewForId(job.CrewId, out _))
        { NpcTimetableRegistry.SetCrew(lead, job.CrewId); return; }
        string name = "AEL " + lead.Ident.RoadNumber;
        string description = "AE Local " + job.Id;
        manager.HandleRequestCreateTrainCrew(manager.LocalPlayer, new Snapshot.TrainCrew {
            Name = name, Description = description, TimetableSymbol = "", MemberPlayerIds = new System.Collections.Generic.HashSet<string>() });
        var created = manager.TrainCrews.First(c => c.Description == description);
        job.CrewId = created.Id;
        NpcTimetableRegistry.SetCrew(lead, job.CrewId);
        ModDiagnosticLog.Write("AE LOCAL CREW", $"{lead.DisplayName}: created {name}; crew={created.Id}.");
    }
    internal static void Sync(AeLocalJob job)
    {
        if (!StateManager.IsHost || AeLocalService.CarFor(job.LeadId) is not BaseLocomotive lead) return;
        Ensure(job, lead);
        foreach (var id in job.OwnedIds) if (AeLocalService.CarFor(id) is Car car && car.trainCrewId != job.CrewId)
            NpcTimetableRegistry.SetCrew(car, job.CrewId);
        var approaching = AeLocalService.CarFor(job.CoupleTo);
        var approachIds = approaching == null ? System.Array.Empty<string>() : AeLocalSwitchingPlan.FromEnd(approaching).Select(c => c.id).ToArray();
        var rank = job.WorkingCut.Concat(job.StageDeferredIds).Concat(approachIds).Concat(job.StageKeepIds).Concat(job.YardRemainder).Concat(job.Tasks.Select(t => t.CarId)).Concat(job.OwnedIds)
            .Distinct().ToList();
        var ids = rank.Where(id => AeLocalService.CarFor(id) is Car car && !car.MotivePower() &&
            (job.Tasks.Any(t => t.CarId == id && !t.Done) || job.OwnedIds.Contains(id))).ToList();
        var controller = OpsController.Shared.SwitchListController;
        if (!controller._switchLists.TryGetValue(job.CrewId, out var existing)) existing = new();
        // Preserve unrelated crew cars while ordering this job's cars by its work plan.
        var other = existing.Where(c => !job.Tasks.Any(t => t.CarId == c.Id)).Select(c => c.Id).ToList();
        var ordered = ids.Concat(other).ToList();
        if (existing.Select(c => c.Id).SequenceEqual(ordered)) return;
        StateManager.ApplyLocal(new SwitchListSetCarIds(job.CrewId, ordered));
        job.SwitchListIds = ids;
        ModDiagnosticLog.Write("AE LOCAL SWITCH LIST", $"{lead.DisplayName}: {ids.Count} pending cars in planned order; crew={job.CrewId}; cars={string.Join(",", ids)}.",
            "local-list:" + job.Id, 5);
    }
}
