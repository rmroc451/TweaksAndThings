using System.Collections.Generic;
using Game;
using Game.State;
using Model;
using Model.Ops;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcTrafficDiagnostics
{
    private static readonly Dictionary<string, string> states = new();
    private static readonly Dictionary<string, string> loggedStates = new();
    private static readonly Dictionary<string, float> lastLog = new();
    private static float nextSnapshot;
    internal static string Status(string key) => states.TryGetValue(key, out var status) ? status : "Waiting for scheduler";
    internal static void UpdateFileSnapshot()
    {
        if (Time.realtimeSinceStartup < nextSnapshot || OpsController.Shared == null) return;
        nextSnapshot = Time.realtimeSinceStartup + 120;
        if (StateManager.IsHost && NpcServiceStore.Load()) { NpcHeldDeliveryTracking.ReconcileCars(); NpcServiceStore.Save(); }
        ModDiagnosticLog.Write("SNAPSHOT", Snapshot());
    }

    internal static void Detail(string key, string message) =>
        ModDiagnosticLog.Write("TRAFFIC", "game=" + TimeWeather.Now + " " + message, key);
    internal static void Report(string key, string text)
    {
        states[key] = text;
        if (loggedStates.TryGetValue(key, out var previous) && previous == text) return;
        if (lastLog.TryGetValue(key, out float last) && Time.realtimeSinceStartup - last < 30f) return;
        lastLog[key] = Time.realtimeSinceStartup;
        loggedStates[key] = text;
        TweaksAndThingsPlugin.LogDiagnostic("Traffic " + key + ": " + text);
    }

    internal static string Snapshot()
    {
        var settings = TweaksAndThingsPlugin.Instance?.settings;
        var lines = new List<string> { $"NPC interchange traffic: enabled={TweaksAndThingsPlugin.Instance?.IsEnabled}, mode={settings?.InterchangeService}, spawn={settings?.InterchangeSpawnMode}, host={StateManager.IsHost}" };
        if (OpsController.Shared == null || TrainController.Shared == null)
        { lines.Add("Waiting for a loaded railroad."); return string.Join("\n", lines); }
        lines.Add("Game time: " + TimeWeather.Now);
        foreach (var interchange in OpsController.Shared.EnabledInterchanges)
        {
            lines.Add($"{interchange.DisplayName} [{interchange.Identifier}]: next service {interchange.GetNextServiceTime(TimeWeather.Now, out _)}; {NpcTrainOperations.ApproachStatus(interchange.Identifier)}");
            if (states.TryGetValue(interchange.Identifier, out var status)) lines.Add("  " + status);
        }
        if (NpcServiceStore.Load()) foreach (var service in NpcServiceStore.State.Services)
        {
            string coupled = TrainController.Shared.TryGetCarForId(service.LeadId, out var lead)
                ? string.Join(",", System.Linq.Enumerable.Select(lead.EnumerateCoupled(), c => c.id + ":" + c.DisplayName)) : "LEAD MISSING";
            lines.Add($"Active {service.Id} {service.InterchangeId} {service.TrainSymbol}: {service.Phase}, planned={service.PlannedInboundCars}, setout {service.SetoutDone}/{service.Inbound.Count}, pickup {service.PickupDone}/{service.PickupSnapshot.Count}, due={new GameDateTime((float)service.Scheduled)}, next transfer={new GameDateTime((float)service.NextTransfer)}, wait={service.WaitingReason}, spawn={service.Spawn}, target={service.Target}, lead={service.LeadId}; coupled={coupled}");
        }
        if (NpcServiceStore.Load()) lines.Add($"Held deliveries: {NpcServiceStore.State.HeldDeliveries.Count}; delinquent pickups: {NpcServiceStore.State.DelinquentPickups.Count}");
        if (NpcServiceStore.Load()) foreach (var pool in NpcServiceStore.State.PoolPower)
            lines.Add($"Pool power {pool.Id}: interchange={pool.InterchangeId}; area={pool.AreaId}; vehicles={string.Join(",", pool.PowerIds)}; park={pool.ParkingLocation}; exit={pool.ExitLocation}");
        return string.Join("\n", lines);
    }
}
