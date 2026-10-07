using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Game.Progression;
using Game.State;
using Newtonsoft.Json;
using Track;
using UI.Builder;
using UI.Common;
using UI.Menu;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal sealed class NewGameStartSelection
{
    public int Schema = 2;
    public string ProgressionId = "ewh";
    public string BaseSetup = "ewh-steam";
    public string Origin = StartingProgressionPolicy.OriginalOrigin;
    public string AreaId = string.Empty;
    public HashSet<string> Unlocked = new HashSet<string>();
    public bool ChooseAfterMapLoad = true;
}

internal static class NewGameStartOptions
{
    internal const string SetupPrefix = "TNT-START-1:";
    internal const string SaveKey = "RMROC451.TweaksAndThings.Start.v1";
    private static readonly ConditionalWeakTable<NewGameMenu, NewGameStartSelection> selections = new();
    private static Graph? inspectedGraph;
    private static int inspectedRevision = -1;
    internal static void InspectLoadedMap()
    {
        if (!WorldPatchReadiness.Ready) return;
        if (inspectedGraph == Graph.Shared && inspectedRevision == WorldPatchReadiness.Revision) return;
        var progressions = Resources.FindObjectsOfTypeAll<Progression>().Where(p => p.gameObject.scene.IsValid()).ToList();
        if (progressions.Count == 0) return;
        inspectedGraph = Graph.Shared; inspectedRevision = WorldPatchReadiness.Revision;
        // Inspect native map data too; discovery never depends on a map mod.
        foreach (var progression in progressions)
            foreach (var option in Options(progression))
                ModDiagnosticLog.Write("START-AREAS", progression.identifier + ": " + option.Name +
                    (option.Reason.Length == 0 ? " — eligible staging track" : " — " + option.Reason));
    }
    internal sealed class Option
    {
        internal string Origin = string.Empty;
        internal string AreaId = string.Empty;
        internal string Name = string.Empty;
        internal string Reason = string.Empty;
    }
    internal static NewGameStartSelection Selection(NewGameMenu menu) => selections.GetValue(menu, _ => new NewGameStartSelection());
    internal static Progression? FindProgression(string id) => Resources.FindObjectsOfTypeAll<Progression>()
        .FirstOrDefault(p => p.gameObject.scene.IsValid() && p.identifier == id);
    internal static Dictionary<string, string[]> Dependencies(Progression progression) =>
        progression.GetComponentsInChildren<Section>(true).Where(s => s.identifier != StartingProgressionPolicy.OriginalOrigin)
            .ToDictionary(s => s.identifier, s => (s.prerequisiteSections ?? Array.Empty<Section>())
                .Where(p => p != null && p.identifier != StartingProgressionPolicy.OriginalOrigin).Select(p => p.identifier).ToArray(), StringComparer.Ordinal);
    internal static List<Option> Options(Progression progression)
    {
        var result = new List<Option> { new Option { Origin = StartingProgressionPolicy.OriginalOrigin, Name = "East Whittier (original start)" } };
        var dependencies = Dependencies(progression);
        var sections = progression.GetComponentsInChildren<Section>(true);
        foreach (var section in sections.Where(s => s.identifier != StartingProgressionPolicy.OriginalOrigin))
        {
            string reason = string.Empty;
            try { StartingProgressionPolicy.Select(dependencies, Array.Empty<string>(), section.identifier); }
            catch (ArgumentException ex) { reason = ex.Message; }
            var areas = (section.enableFeaturesOnUnlock ?? Array.Empty<MapFeature>()).Where(f => f != null)
                .SelectMany(f => f.areasEnableOnUnlock ?? Array.Empty<Model.Ops.Area>()).Where(a => a != null).Distinct();
            foreach (var area in areas)
            {
                var option = new Option { Origin = section.identifier, AreaId = area.identifier, Name = area.name + " start", Reason = reason };
                if (option.Reason.Length == 0 && !HasStagingTrack(progression, section.identifier, area.identifier))
                    option.Reason = "No usable staging track in the area's starting unlocks.";
                result.Add(option);
            }
        }
        return result.GroupBy(o => o.Origin + ":" + o.AreaId).Select(g => g.First()).ToList();
    }
    internal static Dictionary<string, string[]> SelectionDependencies(Progression progression, NewGameStartSelection choice) => choice.Schema == 1 ? StartingProgressionPolicy.ForOrigin(Dependencies(progression), choice.Origin) : StartingProgressionPolicy.Validate(Dependencies(progression));
    internal static HashSet<string> TrackGroups(Progression progression, string origin)
    {
        var required = origin == StartingProgressionPolicy.OriginalOrigin ? new HashSet<string>() :
            StartingProgressionPolicy.Select(Dependencies(progression), Array.Empty<string>(), origin);
        var features = (progression.enableFeaturesAtStart ?? Array.Empty<MapFeature>()).Concat(
            progression.GetComponentsInChildren<Section>(true).Where(s => required.Contains(s.identifier))
                .SelectMany(s => s.enableFeaturesOnUnlock ?? Array.Empty<MapFeature>())).ToArray();
        var groups = new HashSet<string>((features ?? Array.Empty<MapFeature>()).Where(f => f != null)
            .SelectMany(f => f.trackGroupsEnableOnUnlock ?? Array.Empty<string>()));
        // Common track need not belong to an unlock feature. It remains usable
        // at every origin; previously this omitted native yards such as Bryson.
        var controlled = Resources.FindObjectsOfTypeAll<MapFeature>().Where(f => f.gameObject.scene.IsValid())
            .SelectMany(f => (f.trackGroupsEnableOnUnlock ?? Array.Empty<string>())
                .Concat(f.trackGroupsAvailableOnUnlock ?? Array.Empty<string>())).ToHashSet();
        if (Graph.Shared != null)
            groups.UnionWith(Graph.Shared.Segments.Where(s => !controlled.Contains(s.groupId)).Select(s => s.groupId));
        return groups;
    }
    internal static bool HasStagingTrack(Progression progression, string origin, string areaId)
    {
        if (Graph.Shared == null) return false;
        var area = Resources.FindObjectsOfTypeAll<Model.Ops.Area>().FirstOrDefault(a => a.identifier == areaId && a.gameObject.scene.IsValid());
        if (area == null) return false;
        var groups = TrackGroups(progression, origin);
        return Graph.Shared.Segments.Any(s => groups.Contains(s.groupId) && s.GetLength() >= 40 &&
            Vector3.Distance(new Location(s, s.GetLength() / 2, TrackSegment.End.A).GetPosition(), Helpers.WorldTransformer.WorldToGame(area.transform.position)) <= area.radius);
    }
    internal static void Build(NewGameMenu menu, UIPanelBuilder builder)
    {
        if ((int)menu._gameMode != 1) return;
        var choice = Selection(menu);
        if (choice.ProgressionId != menu._progressionId && !string.IsNullOrEmpty(menu._progressionId))
        { choice.ProgressionId = menu._progressionId; choice.Origin = StartingProgressionPolicy.OriginalOrigin; choice.Unlocked.Clear(); }
        var progression = FindProgression(choice.ProgressionId);
        if (!WorldPatchReadiness.Ready || progression == null)
        {
            builder.AddSection("Starting area and progression");
            // The main menu has no map graph. Do not ask the user to refresh
            // indefinitely for objects that cannot exist until creation starts.
            builder.AddFieldToggle("Choose starting area after map loads", () => choice.ChooseAfterMapLoad,
                value => choice.ChooseAfterMapLoad = value);
            builder.AddLabel("Create the railroad to choose from its loaded map. Starter equipment waits until you confirm the starting area and progression unlocks. Uncheck to use the original East Whittier start.");
            return;
        }
        choice.ChooseAfterMapLoad = false;
        BuildLoaded(choice, progression, builder);
    }
    internal static void BuildLoaded(NewGameStartSelection choice, Progression progression, UIPanelBuilder builder)
    {
        var options = Options(progression);
        var valid = options.Where(o => o.Reason.Length == 0).ToList();
        builder.AddSection("Starting area and progression", section =>
        {
            int index = Math.Max(0, valid.FindIndex(o => o.Origin == choice.Origin && o.AreaId == choice.AreaId));
            section.AddField("Starting area", section.AddDropdown(valid.Select(o => o.Name).ToList(), index, selected =>
            {
                choice.Origin = valid[selected].Origin; choice.AreaId = valid[selected].AreaId;
                choice.Unlocked.Clear();
                if (choice.Origin != StartingProgressionPolicy.OriginalOrigin) choice.Unlocked = StartingProgressionPolicy.Select(Dependencies(progression), choice.Unlocked, choice.Origin);
                builder.Rebuild();
            }));
            section.AddLabel("Selecting an unlock includes its prerequisites. Removing one also removes dependent selections. The selected starting area is required.");
            Dictionary<string, string[]> graph;
            try { graph = SelectionDependencies(progression, choice); }
            catch (ArgumentException ex) { section.AddLabel(ex.Message); return; }
            var names = progression.GetComponentsInChildren<Section>(true).ToDictionary(s => s.identifier, s => s.displayName);
            names[StartingProgressionPolicy.OriginalOrigin] = "East Whittier / original starting territory";
            foreach (var item in graph)
            {
                string id = item.Key;
                bool required = choice.Origin != StartingProgressionPolicy.OriginalOrigin && StartingProgressionPolicy.Select(graph, Array.Empty<string>(), choice.Origin).Contains(id);
                section.AddFieldToggle(names.TryGetValue(id, out var name) ? name : id,
                    () => choice.Unlocked.Contains(id), selected =>
                    {
                        choice.Unlocked = selected ? StartingProgressionPolicy.Select(graph, choice.Unlocked, id) :
                            StartingProgressionPolicy.Remove(graph, choice.Unlocked, id);
                        builder.Rebuild();
                    }).Disable(required);
                if (required) section.AddLabel("Required starting area");
                else if (item.Value.Length > 0) section.AddLabel("Requires: " + string.Join(", ", item.Value.Select(p => names.TryGetValue(p, out var display) ? display : p)));
            }
            foreach (var unavailable in options.Where(o => o.Reason.Length > 0)) section.AddLabel(unavailable.Name + " — unavailable: " + unavailable.Reason);
            section.AddButtonCompact("Refresh after map changes", () => builder.Rebuild());
        });
    }
    internal static bool ValidateSelection(NewGameMenu menu, out string error)
    {
        error = string.Empty;
        var choice = Selection(menu);
        choice.BaseSetup = menu._setupId;
        if (choice.ChooseAfterMapLoad)
        {
            if (!string.IsNullOrEmpty(choice.BaseSetup)) return true;
            error = "Choose valid starting equipment before creating the railroad."; return false;
        }
        return ValidateLoaded(choice, out error);
    }
    internal static bool ValidateLoaded(NewGameStartSelection choice, out string error)
    {
        error = string.Empty;
        if (choice.Origin == StartingProgressionPolicy.OriginalOrigin && choice.Unlocked.Count == 0) return true;
        if (!WorldPatchReadiness.Ready) { error = "Map patches are still settling. Refresh starting areas and try again."; return false; }
        var progression = FindProgression(choice.ProgressionId);
        if (progression == null) { error = "The selected progression is no longer available."; return false; }
        try
        {
            var graph = SelectionDependencies(progression, choice);
            foreach (string id in choice.Unlocked)
                if (!graph.ContainsKey(id) || graph[id].Any(p => !choice.Unlocked.Contains(p)))
                    throw new ArgumentException("Starting unlocks no longer satisfy prerequisites. Refresh and select them again.");
            if (choice.Origin != StartingProgressionPolicy.OriginalOrigin &&
                (!choice.Unlocked.Contains(choice.Origin) || !HasStagingTrack(progression, choice.Origin, choice.AreaId)))
                throw new ArgumentException("The selected start no longer has valid starting territory and staging track.");
            if (string.IsNullOrEmpty(choice.BaseSetup)) throw new ArgumentException("Choose a valid starting equipment setup.");
            return true;
        }
        catch (ArgumentException ex) { error = ex.Message; return false; }
    }
    internal static string Encode(NewGameStartSelection selection) => SetupPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(selection)));
    internal static bool TryDecode(string? setupId, out NewGameStartSelection? selection)
    {
        selection = null;
        if (setupId?.StartsWith(SetupPrefix, StringComparison.Ordinal) != true) return false;
        try
        {
            selection = JsonConvert.DeserializeObject<NewGameStartSelection>(Encoding.UTF8.GetString(Convert.FromBase64String(setupId.Substring(SetupPrefix.Length))));
            return (selection?.Schema == 1 || selection?.Schema == 2) && selection.Unlocked != null && !string.IsNullOrWhiteSpace(selection.BaseSetup);
        }
        catch (Exception ex) when (ex is FormatException || ex is JsonException) { return false; }
    }
}
