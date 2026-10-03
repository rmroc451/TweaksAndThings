using System.Collections.Generic;
using System.Linq;
using Game.Notices;
using Game.State;
using Model;
using Model.Ops;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcServiceHud
{
    internal const string NoticeKey = "tat-ai-service";
    private static readonly Dictionary<string, string> messages = new();
    private static NoticeManager? manager;
    private static float nextRefresh;
    internal static string DisplayPhase(NpcServiceRecord service)
    {
        if (service.Phase == "Loading") return "Preparing consist";
        bool interchange = service.InterchangeId.Length > 0;
        string approach = interchange ? (service.Phase == "Departing" ? "approach-outbound" : "approach-inbound") : "approach-" + service.StopIndex;
        return NpcServicePolicy.TravelLabel(service.Phase, service.Announcements.Contains(approach));
    }
    internal static void Update()
    {
        if (!StateManager.IsHost || TrainController.Shared == null || RestoreNotifier.Shared == null || !RestoreNotifier.Shared.HasRestored || Time.realtimeSinceStartup < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartup + 0.5f;
        var current = NoticeManager.Shared;
        if (current == null) return;
        if (manager != current) { messages.Clear(); manager = current; }
        if (!NpcServiceStore.Load()) { Hide(); return; }
        var active = new HashSet<string>();
        foreach (var service in NpcServiceStore.State.Services)
        {
            if (!TrainController.Shared.TryGetCarForId(service.LeadId, out var car) || car is not BaseLocomotive lead) continue;
            active.Add(lead.id);
            var interchange = OpsController.Shared?.AllInterchanges.FirstOrDefault(i => i.Identifier == service.InterchangeId);
            string content = service.TrainSymbol + " · " + (interchange?.DisplayName ?? "Through traffic") + " · " + DisplayPhase(service);
            if (service.InterchangeId.Length > 0)
                content += $"\nSetout {service.SetoutDone}/{service.Inbound.Count} · Pickup {service.Outbound.Take(service.PickupDone).Count(p => !p.Missed)}/{System.Math.Max(service.PickupSnapshot.Count, service.Outbound.Count)} · Missed {service.Outbound.Count(p => p.Missed)}" +
                    (service.TransferSecondsPerCar < NpcServicePolicy.SecondsPerCar ? "\nCaboose assist: 1 min/car · no charge" : "");
            if (service.WaitingReason.Length > 0) content += "\n" + service.WaitingReason;
            if (messages.TryGetValue(lead.id, out var old) && old == content) continue;
            lead.PostNotice(NoticeKey, content);
            messages[lead.id] = content;
        }
        foreach (var pool in NpcServiceStore.State.PoolPower) {
            var lead = pool.PowerIds.Select(id => TrainController.Shared.CarForId(id)).OfType<BaseLocomotive>().FirstOrDefault();
            if (lead == null) continue;
            active.Add(lead.id);
            var interchange = OpsController.Shared?.AllInterchanges.FirstOrDefault(i => i.Identifier == pool.InterchangeId);
            string content = (interchange?.DisplayName ?? pool.InterchangeId) + " · Parked pool power\nWaiting for next outbound service · outer couplings and air connections usable";
            if (messages.TryGetValue(lead.id, out var old) && old == content) continue;
            lead.PostNotice(NoticeKey, content);
            messages[lead.id] = content;
        }
        foreach (var id in messages.Keys.Where(id => !active.Contains(id)).ToList())
        {
            current.PostEphemeral(new EntityReference(EntityType.Car, id), NoticeKey, null!);
            messages.Remove(id);
        }
    }
    internal static void Hide()
    {
        if (StateManager.IsHost && NoticeManager.Shared != null)
            foreach (var id in messages.Keys) NoticeManager.Shared.PostEphemeral(new EntityReference(EntityType.Car, id), NoticeKey, null!);
        messages.Clear();
    }
}
