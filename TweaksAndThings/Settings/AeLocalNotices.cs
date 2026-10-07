using System;
using System.Collections.Generic;
using System.Linq;
using Game.Notices;
using Game.State;
using Model;
using Model.AI;
using Model.Ops;
using Network;

namespace RMROC451.TweaksAndThings;

internal static class AeLocalNotices
{
    private const string Key = "tat-ae-local";
    private static readonly Dictionary<string, string> notices = new();
    private static readonly Dictionary<string, string> holds = new();
    private static readonly Dictionary<string, string> announced = new();
    private static readonly Dictionary<string, float> lastAnnouncement = new();
    private static float nextRefresh;
    internal static void Observe(BaseLocomotive lead, AutoEngineer.Targets targets)
    {
        if (!StateManager.IsHost || !AeLocalService.Owns(lead)) return;
        var stop = targets.AllTargets.FirstOrDefault(t => t.SpeedMph == 0 && t.Reason != null &&
            t.Reason.IndexOf("signal", StringComparison.OrdinalIgnoreCase) >= 0);
        string text = stop.Reason == null || !lead.IsStopped() ? "" :
            "Waiting for dispatcher at signal " + (targets.NextSignal?.id ?? "unknown") + " · " + stop.Reason;
        holds[lead.id] = text;
    }
    internal static void Update()
    {
        if (!StateManager.IsHost || TrainController.Shared == null || !NpcServiceStore.Load() || UnityEngine.Time.realtimeSinceStartup < nextRefresh) return;
        nextRefresh = UnityEngine.Time.realtimeSinceStartup + 1;
        var active = new HashSet<string>();
        foreach (var job in NpcServiceStore.State.LocalJobs)
        {
            if (job.Phase == "Complete" || AeLocalService.CarFor(job.LeadId) is not BaseLocomotive lead) continue;
            active.Add(lead.id);
            string hold = holds.TryGetValue(lead.id, out var h) ? h : "";
            string location = OpsController.Shared.ClosestArea(lead)?.name ?? "Unknown area";
            string text = AeLocalService.Summary(job) + "\n" + job.Status + "\nLocation: " + location;
            if (hold.Length > 0 && job.Phase == "Moving") text += "\n" + hold;
            if (!notices.TryGetValue(lead.id, out var old) || old != text) { lead.PostNotice(Key, text); notices[lead.id] = text; }
            string dispatch = hold.Length > 0 && job.Phase == "Moving" ? hold + "; " + job.Status + "; location=" + location : "";
            if (dispatch.Length == 0) { announced.Remove(lead.id); continue; }
            if (announced.TryGetValue(lead.id, out var previous) && previous == dispatch ||
                lastAnnouncement.TryGetValue(lead.id, out float last) && UnityEngine.Time.realtimeSinceStartup - last < 60) continue;
            Multiplayer.Broadcast($"AE Local {Hyperlink.To(lead)}: {dispatch}. Please route this local toward its next task.");
            ModDiagnosticLog.Write("LOCAL DISPATCH", lead.DisplayName + ": " + dispatch);
            announced[lead.id] = dispatch; lastAnnouncement[lead.id] = UnityEngine.Time.realtimeSinceStartup;
        }
        foreach (var id in notices.Keys.Where(id => !active.Contains(id)).ToList())
        {
            NoticeManager.Shared?.PostEphemeral(new EntityReference(EntityType.Car, id), Key, null!);
            notices.Remove(id); holds.Remove(id); announced.Remove(id); lastAnnouncement.Remove(id);
        }
    }
}
