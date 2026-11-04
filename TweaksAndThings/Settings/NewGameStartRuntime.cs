using System;
using System.Collections.Generic;
using System.Linq;
using Game.Progression;
using Game.State;
using Helpers;
using KeyValue.Runtime;
using Model;
using Model.Ops;
using Track;
using UnityEngine;
using Section = Game.Progression.Milestone;

namespace RMROC451.TweaksAndThings;

internal static class NewGameStartRuntime
{
    private sealed class Backup
    {
        internal MapFeature[] Features = Array.Empty<MapFeature>();
        internal Dictionary<Milestone, MilestoneState> States = new();
    }
    private static readonly Dictionary<Progression, Backup> backups = new();
    internal static void Configure(Progression progression)
    {
        Restore(progression);
        if (!NewGameStartOptions.TryDecode(StateManager.Shared?.Storage.SetupId, out var choice) || choice == null ||
            choice.Schema != 1 || choice.ProgressionId != progression.identifier || choice.Origin == StartingProgressionPolicy.OriginalOrigin) return;
        var required = StartingProgressionPolicy.ForOrigin(NewGameStartOptions.Dependencies(progression), choice.Origin);
        var backup = new Backup { Features = progression.enableFeaturesAtStart ?? Array.Empty<MapFeature>(),
            States = progression.Milestones.ToDictionary(m => m, m => m.State) };
        backups[progression] = backup;
        var selected = progression.Milestones.Where(m => required.ContainsKey(m.Identifier)).ToArray();
        progression.enableFeaturesAtStart = selected.SelectMany(m => m.enableFeaturesOnComplete ?? new List<MapFeature>()).Distinct().ToArray();
        foreach (var milestone in selected) milestone.MarkCompleted();
        ModDiagnosticLog.Write("NEW GAME", "Applied saved starting origin " + choice.Origin + " with reverse expansion dependencies.");
    }
    internal static void Restore(Progression progression)
    {
        if (!backups.TryGetValue(progression, out var backup)) return;
        progression.enableFeaturesAtStart = backup.Features;
        foreach (var pair in backup.States) pair.Key.State = pair.Value;
        backups.Remove(progression);
    }
    internal static void ApplyNewGame(StateManager state, NewGameSetup setup)
    {
        if (!NewGameStartOptions.TryDecode(setup.SetupId, out var choice) || choice == null) return;
        var values = choice.Unlocked.Select(Value.String).ToList();
        state._gameSetupPropertyPresets.Add(("_progression", "unlocked", Value.Array(values)));
        state._gameSetupPropertyPresets.Add((GameStorage.ObjectId, NewGameStartOptions.SaveKey,
            Value.String(Newtonsoft.Json.JsonConvert.SerializeObject(choice))));
    }
    internal static SetupDescriptor? GetSetup(string? id)
    {
        if (!NewGameStartOptions.TryDecode(id, out var choice) || choice == null) return null;
        var all = Resources.FindObjectsOfTypeAll<SetupDescriptor>().Where(d => d.gameObject.scene.IsValid()).ToList();
        var existing = all.FirstOrDefault(d => d.identifier == id);
        if (existing != null) return existing;
        var template = all.FirstOrDefault(d => d.identifier == choice.BaseSetup);
        if (template == null) throw new InvalidOperationException("Starting equipment setup is unavailable: " + choice.BaseSetup);
        if (choice.Origin == StartingProgressionPolicy.OriginalOrigin)
        {
            var normal = UnityEngine.Object.Instantiate(template);
            normal.identifier = id!;
            return normal;
        }
        var progression = NewGameStartOptions.FindProgression(choice.ProgressionId) ?? throw new InvalidOperationException("Starting progression unavailable.");
        var area = OpsController.Shared?.Areas.FirstOrDefault(a => a.identifier == choice.AreaId)
            ?? throw new InvalidOperationException("Starting area unavailable.");
        var groups = NewGameStartOptions.TrackGroups(progression, choice.Origin);
        foreach (var unlock in choice.Unlocked.Where(u => u != StartingProgressionPolicy.OriginalOrigin))
            groups.UnionWith(NewGameStartOptions.TrackGroups(progression, unlock));
        var planned = NewGameEquipmentPlacement.Plan(template, area, groups);
        if (planned.Count == 0) throw new InvalidOperationException("No starting equipment is defined.");
        var root = new GameObject("TweaksAndThings " + area.name + " starting setup");
        var descriptor = root.AddComponent<SetupDescriptor>();
        descriptor.identifier = id!;
        descriptor.initialMoney = template.initialMoney;
        descriptor.showTutorial = false;
        var spawnObject = new GameObject("Starting character"); spawnObject.transform.SetParent(root.transform);
        var spawn = spawnObject.AddComponent<Character.SpawnPoint>();
        spawn.transform.position = WorldTransformer.GameToWorld(planned[0].front.GetPosition()) + Vector3.up * 2;
        descriptor.spawnPoint = spawn;
        descriptor.placements = new SetupDescriptor.CarPlacement[planned.Count];
        for (int i = 0; i < planned.Count; i++)
        {
            var markerObject = new GameObject("Starting equipment " + i); markerObject.transform.SetParent(root.transform);
            var marker = markerObject.AddComponent<TrackMarker>();
            marker.id = "TNT-START-" + i;
            marker.Location = planned[i].front;
            var placement = planned[i].placement;
            placement.marker = marker;
            descriptor.placements[i] = placement;
        }
        ModDiagnosticLog.Write("NEW GAME", $"Prepared {area.name} starter equipment in {planned.Count} switch-clear yard and engine-service cuts.");
        return descriptor;
    }
}
