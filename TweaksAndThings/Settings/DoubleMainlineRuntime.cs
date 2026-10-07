using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Progression;
using Helpers;
using HarmonyLib;
using Model.Ops;
using Track;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace RMROC451.TweaksAndThings;

/// <summary>Marks a runtime-created progression level and the base track groups it follows.</summary>
internal sealed class DoubleMainlineProgressionTag : MonoBehaviour
{
    internal string[] SourceGroupIds = Array.Empty<string>();
    internal string ProgressionId = string.Empty;
    internal string LegacyProgressionId = string.Empty;
    internal string SourceCtcSectionId = string.Empty;
}

internal sealed class DoubleMainlineCtcProgressionTag : MonoBehaviour
{
    internal string[] SourceGroupIds = Array.Empty<string>();
    internal string ProgressionId = string.Empty;
    internal string LegacyProgressionId = string.Empty;
}

/// <summary>
/// Adds a second-mainline progression after each native mainline track-group progression, then
/// constructs its parallel route directly in Track.Graph when that level is unlocked.
/// </summary>
internal static class DoubleMainlineRuntime
{
    private const string IdPrefix = "RMROC451.DoubleMainline.";
    private const float MainlineEndpointTransitionLength = 400f;
    private static readonly ILogger Log = ModLog.ForContext(typeof(DoubleMainlineRuntime));
    private static readonly HashSet<string> MainlineGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "s1", "s2", "s3", "s3a", "s4", "s5"
    };

    private static readonly Dictionary<string, (string Start, string End)> GroupRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        // These are the native map's progression groups. The route anchors keep the generated
        // geometry on the line between towns rather than duplicating every yard and industry spur.
        ["s1"] = ("whittier", "sylva"),
        ["s2"] = ("bryson", "whittier"),
        ["s3"] = ("bryson", "alarka-jct"),
        ["s3a"] = ("alarka-jct", "alarka"),
        ["s4"] = ("alarka", "nantahala"),
        ["s5"] = ("nantahala", "andrews")
    };

    private static readonly (string Name, string Start, string End)[] CrossoverRoutes =
    {
        ("Andrews-Topton", "andrews", "topton"),
        ("Topton-AlarkaJct", "topton", "alarka-jct"),
        ("Bryson-Whittier", "bryson", "whittier"),
        // Cowee is a crossing on the Whittier-Sylva route rather than a town Area in the map data.
        ("Whittier-Cowee", "whittier", "sylva")
    };

    private static readonly Dictionary<string, TrackNode> ParallelNodes = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, RoutePath?> NativeRouteCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<PassingSidingDetour>> PassingSidingCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BuiltSidingConnectorGeometry = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (TrackNode Source, TrackNode Parallel)> RouteEntrySwitches = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<(string Name, TrackNode First, TrackNode Second)>> SidingCrossoverControls = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> AddedSegments = new(StringComparer.Ordinal);
    private static readonly HashSet<string> AddedCrossovers = new(StringComparer.Ordinal);
    private static int parallelNodeSnapRevision;
    private static readonly Dictionary<string, (IReadOnlyList<TrackSegment> Segments, TrackNode SourceStart,
        TrackNode SourceEnd, TrackNode ParallelStart, TrackNode ParallelEnd)> ParallelSignalRoutes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Section, int[]> OriginalCtcCosts = new();
    private static readonly HashSet<Section> DoubledCtcCosts = new();
    private static readonly System.Reflection.MethodInfo? GetPaidDeliveries = AccessTools.Method(typeof(Progression), "GetPaidDeliveriesForSectionId");
    private static readonly System.Reflection.MethodInfo? SetPaidDeliveries = AccessTools.Method(typeof(Progression), "SetPaidDeliveriesForSectionId");
    private static readonly System.Reflection.MethodInfo? GetFulfilledDeliveries = AccessTools.Method(typeof(Progression), "GetFulfilledDeliveriesForSectionId");
    private static readonly System.Reflection.MethodInfo? SetFulfilledDeliveries = AccessTools.Method(typeof(Progression), "SetFulfilledDeliveriesForSectionId");
    private static readonly System.Reflection.FieldInfo? ProgressionKeyValueObject = AccessTools.Field(typeof(Progression), "_keyValueObject");
    private static readonly Dictionary<string, (Progression Progression, string LegacyId, string CurrentId)> PendingProgressionMigrations = new(StringComparer.Ordinal);
    private static readonly HashSet<string> CompletedProgressionMigrations = new(StringComparer.Ordinal);
    private static Graph? observedGraph;
    private static Progression? observedProgression;
    private static bool generationPaused;
    private static string? stepwiseGroupId;
    private static List<TrackSegment> stepwiseSegments = new();
    private static int stepwiseIndex;

    internal static void InvalidateTrackScanCache()
    {
        NativeRouteCache.Clear();
        PassingSidingCache.Clear();
        BuiltSidingConnectorGeometry.Clear();
        NpcTrainOperations.InvalidateTrackGeometryCache();
    }

    internal static void AddProgressionLevels(Progression progression)
    {
        if (!DoubleMainlineSettings.Enabled || progression == null || string.IsNullOrWhiteSpace(progression.identifier)) return;

        var existing = progression.GetComponentsInChildren<Section>(true);
        var progressionSections = (progression.Sections ?? Array.Empty<Section>()).ToList();
        var generated = progression.GetComponentsInChildren<DoubleMainlineProgressionTag>(true)
            .Select(tag => tag.ProgressionId)
            .ToHashSet(StringComparer.Ordinal);
        Section? previousGenerated = null;

        foreach (var source in existing)
        {
            if (source == null || source.GetComponent<DoubleMainlineProgressionTag>() != null) continue;
            var groups = SourceTrackGroups(source);
            if (groups.Length == 0) continue;

            var generatedId = IdFor(source.identifier);
            if (generated.Contains(generatedId))
            {
                var existingGenerated = progression.GetComponentsInChildren<Section>(true)
                    .FirstOrDefault(section => section.identifier == generatedId);
                previousGenerated = existingGenerated;
                var existingTag = existingGenerated?.GetComponent<DoubleMainlineProgressionTag>();
                if (existingTag != null)
                    MigrateProgressionState(progression, existingTag.LegacyProgressionId, generatedId);
                continue;
            }

            var copyObject = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
            copyObject.name = IdPrefix + generatedId;
            var copy = copyObject.GetComponent<Section>();
            if (copy == null)
            {
                UnityEngine.Object.Destroy(copyObject);
                continue;
            }

            copy.identifier = generatedId;
            copy.displayName = $"{source.displayName} — Second Mainline";
            copy.description = "Build the parallel mainline track and its crossovers for this section.";
            var ctcPricingSection = FindCtcPricingSection(progression, source, groups);
            if (ctcPricingSection != null)
                copy.deliveryPhases = CloneDeliveryPhases(ctcPricingSection.deliveryPhases);
            copy.prerequisiteSections = previousGenerated == null
                ? new[] { source }
                : new[] { source, previousGenerated };

            // The source section is cloned for native delivery requirements and industry UI. Its
            // map features must not unlock the original track a second time.
            copy.enableFeaturesOnUnlock = Array.Empty<MapFeature>();
            copy.enableFeaturesOnAvailable = Array.Empty<MapFeature>();
            copy.disableFeaturesOnUnlock = Array.Empty<MapFeature>();

            var tag = copyObject.AddComponent<DoubleMainlineProgressionTag>();
            tag.SourceGroupIds = groups;
            tag.ProgressionId = generatedId;
            tag.LegacyProgressionId = LegacyIdFor(progression.identifier, source.identifier);
            tag.SourceCtcSectionId = ctcPricingSection?.identifier ?? string.Empty;
            MigrateProgressionState(progression, tag.LegacyProgressionId, generatedId);
            progressionSections.Add(copy);
            previousGenerated = copy;
            generated.Add(generatedId);
            ModLog.Information("Added second-mainline progression {SectionId} for {SourceSection} ({Groups})",
                generatedId, source.identifier, string.Join(",", groups));
            if (ctcPricingSection != null)
                ModLog.Information("Copied CTC progression pricing from {PricingSection} to {SectionId}",
                    ctcPricingSection.identifier, generatedId);
        }

        if (progressionSections.Count != (progression.Sections?.Length ?? 0))
            progression.Sections = progressionSections.ToArray();

        AddSecondMainlineCtcProgressions(progression, progressionSections, existing);
        if (progressionSections.Count != (progression.Sections?.Length ?? 0))
            progression.Sections = progressionSections.ToArray();
    }

    private static void AddSecondMainlineCtcProgressions(Progression progression,
        List<Section> progressionSections, IEnumerable<Section> nativeSections)
    {
        var trackTags = progression.GetComponentsInChildren<DoubleMainlineProgressionTag>(true)
            .Where(tag => !string.IsNullOrWhiteSpace(tag.SourceCtcSectionId) && tag.GetComponent<Section>() != null)
            .ToArray();
        var nativeById = nativeSections.Where(section => section != null)
            .GroupBy(section => section.identifier, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var ctcTagsBySourceId = progression.GetComponentsInChildren<DoubleMainlineCtcProgressionTag>(true)
            .Where(tag => tag.GetComponent<Section>() != null)
            .ToDictionary(tag => tag.GetComponent<Section>()!.identifier, tag => tag, StringComparer.Ordinal);

        foreach (var group in trackTags.GroupBy(tag => tag.SourceCtcSectionId, StringComparer.Ordinal))
        {
            if (!nativeById.TryGetValue(group.Key, out var sourceCtc)) continue;
            var mainlineSections = group.Select(tag => tag.GetComponent<Section>())
                .OfType<Section>()
                .Distinct()
                .ToArray();
            if (mainlineSections.Length == 0) continue;

            if (ctcTagsBySourceId.TryGetValue(sourceCtc.identifier, out var existingTag))
            {
                var existingSection = existingTag.GetComponent<Section>();
                if (existingSection != null)
                    existingSection.prerequisiteSections = new[] { sourceCtc }.Concat(mainlineSections).Distinct().ToArray();
                existingTag.SourceGroupIds = group.SelectMany(tag => tag.SourceGroupIds)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                continue;
            }

            string generatedId = IdFor(sourceCtc.identifier);
            var copyObject = UnityEngine.Object.Instantiate(sourceCtc.gameObject, sourceCtc.transform.parent, false);
            copyObject.name = IdPrefix + "Signals." + generatedId;
            var copy = copyObject.GetComponent<Section>();
            if (copy == null)
            {
                UnityEngine.Object.Destroy(copyObject);
                continue;
            }

            copy.identifier = generatedId;
            copy.displayName = $"{sourceCtc.displayName} — Second Mainline";
            copy.description = "Add CTC signals to the second mainline for this section.";
            copy.deliveryPhases = CloneDeliveryPhases(sourceCtc.deliveryPhases);
            foreach (var phase in copy.deliveryPhases) phase.cost = checked(phase.cost * 2);
            copy.prerequisiteSections = new[] { sourceCtc }.Concat(mainlineSections).Distinct().ToArray();
            copy.enableFeaturesOnUnlock = Array.Empty<MapFeature>();
            copy.enableFeaturesOnAvailable = Array.Empty<MapFeature>();
            copy.disableFeaturesOnUnlock = Array.Empty<MapFeature>();

            var tag = copyObject.AddComponent<DoubleMainlineCtcProgressionTag>();
            tag.ProgressionId = generatedId;
            tag.LegacyProgressionId = LegacyIdFor(progression.identifier, sourceCtc.identifier);
            tag.SourceGroupIds = group.SelectMany(trackTag => trackTag.SourceGroupIds)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            MigrateProgressionState(progression, tag.LegacyProgressionId, generatedId);
            progressionSections.Add(copy);
            ctcTagsBySourceId[sourceCtc.identifier] = tag;
            ModLog.Information("Added second-mainline CTC progression {SectionId} for {SourceSection} ({Groups})",
                generatedId, sourceCtc.identifier, string.Join(",", tag.SourceGroupIds));
        }
    }

    private static Section? FindCtcPricingSection(Progression progression, Section trackSection, string[] groups)
    {
        var townNames = groups.SelectMany(group => GroupRoutes.TryGetValue(group, out var route)
                ? new[] { route.Start, route.End }
                : Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var candidates = progression.GetComponentsInChildren<Section>(true)
            .Where(section => section != trackSection && section.GetComponent<DoubleMainlineProgressionTag>() == null)
            .Select(section => new
            {
                Section = section,
                Text = (section.identifier + " " + section.displayName + " " + section.description).ToLowerInvariant()
            })
            .Where(candidate => candidate.Text.Contains("ctc") || candidate.Text.Contains("signal"))
            .Select(candidate => new
            {
                candidate.Section,
                Score = townNames.Count(town => candidate.Text.Contains(town.Replace("-", " ")) || candidate.Text.Contains(town))
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Section)
            .FirstOrDefault();
        return candidates;
    }

    private static Section.DeliveryPhase[] CloneDeliveryPhases(Section.DeliveryPhase[] phases) =>
        (phases ?? Array.Empty<Section.DeliveryPhase>()).Select(phase => new Section.DeliveryPhase
        {
            cost = phase.cost,
            industryComponent = phase.industryComponent,
            deliveries = (phase.deliveries ?? Array.Empty<Section.Delivery>()).Select(delivery => new Section.Delivery
            {
                carTypeFilter = delivery.carTypeFilter,
                count = delivery.count,
                load = delivery.load,
                direction = delivery.direction
            }).ToArray()
        }).ToArray();

    private static void ApplyCtcCostMultipliers(Progression progression)
    {
        var unlockedGroups = progression.GetComponentsInChildren<DoubleMainlineProgressionTag>(true)
            .Where(tag => tag.GetComponent<Section>()?.Unlocked == true)
            .SelectMany(tag => tag.SourceGroupIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var section in progression.GetComponentsInChildren<Section>(true))
        {
            if (section.GetComponent<DoubleMainlineProgressionTag>() != null ||
                section.GetComponent<DoubleMainlineCtcProgressionTag>() != null) continue;
            var text = (section.identifier + " " + section.displayName + " " + section.description).ToLowerInvariant();
            if (!text.Contains("ctc") && !text.Contains("signal")) continue;

            bool doubleCost = unlockedGroups.Any(group => GroupRoutes.TryGetValue(group, out var route) &&
                (text.Contains(route.Start) || text.Contains(route.End) ||
                 text.Contains(route.Start.Replace("-", " ")) || text.Contains(route.End.Replace("-", " "))));
            if (!doubleCost)
            {
                if (DoubledCtcCosts.Remove(section) && OriginalCtcCosts.TryGetValue(section, out var original))
                {
                    section.deliveryPhases = CloneDeliveryPhases(section.deliveryPhases);
                    for (int i = 0; i < section.deliveryPhases.Length && i < original.Length; i++)
                        section.deliveryPhases[i].cost = original[i];
                }
                continue;
            }

            if (DoubledCtcCosts.Contains(section)) continue;
            var phases = section.deliveryPhases ?? Array.Empty<Section.DeliveryPhase>();
            OriginalCtcCosts[section] = phases.Select(phase => phase.cost).ToArray();
            section.deliveryPhases = CloneDeliveryPhases(phases);
            foreach (var phase in section.deliveryPhases) phase.cost = checked(phase.cost * 2);
            DoubledCtcCosts.Add(section);
            ModLog.Information("Doubled CTC progression requirements for {SectionId} because its second mainline is built", section.identifier);
        }
    }

    internal static void RemoveProgressionLevels(Progression progression)
    {
        if (progression == null) return;
        foreach (var tag in progression.GetComponentsInChildren<DoubleMainlineProgressionTag>(true))
        {
            if (tag != null) UnityEngine.Object.Destroy(tag.gameObject);
        }
        foreach (var tag in progression.GetComponentsInChildren<DoubleMainlineCtcProgressionTag>(true))
        {
            if (tag != null) UnityEngine.Object.Destroy(tag.gameObject);
        }
    }

    internal static void Update()
    {
        if (!DoubleMainlineSettings.Enabled) return;
        var graph = Graph.Shared;
        var progression = Progression.Shared;
        if (graph == null || progression == null || !graph.HasPopulatedCollections) return;

        if (graph != observedGraph)
        {
            observedGraph = graph;
            InvalidateTrackScanCache();
            ParallelNodes.Clear();
            RouteEntrySwitches.Clear();
            SidingCrossoverControls.Clear();
            AddedSegments.Clear();
            AddedCrossovers.Clear();
            ParallelSignalRoutes.Clear();
            DoubleMainlineCtc.Reset();
        }

        if (progression != observedProgression)
        {
            observedProgression = progression;
            InvalidateTrackScanCache();
            ParallelNodes.Clear();
            RouteEntrySwitches.Clear();
            SidingCrossoverControls.Clear();
            AddedSegments.Clear();
            AddedCrossovers.Clear();
            ParallelSignalRoutes.Clear();
            OriginalCtcCosts.Clear();
            DoubledCtcCosts.Clear();
            DoubleMainlineCtc.Reset();
        }

        // Debug commands can hold progression-triggered geometry until the player requests
        // each generated segment. Graph/progression lifecycle tracking above still runs.
        if (generationPaused) return;

        MigratePendingProgressionStates();

        ApplyCtcCostMultipliers(progression);

        bool changed = false;
        foreach (var tag in progression.GetComponentsInChildren<DoubleMainlineProgressionTag>(true))
        {
            var section = tag.GetComponent<Section>();
            if (section == null || !section.Unlocked) continue;

            foreach (var groupId in tag.SourceGroupIds)
            {
                if (!GroupRoutes.TryGetValue(groupId, out var route)) continue;
                if (TryBuildParallelRoute(graph, route.Start, route.End, groupId, out var routeChanged))
                    changed |= routeChanged;
            }
        }

        var crossoverControls = new List<(string Name, TrackNode First, TrackNode Second)>();
        foreach (var crossover in CrossoverRoutes)
        {
            changed |= TryBuildCrossover(graph, crossover.Name, crossover.Start, crossover.End,
                out var firstSwitch, out var secondSwitch);
            if (firstSwitch != null && secondSwitch != null)
                crossoverControls.Add((crossover.Name, firstSwitch, secondSwitch));
        }

        foreach (var control in SidingCrossoverControls.Values.SelectMany(controls => controls))
            crossoverControls.Add(control);

        // Build world meshes before touching optional CTC panel objects. A panel lifecycle
        // exception must not leave graph-only rails visible on the map without 3D track.
        if (changed)
        {
            // Siding connector construction happens before the parallel segments and area
            // endpoint curves are added. Rebuild the graph collections here, after every
            // generated segment exists, so the world track-object builder sees the same
            // connected graph that the map renderer sees.
            graph.RebuildCollections();
            foreach (var control in SidingCrossoverControls.Values.SelectMany(controls => controls))
            {
                int firstDegree = graph.SegmentsConnectedTo(control.First).Count;
                int secondDegree = graph.SegmentsConnectedTo(control.Second).Count;
                ModLog.Information("Final siding connector topology {Name}: first={FirstNode} degree={FirstDegree}; second={SecondNode} degree={SecondDegree}",
                    control.Name, control.First.id, firstDegree, control.Second.id, secondDegree);
            }
            TrackObjectManager.Instance?.Rebuild();
            LogTrackRendererState(graph, "immediate");
            if (TrackObjectManager.Instance != null)
                TrackObjectManager.Instance.StartCoroutine(LogTrackRendererStateAfterDelay(graph));
            ModLog.Information("Updated code-generated second mainline geometry; nodes={NodeCount}, segments={SegmentCount}",
                ParallelNodes.Count, AddedSegments.Count);
        }

        foreach (var entry in RouteEntrySwitches)
        {
            int sourceDegree = graph.SegmentsConnectedTo(entry.Value.Source).Count;
            int parallelDegree = graph.SegmentsConnectedTo(entry.Value.Parallel).Count;
            ModLog.Information("Final second-mainline entry topology {GroupId}: source={SourceNode} degree={SourceDegree} tracks=[{SourceTracks}]; parallel={ParallelNode} degree={ParallelDegree} tracks=[{ParallelTracks}]",
                entry.Key, entry.Value.Source.id, sourceDegree, DescribeConnectedTracks(graph, entry.Value.Source),
                entry.Value.Parallel.id, parallelDegree, DescribeConnectedTracks(graph, entry.Value.Parallel));
            try { DoubleMainlineCtc.AddCrossover("Entry-" + entry.Key, entry.Value.Source, entry.Value.Parallel); }
            catch (Exception exception)
            {
                ModLog.Warning(exception, "Could not add CTC controls for second-mainline entry {GroupId}", entry.Key);
            }
        }

        foreach (var control in crossoverControls)
        {
            try { DoubleMainlineCtc.AddCrossover(control.Name, control.First, control.Second); }
            catch (Exception exception)
            {
                ModLog.Warning(exception, "Could not add CTC controls for second-mainline crossover {Name}", control.Name);
            }
        }

        foreach (var tag in progression.GetComponentsInChildren<DoubleMainlineCtcProgressionTag>(true))
        {
            var section = tag.GetComponent<Section>();
            if (section == null || !section.Unlocked) continue;
            foreach (var groupId in tag.SourceGroupIds)
            {
                if (!ParallelSignalRoutes.TryGetValue(groupId, out var route)) continue;
                DoubleMainlineCtc.Add(groupId, route.Segments, route.SourceStart, route.SourceEnd,
                    route.ParallelStart, route.ParallelEnd);
            }
        }
    }

    private static IEnumerator LogTrackRendererStateAfterDelay(Graph graph)
    {
        yield return new WaitForSeconds(5f);
        LogTrackRendererState(graph, "after 5 seconds");
    }

    private static void LogTrackRendererState(Graph graph, string phase)
    {
        try
        {
            var manager = TrackObjectManager.Instance;
            if (manager == null)
            {
                ModLog.Warning("Second-mainline renderer diagnostic: TrackObjectManager.Instance is null");
                return;
            }

            var generated = graph.Segments.Where(segment => segment.id.StartsWith(IdPrefix, StringComparison.Ordinal)).ToArray();
            var descriptorField = typeof(TrackObjectManager).GetField("_descriptors", BindingFlags.Instance | BindingFlags.NonPublic);
            var descriptors = descriptorField?.GetValue(manager);
            var segmentDescriptors = descriptors?.GetType().GetField("segments", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(descriptors) as System.Collections.IDictionary;
            var descriptorIds = new HashSet<string>(StringComparer.Ordinal);
            if (segmentDescriptors != null)
            {
                foreach (System.Collections.DictionaryEntry entry in segmentDescriptors)
                {
                    var proxy = entry.Value?.GetType().GetField("segment", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetValue(entry.Value);
                    var segment = proxy?.GetType().GetField("Segment", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetValue(proxy) as TrackSegment;
                    if (segment != null && segment.id.StartsWith(IdPrefix, StringComparison.Ordinal))
                        descriptorIds.Add(segment.id);
                }
            }

            var rebuilderField = typeof(TrackObjectManager).GetField("_rebuilder", BindingFlags.Instance | BindingFlags.NonPublic);
            var rebuilder = rebuilderField?.GetValue(manager);
            var entriesField = rebuilder?.GetType().GetField("_entryLookup", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = entriesField?.GetValue(rebuilder) as System.Collections.IDictionary;
            int buildQueueCount = GetQueueCount(rebuilder, "_buildQueue");
            int destroyQueueCount = GetQueueCount(rebuilder, "_destroyQueue");
            var entryCount = 0;
            var inRangeCount = 0;
            var visibleCount = 0;
            var builtObjectCount = 0;
            var objectSamples = new List<string>();
            if (entries != null)
            {
                foreach (System.Collections.DictionaryEntry entry in entries)
                {
                    var descriptor = entry.Value?.GetType().GetField("descriptor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetValue(entry.Value);
                    var identifier = descriptor?.GetType().GetProperty("Identifier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetValue(descriptor) as string;
                    if (identifier == null || !descriptorIds.Any(id => identifier.IndexOf(id, StringComparison.Ordinal) >= 0)) continue;
                    entryCount++;
                    var entryType = entry.Value!.GetType();
                    bool inRange = (bool)(entryType.GetField("isInRange", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(entry.Value) ?? false);
                    bool visible = (bool)(entryType.GetField("isVisible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(entry.Value) ?? false);
                    bool built = entryType.GetField("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(entry.Value) != null;
                    if (inRange) inRangeCount++;
                    if (visible) visibleCount++;
                    if (built) builtObjectCount++;
                    if (built && objectSamples.Count < 3)
                    {
                        var gameObject = entryType.GetField("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?.GetValue(entry.Value) as GameObject;
                        if (gameObject != null)
                        {
                            var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
                            var mesh = gameObject.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault()?.sharedMesh;
                            objectSamples.Add($"{identifier}: active={gameObject.activeSelf}/{gameObject.activeInHierarchy}, pos={gameObject.transform.position}, local={gameObject.transform.localPosition}, renderers={renderers.Length}, enabled={renderers.Count(renderer => renderer.enabled)}, vertices={mesh?.vertexCount ?? -1}, meshBounds={mesh?.bounds.ToString() ?? "none"}, rendererBounds={(renderers.Length > 0 ? renderers[0].bounds.ToString() : "none")}");
                        }
                    }
                }
            }

            int activeCount = generated.Count(segment => segment.isActiveAndEnabled);
            int availableCount = generated.Count(segment => segment.Available);
            int enabledCount = generated.Count(segment => segment.GroupEnabled);
            int invisibleCount = generated.Count(segment => segment.IsInvisible);
            var camera = Camera.main;
            var worldOffset = typeof(WorldTransformer).GetField("_currentOffset", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            var builder = typeof(TrackObjectManager).GetField("_builder", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(manager);
            var builderParent = builder?.GetType().GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(builder) as GameObject;
            var terrainSamples = generated.OrderBy(segment =>
            {
                var midpoint = (segment.a.transform.position + segment.b.transform.position) * 0.5f;
                return camera == null ? 0f : (midpoint - camera.transform.position).sqrMagnitude;
            }).Take(12).Select(segment =>
            {
                var a = segment.a.transform.position;
                var b = segment.b.transform.position;
                var midpoint = (a + b) * 0.5f;
                var hits = Physics.RaycastAll(midpoint + Vector3.up * 100f, Vector3.down, 300f,
                        ~0, QueryTriggerInteraction.Ignore)
                    .OrderBy(hit => hit.distance)
                    .Take(4)
                    .Select(hit => $"{hit.collider.name}[{hit.collider.GetType().Name},layer={hit.collider.gameObject.layer},track={hit.collider.GetComponentInParent<TrackSegment>() != null}]@y={hit.point.y:F2}");
                return $"{segment.id}: aY={a.y:F2}, bY={b.y:F2}, midY={midpoint.y:F2}, cameraDist={(camera == null ? -1f : Vector3.Distance(midpoint, camera.transform.position)):F1}, hits=[{string.Join(",", hits)}]";
            }).ToArray();
            var junctions = generated.SelectMany(segment => new[] { segment.a, segment.b })
                .Where(node => node != null)
                .Distinct()
                .Select(node => (Node: node, Links: graph.SegmentsConnectedTo(node).ToArray()))
                .Where(item => item.Links.Length != 2 || item.Links.Any(link =>
                    link.id.StartsWith(IdPrefix, StringComparison.Ordinal) !=
                    item.Links[0].id.StartsWith(IdPrefix, StringComparison.Ordinal)))
                .Take(40)
                .Select(item =>
                {
                    var node = item.Node;
                    var measurements = item.Links.Select(segment =>
                    {
                        var other = segment.a == node ? segment.b : segment.a;
                        var delta = other.transform.position - node.transform.position;
                        float horizontal = new Vector2(delta.x, delta.z).magnitude;
                        float gradePercent = horizontal > 0.01f ? delta.y / horizontal * 100f : 0f;
                        string text = $"{segment.id}[a={PrecisePosition(segment.a.transform.position)},b={PrecisePosition(segment.b.transform.position)},outGrade={gradePercent:F3}%]";
                        return (Text: text, Grade: gradePercent);
                    }).ToArray();
                    float gradeSpread = measurements.Length > 1
                        ? measurements.Max(measurement => measurement.Grade) - measurements.Min(measurement => measurement.Grade)
                        : 0f;
                    return $"{node.id}@world={PrecisePosition(node.transform.position)},local={PrecisePosition(node.transform.localPosition)},degree={item.Links.Length},gradeSpread={gradeSpread:F3}pp,links=[{string.Join(";", measurements.Select(measurement => measurement.Text))}]";
                }).ToArray();
            ModLog.Information("Second-mainline renderer diagnostic ({Phase}): generated={Generated}, active={Active}, available={Available}, enabled={Enabled}, invisible={Invisible}, descriptors={Descriptors}, rebuilderEntries={Entries}, inRange={InRange}, visible={Visible}, builtObjects={BuiltObjects}, buildQueue={BuildQueue}, destroyQueue={DestroyQueue}, worldOffset={WorldOffset}, builderParent={BuilderParent}, camera={Camera}; objects={Objects}; sample={Sample}",
                phase, generated.Length, activeCount, availableCount, enabledCount, invisibleCount, descriptorIds.Count, entryCount,
                inRangeCount, visibleCount, builtObjectCount, buildQueueCount, destroyQueueCount, worldOffset,
                builderParent == null ? "none" : $"pos={builderParent.transform.position}, scale={builderParent.transform.lossyScale}",
                camera == null ? "none" : $"pos={camera.transform.position}, forward={camera.transform.forward}",
                string.Join(" || ", objectSamples),
                string.Join(" | ", generated.Take(4).Select(segment =>
                    $"{segment.id} len={segment.GetLength():F1}m a={segment.a.transform.localPosition} b={segment.b.transform.localPosition}")));
            ModLog.Information("Second-mainline elevation samples ({Phase}): {Samples}", phase, string.Join(" | ", terrainSamples));
            ModLog.Information("Second-mainline junction coordinate audit ({Phase}): {Junctions}", phase, string.Join(" || ", junctions));
        }
        catch (Exception exception)
        {
            ModLog.Warning(exception, "Second-mainline renderer diagnostic failed");
        }
    }

    private static int GetQueueCount(object? rebuilder, string fieldName)
    {
        var queue = rebuilder?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(rebuilder);
        return queue is ICollection collection ? collection.Count : -1;
    }

    private static string PrecisePosition(Vector3 position) =>
        $"({position.x:F3},{position.y:F3},{position.z:F3})";

    internal static string SetRendererVisibilityOverride(bool forceVisible)
    {
        if (!DoubleMainlineSettings.Enabled)
            return "Experimental second-mainline progressions are disabled in mod settings.";
        DoubleMainlineSettings.ForceGeneratedTrackVisible = forceVisible;
        if (!forceVisible)
        {
            TrackObjectManager.Instance?.Rebuild();
            ModLog.Information("Disabled forced second-mainline visibility and rebuilt track culling");
            return "Forced second-mainline visibility off; track culling rebuilt.";
        }

        var manager = TrackObjectManager.Instance;
        var rebuilder = manager == null
            ? null
            : typeof(TrackObjectManager).GetField("_rebuilder", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(manager);
        var entries = rebuilder?.GetType().GetField("_entryLookup", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(rebuilder) as System.Collections.IDictionary;
        int matched = 0;
        int activated = 0;
        if (entries != null)
        {
            foreach (System.Collections.DictionaryEntry pair in entries)
            {
                if (pair.Value == null) continue;
                var entryType = pair.Value.GetType();
                var descriptor = entryType.GetField("descriptor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(pair.Value);
                var identifier = descriptor?.GetType().GetProperty("Identifier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(descriptor) as string;
                if (identifier == null || identifier.IndexOf(IdPrefix, StringComparison.Ordinal) < 0) continue;
                matched++;
                ForceRendererEntryVisible(pair.Value);
                var gameObject = entryType.GetField("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(pair.Value) as GameObject;
                if (gameObject != null && gameObject.activeSelf) activated++;
            }
        }

        ModLog.Information("Forced second-mainline renderer visibility for {Activated} built entries; matched {Matched} entries",
            activated, matched);
        if (manager != null) LogTrackRendererState(manager.Graph, "after force command");
        return $"Forced second-mainline visibility for {activated} built track objects ({matched} renderer entries).";
    }

    internal static string PauseGeneration()
    {
        if (!DoubleMainlineSettings.Enabled)
            return "Experimental second-mainline progressions are disabled in mod settings.";
        generationPaused = true;
        ModLog.Information("Paused automatic second-mainline generation from console");
        return "Automatic second-mainline generation paused. Use /dmgen step <group> or /dmgen resume.";
    }

    internal static string BeginSegmentStepping(string groupId)
    {
        if (!DoubleMainlineSettings.Enabled)
            return "Experimental second-mainline progressions are disabled in mod settings.";
        if (string.IsNullOrWhiteSpace(groupId) || !GroupRoutes.TryGetValue(groupId, out var routeAreas))
            return "Usage: /dmgen step <group>; valid groups: " + string.Join(",", GroupRoutes.Keys.OrderBy(id => id));

        var graph = Graph.Shared;
        var progression = Progression.Shared;
        if (graph == null || progression == null || !graph.HasPopulatedCollections)
            return "Track graph or progression is not ready.";

        bool unlocked = progression.GetComponentsInChildren<DoubleMainlineProgressionTag>(true).Any(tag =>
        {
            var section = tag.GetComponent<Section>();
            return section != null && section.Unlocked &&
                tag.SourceGroupIds.Any(source => string.Equals(source, groupId, StringComparison.OrdinalIgnoreCase));
        });
        if (!unlocked)
            return $"Second-mainline progression for {groupId} is not unlocked yet.";

        generationPaused = true;
        stepwiseGroupId = null;
        stepwiseSegments.Clear();
        stepwiseIndex = 0;

        if (!ParallelSignalRoutes.TryGetValue(groupId, out var signalRoute) || signalRoute.Segments.Count == 0)
        {
            TryBuildParallelRoute(graph, routeAreas.Start, routeAreas.End, groupId, out _);
            if (!ParallelSignalRoutes.TryGetValue(groupId, out signalRoute) || signalRoute.Segments.Count == 0)
                return $"Could not prepare generated track for {groupId}; see TweaksAndThings-debug.log.";
        }

        var generated = signalRoute.Segments
            .Where(segment => segment != null && segment.id.StartsWith(IdPrefix, StringComparison.Ordinal))
            .GroupBy(segment => segment.id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        stepwiseSegments = OrderStepSegments(graph, groupId, routeAreas, generated);
        if (stepwiseSegments.Count == 0)
            return $"No generated track segments were found for {groupId}.";

        foreach (var segment in stepwiseSegments)
        {
            segment.Available = false;
            segment.GroupEnabled = false;
        }
        stepwiseGroupId = groupId;
        graph.RebuildCollections();
        TrackObjectManager.Instance?.Rebuild();
        ModLog.Information("Prepared segment-by-segment second-mainline build for {GroupId}: hidden generated segments={Count}; order=[{Segments}]",
            groupId, stepwiseSegments.Count, string.Join(",", stepwiseSegments.Select(segment => segment.id)));
        return $"Prepared {stepwiseSegments.Count} generated segments for {groupId}. Track is paused; use /dmgen next, /dmgen status, or /dmgen resume.";
    }

    private static List<TrackSegment> OrderStepSegments(Graph graph, string groupId,
        (string Start, string End) routeAreas, IReadOnlyList<TrackSegment> generated)
    {
        var remaining = generated.ToDictionary(segment => segment.id, StringComparer.Ordinal);
        var ordered = new List<TrackSegment>(generated.Count);
        void AddWhere(Func<TrackSegment, bool> predicate, IEnumerable<string>? preferredIds = null)
        {
            if (preferredIds != null)
            {
                foreach (var id in preferredIds)
                    if (remaining.TryGetValue(id, out var segment) && predicate(segment))
                    {
                        ordered.Add(segment);
                        remaining.Remove(id);
                    }
            }
            foreach (var segment in remaining.Values.Where(predicate).OrderBy(segment => segment.id, StringComparer.Ordinal).ToArray())
            {
                ordered.Add(segment);
                remaining.Remove(segment.id);
            }
        }

        string entryPrefix = IdPrefix + "Entry." + groupId + ".";
        AddWhere(segment => segment.id.StartsWith(entryPrefix, StringComparison.Ordinal),
            Enumerable.Range(1, DoubleMainlineSettings.CrossoverCurveSamples)
                .Select(sample => entryPrefix + sample));

        IEnumerable<string>? routeSegmentIds = null;
        if (TryShortestMainlinePath(graph, routeAreas.Start, routeAreas.End, out var route))
            routeSegmentIds = route.Segments.Select(segment => IdPrefix + "Segment." + segment.id);
        AddWhere(segment => segment.id.StartsWith(IdPrefix + "Segment.", StringComparison.Ordinal), routeSegmentIds);

        var sidingPrefixForGroup = IdPrefix + groupId + ".Siding.";
        AddWhere(segment => segment.id.StartsWith(sidingPrefixForGroup, StringComparison.Ordinal));

        string exitPrefix = IdPrefix + "Exit." + groupId + ".";
        AddWhere(segment => segment.id.StartsWith(exitPrefix, StringComparison.Ordinal),
            Enumerable.Range(1, DoubleMainlineSettings.CrossoverCurveSamples)
                .Select(sample => exitPrefix + sample));

        foreach (var segment in remaining.Values.OrderBy(segment => segment.id, StringComparer.Ordinal))
            ordered.Add(segment);
        return ordered;
    }

    internal static string StepNextGeneratedSegment()
    {
        if (!DoubleMainlineSettings.Enabled)
            return "Experimental second-mainline progressions are disabled in mod settings.";
        if (stepwiseGroupId == null || stepwiseSegments.Count == 0)
            return "No segment-by-segment build is active. Use /dmgen step <group>.";
        if (stepwiseIndex >= stepwiseSegments.Count)
            return $"All {stepwiseSegments.Count} generated segments for {stepwiseGroupId} are enabled. Use /dmgen resume.";

        var segment = stepwiseSegments[stepwiseIndex++];
        segment.Available = true;
        segment.GroupEnabled = true;
        var graph = Graph.Shared;
        graph?.RebuildCollections();
        TrackObjectManager.Instance?.Rebuild();
        ModLog.Information("Segment step {Index}/{Count} enabled {SegmentId}: a={A}, b={B}",
            stepwiseIndex, stepwiseSegments.Count, segment.id,
            FormatPosition(segment.a), FormatPosition(segment.b));
        return $"Enabled {stepwiseIndex}/{stepwiseSegments.Count}: {segment.id}";
    }

    internal static string SegmentSteppingStatus() => stepwiseGroupId == null
        ? $"Automatic second-mainline generation is {(generationPaused ? "paused" : "running")}; no segment-step session is active."
        : $"Segment-step session {stepwiseGroupId}: {stepwiseIndex}/{stepwiseSegments.Count} enabled; automatic generation paused.";

    internal static string ResumeGeneration()
    {
        if (!DoubleMainlineSettings.Enabled)
            return "Experimental second-mainline progressions are disabled in mod settings.";
        foreach (var segment in stepwiseSegments)
        {
            segment.Available = true;
            segment.GroupEnabled = true;
        }
        stepwiseSegments.Clear();
        stepwiseGroupId = null;
        stepwiseIndex = 0;
        generationPaused = false;
        var graph = Graph.Shared;
        graph?.RebuildCollections();
        TrackObjectManager.Instance?.Rebuild();
        ModLog.Information("Resumed automatic second-mainline generation from console");
        return "All prepared segments enabled; automatic second-mainline generation resumed.";
    }

    internal static void ForceRendererEntryVisible(object entry)
    {
        if (!DoubleMainlineSettings.ForceGeneratedTrackVisible || entry == null) return;
        var entryType = entry.GetType();
        var descriptor = entryType.GetField("descriptor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(entry);
        var identifier = descriptor?.GetType().GetProperty("Identifier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(descriptor) as string;
        if (identifier == null || identifier.IndexOf(IdPrefix, StringComparison.Ordinal) < 0) return;
        bool inRange = (bool)(entryType.GetField("isInRange", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(entry) ?? false);
        if (!inRange) return;
        entryType.GetField("isVisible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(entry, true);
        (entryType.GetField("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(entry) as GameObject)?.SetActive(true);
    }

    private static string[] SourceTrackGroups(Section section)
    {
        var features = (section.enableFeaturesOnUnlock ?? Array.Empty<MapFeature>())
            .Concat(section.enableFeaturesOnAvailable ?? Array.Empty<MapFeature>())
            .Where(feature => feature != null);
        return features
            .SelectMany(feature => feature.trackGroupsEnableOnUnlock ?? Array.Empty<string>())
            .Concat(features.SelectMany(feature => feature.trackGroupsAvailableOnUnlock ?? Array.Empty<string>()))
            .Where(group => MainlineGroups.Contains(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string IdFor(string sectionId)
    {
        var safeSection = new string((sectionId ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        return safeSection + ".tnt";
    }

    private static string LegacyIdFor(string progressionId, string sectionId)
    {
        var safeProgression = new string((progressionId ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        var safeSection = new string((sectionId ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        return safeProgression + "." + safeSection + ".SecondMainline";
    }

    private static void MigrateProgressionState(Progression progression, string legacyId, string currentId)
    {
        if (string.IsNullOrEmpty(legacyId) || string.Equals(legacyId, currentId, StringComparison.Ordinal)) return;
        string migrationKey = progression.GetInstanceID() + ":" + legacyId + "->" + currentId;
        if (CompletedProgressionMigrations.Contains(migrationKey)) return;
        if (ProgressionKeyValueObject?.GetValue(progression) == null)
        {
            PendingProgressionMigrations[migrationKey] = (progression, legacyId, currentId);
            return;
        }

        try
        {
            // Preserve the unlock first. Counter access can fail if the save does not contain
            // old-format keys, but that must not prevent the new milestone from being available.
            var unlocked = progression.UnlockedSectionIds?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            if (unlocked.Contains(legacyId) && unlocked.Add(currentId)) progression.UnlockedSectionIds = unlocked;

            int legacyPaid = (int)(GetPaidDeliveries?.Invoke(progression, new object[] { legacyId }) ?? 0);
            int legacyFulfilled = (int)(GetFulfilledDeliveries?.Invoke(progression, new object[] { legacyId }) ?? 0);
            int currentPaid = (int)(GetPaidDeliveries?.Invoke(progression, new object[] { currentId }) ?? 0);
            int currentFulfilled = (int)(GetFulfilledDeliveries?.Invoke(progression, new object[] { currentId }) ?? 0);

            if (legacyPaid > currentPaid) SetPaidDeliveries?.Invoke(progression, new object[] { currentId, legacyPaid });
            if (legacyFulfilled > currentFulfilled) SetFulfilledDeliveries?.Invoke(progression, new object[] { currentId, legacyFulfilled });

            if (legacyPaid > 0 || legacyFulfilled > 0 || unlocked.Contains(currentId))
                ModLog.Information("Migrated second-mainline progression state {LegacyId} to {CurrentId}", legacyId, currentId);
            CompletedProgressionMigrations.Add(migrationKey);
            PendingProgressionMigrations.Remove(migrationKey);
        }
        catch (Exception exception)
        {
            ModLog.Warning(exception, "Could not migrate second-mainline progression state {LegacyId} to {CurrentId}", legacyId, currentId);
            CompletedProgressionMigrations.Add(migrationKey);
            PendingProgressionMigrations.Remove(migrationKey);
        }
    }

    private static void MigratePendingProgressionStates()
    {
        foreach (var migration in PendingProgressionMigrations.Values.ToArray())
        {
            if (migration.Progression == null || ProgressionKeyValueObject?.GetValue(migration.Progression) == null) continue;
            MigrateProgressionState(migration.Progression, migration.LegacyId, migration.CurrentId);
        }
    }

    private static bool TryBuildParallelRoute(Graph graph, string startAreaId, string endAreaId, string groupId,
        out bool changed)
    {
        changed = false;
        int snapRevisionAtStart = parallelNodeSnapRevision;
        if (ParallelSignalRoutes.TryGetValue(groupId, out var cachedRoute) && cachedRoute.Segments.Count > 0 &&
            cachedRoute.Segments.All(segment => segment != null && graph.GetSegment(segment.id) == segment)) return false;
        if (!TryShortestMainlinePath(graph, startAreaId, endAreaId, out var route)) return false;

        // Switch 47 is inside the Bryson-Wittier native route rather than before its first
        // segment. Start the new route at that switch: requiring a path from it to include the
        // route's earlier segment incorrectly asks the line to run back through Bryson.
        if (string.Equals(groupId, "s2", StringComparison.OrdinalIgnoreCase))
        {
            var brysonSwitchIds = DoubleMainlineCtc.PanelSwitchNodes("47")
                .Select(node => node.id).ToHashSet(StringComparer.Ordinal);
            int anchorIndex = route.Nodes.ToList().FindIndex(node => brysonSwitchIds.Contains(node.id));
            if (anchorIndex > 0)
            {
                ModLog.Information("Anchored second-mainline route {GroupId} at Bryson panel switch 47 node {NodeId}, trimming {TrimmedSegments} preceding native segment(s)",
                    groupId, route.Nodes[anchorIndex].id, anchorIndex);
                route = new RoutePath(route.Nodes.Skip(anchorIndex).ToArray(), route.Segments.Skip(anchorIndex).ToArray());
            }
        }

        var sourceSegments = route.Segments
            .Where(segment => string.Equals(segment.groupId, groupId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (sourceSegments.Length == 0) return false;

        int firstSourceIndex = -1;
        int lastSourceIndex = -1;
        for (int i = 0; i < route.Segments.Count; i++)
        {
            if (!sourceSegments.Contains(route.Segments[i])) continue;
            if (firstSourceIndex < 0) firstSourceIndex = i;
            lastSourceIndex = i;
        }
        if (string.Equals(groupId, "s2", StringComparison.OrdinalIgnoreCase) ||
            (firstSourceIndex == 0 && !graph.IsSwitch(route.Nodes[0])))
        {
            var target = route.Nodes[Math.Min(lastSourceIndex + 1, route.Nodes.Count - 1)];
            bool anchored = false;
            var candidates = string.Equals(groupId, "s2", StringComparison.OrdinalIgnoreCase)
                ? DoubleMainlineCtc.PanelSwitchNodes("47")
                : graph.Nodes
                    .Where(node => graph.IsSwitch(node) &&
                        Vector3.Distance(node.transform.localPosition, route.Nodes[0].transform.localPosition) <=
                        DoubleMainlineSettings.EntrySwitchSearchDistance)
                    .OrderByDescending(node => node.IsCTCSwitch)
                    .ThenBy(node => (node.transform.localPosition - route.Nodes[0].transform.localPosition).sqrMagnitude)
                    .ToArray();
            ModLog.Information("Second-mainline entry target for {GroupId}: required panel switch={RequiredSwitch}, route-start={RouteStart}, candidate nodes={Candidates}",
                groupId, string.Equals(groupId, "s2", StringComparison.OrdinalIgnoreCase) ? "47" : "none",
                route.Nodes[0].id, string.Join(",", candidates.Select(node => node.id)));
            foreach (var candidate in candidates)
            {
                if (string.Equals(groupId, "s2", StringComparison.OrdinalIgnoreCase) &&
                    route.Nodes.Count > 0 && candidate.id == route.Nodes[0].id)
                {
                    anchored = true;
                    ModLog.Information("Second-mainline route {GroupId} already begins at required panel switch 47 node {NodeId}; using native route to {TargetNode}",
                        groupId, candidate.id, target.id);
                    break;
                }
                if (!TryShortestMainlinePath(graph, candidate, target, out var anchoredRoute) ||
                    !anchoredRoute.Segments.Contains(sourceSegments[0])) continue;
                route = anchoredRoute;
                anchored = true;
                ModLog.Information("Extended second-mainline route {GroupId} to entry switch {NodeId} at {Position}; attached tracks: {ConnectedTracks}",
                    groupId, candidate.id, FormatPosition(candidate), DescribeConnectedTracks(graph, candidate));
                break;
            }
            if (!anchored)
            {
                ModLog.Error("Could not route second-mainline entry {GroupId} from required switch {RequiredSwitch} through source segment {SourceSegment} to {TargetNode}; skipping disconnected geometry. Candidate details: {Candidates}",
                    groupId, string.Equals(groupId, "s2", StringComparison.OrdinalIgnoreCase) ? "47" : "nearest existing switch",
                    sourceSegments[0].id, target.id,
                    string.Join(" || ", candidates.Select(node => node.id + " @ " + FormatPosition(node) + " tracks=" + DescribeConnectedTracks(graph, node))));
                return false;
            }

            firstSourceIndex = -1;
            lastSourceIndex = -1;
            for (int i = 0; i < route.Segments.Count; i++)
            {
                if (!sourceSegments.Contains(route.Segments[i])) continue;
                if (firstSourceIndex < 0) firstSourceIndex = i;
                lastSourceIndex = i;
            }
            if (firstSourceIndex < 0) return false;
        }
        ModLog.Information("Second-mainline route {GroupId}: nodes={NodeCount}, source indexes={FirstSource}-{LastSource}, start={StartNode}, end={EndNode}",
            groupId, route.Nodes.Count, firstSourceIndex, lastSourceIndex, route.Nodes[0].id, route.Nodes[route.Nodes.Count - 1].id);
        var sidingCandidates = FindPassingSidingDetours(graph, route, groupId, firstSourceIndex, lastSourceIndex);
        var offsets = OffsetDirections(route.Nodes);
        float? sidingSide = SidingSidePreference(route, sidingCandidates, offsets);
        float preferredSide = ChooseClearSide(graph, route, offsets, sidingSide);
        var parallelSegments = new List<TrackSegment>();

        int entryStartIndex = FindEntryStartIndex(graph, route, firstSourceIndex, groupId);
        // Route groups can meet at the same town approach. The shared native node and twin
        // track segments already carry the second mainline through that boundary, so a second
        // group must extend the line instead of building another ramp from the same switch.
        bool entryAlreadyBuilt = RouteEntrySwitches.Values.Any(entry =>
            entry.Source.id == route.Nodes[entryStartIndex].id ||
            Vector3.Distance(entry.Source.transform.localPosition,
                route.Nodes[entryStartIndex].transform.localPosition) <= MainlineEndpointTransitionLength);
        if (entryAlreadyBuilt)
            ModLog.Information("Reusing existing second-mainline entry at native node {EntryNode} for route group {GroupId}; suppressing duplicate entry ramp",
                route.Nodes[entryStartIndex].id, groupId);
        int preliminaryEntryEndIndex = entryAlreadyBuilt
            ? entryStartIndex
            : FindEntryEndIndex(route, entryStartIndex, firstSourceIndex, lastSourceIndex,
                Array.Empty<PassingSidingDetour>());
        int exitEndIndex = FindSafeExitEndIndex(graph, route, firstSourceIndex, lastSourceIndex);
        int preliminaryExitStartIndex = FindExitStartIndex(route, preliminaryEntryEndIndex,
            exitEndIndex, Array.Empty<PassingSidingDetour>());
        var sidingDetours = BuildSidingExtensions(graph, route, groupId, sidingCandidates, offsets,
            preferredSide, preliminaryEntryEndIndex, preliminaryExitStartIndex, firstSourceIndex,
            lastSourceIndex, out var sidingChanged);
        changed |= sidingChanged;
        int entryEndIndex = entryAlreadyBuilt
            ? entryStartIndex
            : FindEntryEndIndex(route, entryStartIndex, firstSourceIndex, lastSourceIndex, sidingDetours);
        exitEndIndex = FindSafeExitEndIndex(graph, route, firstSourceIndex, lastSourceIndex);
        int exitStartIndex = FindExitStartIndex(route, entryEndIndex, exitEndIndex, sidingDetours);
        if (entryEndIndex > entryStartIndex)
        {
            bool entryMissing = graph.GetSegment(IdPrefix + "Entry." + groupId + ".1") == null;
            var entrySegments = BuildGentleRouteEntry(graph, route, groupId, entryStartIndex,
                entryEndIndex, offsets, preferredSide);
            parallelSegments.AddRange(entrySegments);
            RouteEntrySwitches[groupId] = (route.Nodes[entryStartIndex], ParallelNodes[route.Nodes[entryEndIndex].id]);
            ModLog.Information("Second-mainline entry {GroupId}: existing switch {SwitchNode} @ {SwitchPosition} -> parallel node {ParallelNode} @ {ParallelPosition}; curve samples={CurveSamples}",
                groupId, route.Nodes[entryStartIndex].id, FormatPosition(route.Nodes[entryStartIndex]),
                ParallelNodes[route.Nodes[entryEndIndex].id].id, FormatPosition(ParallelNodes[route.Nodes[entryEndIndex].id]),
                entrySegments.Count);
            changed |= entryMissing;
        }
        else
        {
            if (!entryAlreadyBuilt)
                ModLog.Warning("Second-mainline entry {GroupId} has no gentle merge: entry switch index={EntryIndex}, first source={SourceIndex}, last source={LastSourceIndex}",
                    groupId, entryStartIndex, firstSourceIndex, lastSourceIndex);
        }

        IReadOnlyList<TrackSegment> exitSegments = Array.Empty<TrackSegment>();
        if (exitStartIndex > entryEndIndex && exitEndIndex > exitStartIndex)
        {
            bool exitMissing = graph.GetSegment(IdPrefix + "Exit." + groupId + ".1") == null;
            exitSegments = BuildGentleRouteExit(graph, route, groupId, exitStartIndex,
                exitEndIndex, offsets, preferredSide);
            parallelSegments.AddRange(exitSegments);
            changed |= exitMissing;
            ModLog.Information("Second-mainline exit {GroupId}: parallel node {ParallelNode} @ {ParallelPosition} -> native merge {NativeNode} @ {NativePosition}; curve samples={CurveSamples}",
                groupId, ParallelNodes[route.Nodes[exitStartIndex].id].id,
                FormatPosition(ParallelNodes[route.Nodes[exitStartIndex].id]), route.Nodes[exitEndIndex].id,
                FormatPosition(route.Nodes[exitEndIndex]), exitSegments.Count);
        }
        else
        {
            ModLog.Warning("Second-mainline exit {GroupId} has no gentle merge: entry end={EntryEnd}, exit start={ExitStart}, exit end={ExitEnd}",
                groupId, entryEndIndex, exitStartIndex, exitEndIndex);
        }

        // A progression group can start or end inside the shortest town-to-town route.
        // Keep the generated route continuous between its two gentle connections to the
        // native middle track, ending it before the far merge to avoid a parallel stub.
        int parallelStartIndex = entryEndIndex > entryStartIndex ? entryEndIndex : firstSourceIndex;
        int parallelEndIndex = exitStartIndex > parallelStartIndex ? exitStartIndex : lastSourceIndex + 1;
        for (int i = parallelStartIndex; i < parallelEndIndex && i < route.Segments.Count; i++)
        {
            if (sidingDetours.Any(siding => i >= siding.ParallelStartIndex && i < siding.ParallelEndIndex))
            {
                // The through route is the existing passing siding in this interval. Hide
                // any parallel segment left by an earlier build so the two routes cannot
                // cross or occupy the same narrow right-of-way beside the siding.
                var obsoleteParallel = graph.GetSegment(IdPrefix + "Segment." + route.Segments[i].id);
                if (obsoleteParallel != null)
                {
                    changed |= obsoleteParallel.Available || obsoleteParallel.GroupEnabled;
                    obsoleteParallel.Available = false;
                    obsoleteParallel.GroupEnabled = false;
                }
                continue;
            }

            var sourceSegment = route.Segments[i];

            var sourceA = route.Nodes[i];
            var sourceB = route.Nodes[i + 1];
            var twinA = ParallelNode(graph, sourceA, offsets[i] * preferredSide, route, i);
            var twinB = ParallelNode(graph, sourceB, offsets[i + 1] * preferredSide, route, i + 1);
            var segmentId = IdPrefix + "Segment." + sourceSegment.id;
            var twinSegment = graph.GetSegment(segmentId);
            if (twinSegment == null)
            {
                if (!AddedSegments.Add(segmentId)) continue;
                twinSegment = graph.AddSegment(segmentId, twinA, twinB);
                twinSegment.style = sourceSegment.style;
                twinSegment.trackClass = TrackClass.Mainline;
                twinSegment.priority = sourceSegment.priority;
                twinSegment.speedLimit = sourceSegment.speedLimit;
                twinSegment.groupId = string.Empty;
                twinSegment.transform.SetParent(graph.transform, true);
                changed = true;
            }
            // Graph-only map rendering can show a segment even when the world renderer
            // excludes it. These generated segments have no native group to enable them,
            // so opt them into track-object generation explicitly.
            twinSegment.GroupEnabled = true;
            twinSegment.Available = true;
            if (!parallelSegments.Contains(twinSegment)) parallelSegments.Add(twinSegment);
        }
        foreach (var siding in sidingDetours)
        {
            foreach (var segment in siding.Segments.Concat(siding.EntryCrossoverSegments).Concat(siding.ExitCrossoverSegments))
                if (!parallelSegments.Contains(segment)) parallelSegments.Add(segment);
        }
        if (parallelSegments.Count > 0 && firstSourceIndex >= 0 && lastSourceIndex >= firstSourceIndex)
        {
            int signalStartIndex = entryEndIndex > entryStartIndex ? entryEndIndex : firstSourceIndex;
            var startSource = route.Nodes[signalStartIndex];
            var endSource = route.Nodes[exitEndIndex];
            var parallelStart = ParallelNodes.TryGetValue(route.Nodes[signalStartIndex].id, out var generatedStart) ? generatedStart : startSource;
            var parallelEnd = ParallelNodes.TryGetValue(route.Nodes[parallelEndIndex].id, out var generatedEnd) ? generatedEnd : endSource;
            ParallelSignalRoutes[groupId] = (parallelSegments, startSource, endSource, parallelStart, parallelEnd);
            ModLog.Information("Second-mainline endpoints for {GroupId}: start native={StartNative} @ {StartPosition}, parallel={StartParallel} @ {StartParallelPosition}; end native={EndNative} @ {EndPosition}, parallel={EndParallel} @ {EndParallelPosition}; native links start=[{StartLinks}] end=[{EndLinks}]",
                groupId, startSource.id, FormatPosition(startSource), parallelStart.id, FormatPosition(parallelStart),
                endSource.id, FormatPosition(endSource), parallelEnd.id, FormatPosition(parallelEnd),
                DescribeConnectedTracks(graph, startSource), DescribeConnectedTracks(graph, endSource));
            DoubleMainlineCtc.AddUnsignaled(groupId, parallelSegments);
        }
        if (sidingDetours.Count > 0)
            ModLog.Information("Second mainline {GroupId} extends through {SidingCount} existing passing siding(s) with two Z connectors per siding; native turnout nodes are preserved",
                groupId, sidingDetours.Count);
        if (parallelNodeSnapRevision != snapRevisionAtStart)
        {
            foreach (var segment in parallelSegments)
            {
                segment.InvalidateCurve();
                segment.RebuildBezier();
            }
            changed = true;
        }
        return changed || sourceSegments.Length > 0;
    }

    private static string FormatPosition(TrackNode node)
    {
        var position = node.transform.localPosition;
        return $"({position.x:F0},{position.y:F0},{position.z:F0})";
    }

    private static string DescribeConnectedTracks(Graph graph, TrackNode node) =>
        string.Join(";", graph.SegmentsConnectedTo(node)
            .Select(segment => $"{segment.id}[{segment.trackClass},group={segment.groupId ?? "-"},available={segment.Available},other={segment.GetOtherNode(node).id}]")
            .ToArray());

    private static int FindEntryStartIndex(Graph graph, RoutePath route, int firstSourceIndex, string groupId)
    {
        if (firstSourceIndex < 0 || firstSourceIndex >= route.Nodes.Count) return firstSourceIndex;

        // Switch 47 already has three native legs. Attaching the new entry curve directly to
        // that node turns the native turnout into a four-way junction and hides its switch
        // geometry. Keep the CTC turnout intact and form the new junction on the first ordinary
        // mainline node just beyond it.
        if (string.Equals(groupId, "s2", StringComparison.OrdinalIgnoreCase) && firstSourceIndex == 0 &&
            route.Nodes.Count > 1 && graph.IsSwitch(route.Nodes[0]))
        {
            var safeEntries = Enumerable.Range(1, Math.Min(route.Nodes.Count - 1, 8))
                .Where(index => !graph.IsSwitch(route.Nodes[index]) &&
                    graph.SegmentsConnectedTo(route.Nodes[index]).Count() == 2)
                .ToArray();
            if (safeEntries.Length > 0)
            {
                int safeEntry = safeEntries[0];
                ModLog.Information("Second-mainline entry for {GroupId} preserves panel switch 47 node {PanelSwitch}; entry turnout starts at adjacent two-leg mainline node {EntryNode}",
                    groupId, route.Nodes[0].id, route.Nodes[safeEntry].id);
                return safeEntry;
            }
        }

        var distances = new float[route.Nodes.Count];
        for (int i = 1; i < route.Nodes.Count; i++)
            distances[i] = distances[i - 1] + route.Segments[i - 1].GetLength();

        float sourceDistance = distances[firstSourceIndex];
        var candidates = Enumerable.Range(0, route.Nodes.Count)
            .Where(index => index <= firstSourceIndex && graph.IsSwitch(route.Nodes[index]) &&
                Mathf.Abs(distances[index] - sourceDistance) <= DoubleMainlineSettings.EntrySwitchSearchDistance)
            .OrderByDescending(index => route.Nodes[index].IsCTCSwitch)
            .ThenBy(index => Mathf.Abs(distances[index] - sourceDistance))
            .ThenBy(index => index)
            .ToArray();
        if (candidates.Length == 0)
        {
            ModLog.Warning("No existing mainline switch found near second-mainline entry for route index {RouteIndex}; using the native group boundary", firstSourceIndex);
            return firstSourceIndex;
        }

        int selected = candidates[0];
        // Adding a generated leg directly to an existing turnout makes a four-leg node.
        // That appears at the map as several overlapping switch blades and can also cause
        // the world rail mesh to disappear at the junction. Keep the native turnout intact:
        // branch once from the first ordinary two-leg mainline node immediately beyond it.
        int safeEnd = Math.Min(route.Nodes.Count - 1, Math.Max(firstSourceIndex, selected + 8));
        int adjacentEntryIndex = -1;
        for (int index = selected + 1; index <= safeEnd; index++)
        {
            if (graph.IsSwitch(route.Nodes[index]) || graph.SegmentsConnectedTo(route.Nodes[index]).Count() != 2)
                continue;
            adjacentEntryIndex = index;
            break;
        }
        if (adjacentEntryIndex >= 0)
        {
            ModLog.Information("Second-mainline entry anchor: preserve native turnout {SwitchNode} at route index {SwitchIndex}; branch once from adjacent two-leg node {EntryNode} at route index {EntryIndex}; boundary={BoundaryIndex}; switch links=[{SwitchTracks}], entry links=[{EntryTracks}]",
                route.Nodes[selected].id, selected, route.Nodes[adjacentEntryIndex].id, adjacentEntryIndex, firstSourceIndex,
                DescribeConnectedTracks(graph, route.Nodes[selected]), DescribeConnectedTracks(graph, route.Nodes[adjacentEntryIndex]));
            return adjacentEntryIndex;
        }

        ModLog.Warning("No two-leg mainline node found after entry turnout {SwitchNode} near route index {BoundaryIndex}; using turnout directly with its existing {LegCount} legs",
            route.Nodes[selected].id, firstSourceIndex, graph.SegmentsConnectedTo(route.Nodes[selected]).Count());
        return selected;
    }

    private static int FindEntryEndIndex(RoutePath route, int entryStartIndex, int firstSourceIndex,
        int lastSourceIndex, IReadOnlyList<PassingSidingDetour> sidings)
    {
        if (entryStartIndex < 0 || lastSourceIndex <= entryStartIndex) return -1;
        float targetLength = MainlineEndpointTransitionLength;
        var distances = new float[route.Nodes.Count];
        for (int i = 1; i < route.Nodes.Count; i++)
            distances[i] = distances[i - 1] + route.Segments[i - 1].GetLength();

        int best = -1;
        float bestError = float.PositiveInfinity;
        for (int end = Math.Max(entryStartIndex + 1, firstSourceIndex); end <= lastSourceIndex + 1; end++)
        {
            float length = distances[end] - distances[entryStartIndex];
            if (length < targetLength * 0.7f || length > targetLength * 1.5f) continue;
            if (HasTunnelNearInterval(route, entryStartIndex, end)) continue;
            if (sidings.Any(siding => siding.StartIndex <= end && siding.EndIndex > entryStartIndex)) continue;
            float error = Mathf.Abs(length - targetLength);
            if (error >= bestError) continue;
            best = end;
            bestError = error;
        }
        return best;
    }

    private static int FindSafeExitEndIndex(Graph graph, RoutePath route, int firstSourceIndex, int lastSourceIndex)
    {
        int boundary = Math.Min(lastSourceIndex + 1, route.Nodes.Count - 1);
        // The Ela bridge approach has a longer, denser node chain than a typical area
        // boundary. Search far enough back to find an above-ground two-leg node if the
        // final few nodes are part of the tunnel/bridge transition.
        int minimum = Math.Max(firstSourceIndex + 1, boundary - 32);
        if (boundary < minimum) return boundary;
        var safeNodes = Enumerable.Range(minimum, boundary - minimum + 1)
            .Where(index => !graph.IsSwitch(route.Nodes[index]) &&
                graph.SegmentsConnectedTo(route.Nodes[index]).Count() == 2 &&
                !HasTunnelNearInterval(route, Math.Max(0, index - 1), index))
            .OrderByDescending(index => index)
            .ToArray();
        var distances = new float[route.Nodes.Count];
        for (int i = 1; i < route.Nodes.Count; i++)
            distances[i] = distances[i - 1] + route.Segments[i - 1].GetLength();
        foreach (int safe in safeNodes)
        {
            bool hasClearRunIn = Enumerable.Range(Math.Max(firstSourceIndex + 1, safe - 16),
                    safe - Math.Max(firstSourceIndex + 1, safe - 16))
                .Any(start => distances[safe] - distances[start] >= 20f &&
                    distances[safe] - distances[start] <= MainlineEndpointTransitionLength * 1.5f &&
                    !HasTunnelNearInterval(route, start, safe));
            if (hasClearRunIn)
            {
                ModLog.Information("Second-mainline exit anchor selected route index {Index} of {Boundary}, {Distance:F0}m before source boundary; connected=[{Links}]",
                    safe, boundary, distances[boundary] - distances[safe], DescribeConnectedTracks(graph, route.Nodes[safe]));
                return safe;
            }
        }
        ModLog.Warning("No ordinary two-leg node found near second-mainline end boundary index {Boundary}; using native boundary node {NodeId}",
            boundary, route.Nodes[boundary].id);
        return boundary;
    }

    private static int FindExitStartIndex(RoutePath route, int entryEndIndex, int exitEndIndex,
        IReadOnlyList<PassingSidingDetour> sidings)
    {
        if (entryEndIndex < 0 || exitEndIndex <= entryEndIndex + 1) return -1;
        float[] transitionLengths =
        {
            MainlineEndpointTransitionLength,
            MainlineEndpointTransitionLength * 0.65f,
            MainlineEndpointTransitionLength * 0.4f,
            80f,
            40f,
            20f
        };
        var distances = new float[route.Nodes.Count];
        for (int i = 1; i < route.Nodes.Count; i++)
            distances[i] = distances[i - 1] + route.Segments[i - 1].GetLength();

        foreach (float targetLength in transitionLengths)
        {
            int best = -1;
            float bestError = float.PositiveInfinity;
            for (int start = Math.Max(entryEndIndex + 1, 1); start < exitEndIndex; start++)
            {
                float length = distances[exitEndIndex] - distances[start];
                if (length < targetLength * 0.7f || length > targetLength * 1.5f) continue;
                if (HasTunnelNearInterval(route, start, exitEndIndex)) continue;
                if (sidings.Any(siding => siding.StartIndex < exitEndIndex && siding.EndIndex > start)) continue;
                float error = Mathf.Abs(length - targetLength);
                if (error >= bestError) continue;
                best = start;
                bestError = error;
            }
            if (best < 0) continue;
            ModLog.Information("Second-mainline exit transition selected route indexes {Start}-{End}, length={Length:F0}m (target {Target:F0}m)",
                best, exitEndIndex, distances[exitEndIndex] - distances[best], targetLength);
            return best;
        }
        ModLog.Warning("No tunnel-clear second-mainline exit transition found before route index {ExitEnd}; tried lengths [{Lengths}]m",
            exitEndIndex, string.Join(",", transitionLengths.Select(length => length.ToString("F0"))));
        return -1;
    }

    private static IReadOnlyList<TrackSegment> BuildGentleRouteExit(Graph graph, RoutePath route, string groupId,
        int startIndex, int endIndex, IReadOnlyList<Vector3> offsets, float side)
    {
        string id = IdPrefix + "Exit." + groupId;
        var finalExisting = graph.GetSegment(id + "." + DoubleMainlineSettings.CrossoverCurveSamples);
        if (finalExisting != null)
            return Enumerable.Range(1, DoubleMainlineSettings.CrossoverCurveSamples)
                .Select(sample => graph.GetSegment(id + "." + sample))
                .OfType<TrackSegment>().ToArray();

        var parallelStart = ParallelNode(graph, route.Nodes[startIndex], offsets[startIndex] * side, route, startIndex);
        var sourceEnd = route.Nodes[endIndex];
        parallelStart.IsCTCSwitch = parallelStart.IsCTCSwitchUnlocked = true;
        sourceEnd.IsCTCSwitch = sourceEnd.IsCTCSwitchUnlocked = true;
        var startPosition = parallelStart.transform.localPosition;
        var endPosition = sourceEnd.transform.localPosition;
        var startTangent = RouteTangent(route, startIndex);
        var endTangent = RouteTangent(route, endIndex);
        float length = 0f;
        for (int i = startIndex; i < endIndex; i++) length += route.Segments[i].GetLength();
        float handleLength = Mathf.Min(DoubleMainlineSettings.CrossoverTransitionLength * 0.45f, length * 0.45f);
        var control1 = startPosition + startTangent * handleLength;
        var control2 = endPosition - endTangent * handleLength;
        var created = new List<TrackSegment>(DoubleMainlineSettings.CrossoverCurveSamples);
        TrackNode previous = parallelStart;
        for (int sample = 1; sample <= DoubleMainlineSettings.CrossoverCurveSamples; sample++)
        {
            float t = sample / (float)DoubleMainlineSettings.CrossoverCurveSamples;
            TrackNode next;
            if (sample == DoubleMainlineSettings.CrossoverCurveSamples) next = sourceEnd;
            else
            {
                var point = CubicBezier(startPosition, control1, control2, endPosition, t);
                var tangent = CubicBezierTangent(startPosition, control1, control2, endPosition, t);
                var worldTangent = graph.transform.TransformDirection(tangent);
                var rotation = worldTangent.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(worldTangent.normalized, graph.transform.up)
                    : parallelStart.transform.rotation;
                next = graph.AddNode(id + ".Node." + sample, graph.transform.TransformPoint(point), rotation);
                next.transform.SetParent(graph.transform, true);
            }
            int segmentIndex = Mathf.Clamp(startIndex + Mathf.RoundToInt((endIndex - startIndex - 1) *
                ((sample - 0.5f) / DoubleMainlineSettings.CrossoverCurveSamples)), 0, route.Segments.Count - 1);
            var sourceSegment = route.Segments[segmentIndex];
            var segment = graph.GetSegment(id + "." + sample) ?? graph.AddSegment(id + "." + sample, previous, next);
            segment.style = sourceSegment.style == TrackSegment.Style.Tunnel ? TrackSegment.Style.Standard : sourceSegment.style;
            segment.trackClass = TrackClass.Mainline;
            segment.priority = sourceSegment.priority;
            segment.speedLimit = sourceSegment.speedLimit;
            segment.groupId = string.Empty;
            segment.GroupEnabled = true;
            segment.Available = true;
            segment.transform.SetParent(graph.transform, true);
            created.Add(segment);
            previous = next;
        }
        return created;
    }

    private static IReadOnlyList<TrackSegment> BuildGentleRouteEntry(Graph graph, RoutePath route, string groupId,
        int startIndex, int endIndex, IReadOnlyList<Vector3> offsets, float side)
    {
        string id = IdPrefix + "Entry." + groupId;
        var finalExisting = graph.GetSegment(id + "." + DoubleMainlineSettings.CrossoverCurveSamples);
        if (finalExisting != null)
            return Enumerable.Range(1, DoubleMainlineSettings.CrossoverCurveSamples)
                .Select(sample => graph.GetSegment(id + "." + sample))
                .OfType<TrackSegment>().ToArray();

        var sourceStart = route.Nodes[startIndex];
        var sourceEnd = route.Nodes[endIndex];
        var parallelEnd = ParallelNode(graph, sourceEnd, offsets[endIndex] * side, route, endIndex);
        sourceStart.IsCTCSwitch = true;
        sourceStart.IsCTCSwitchUnlocked = true;
        parallelEnd.IsCTCSwitch = true;
        parallelEnd.IsCTCSwitchUnlocked = true;

        var startPosition = sourceStart.transform.localPosition;
        var endPosition = parallelEnd.transform.localPosition;
        var startTangent = RouteTangent(route, startIndex);
        var endTangent = RouteTangent(route, endIndex);
        float handleLength = Mathf.Min(DoubleMainlineSettings.CrossoverTransitionLength * 0.45f,
            Mathf.Abs(route.Segments.Skip(startIndex).Take(endIndex - startIndex).Sum(segment => segment.GetLength())) * 0.45f);
        var control1 = startPosition + startTangent * handleLength;
        var control2 = endPosition - endTangent * handleLength;

        var created = new List<TrackSegment>(DoubleMainlineSettings.CrossoverCurveSamples);
        var previousNode = sourceStart;
        for (int sample = 1; sample <= DoubleMainlineSettings.CrossoverCurveSamples; sample++)
        {
            float t = sample / (float)DoubleMainlineSettings.CrossoverCurveSamples;
            TrackNode nextNode;
            if (sample == DoubleMainlineSettings.CrossoverCurveSamples) nextNode = parallelEnd;
            else
            {
                var point = CubicBezier(startPosition, control1, control2, endPosition, t);
                var tangent = CubicBezierTangent(startPosition, control1, control2, endPosition, t);
                var worldTangent = graph.transform.TransformDirection(tangent);
                var rotation = worldTangent.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(worldTangent.normalized, graph.transform.up)
                    : sourceStart.transform.rotation;
                nextNode = graph.AddNode(id + ".Node." + sample, graph.transform.TransformPoint(point), rotation);
                nextNode.transform.SetParent(graph.transform, true);
            }

            int sourceSegmentIndex = Mathf.Clamp(startIndex + Mathf.RoundToInt((endIndex - startIndex - 1) *
                ((sample - 0.5f) / DoubleMainlineSettings.CrossoverCurveSamples)), 0, route.Segments.Count - 1);
            var sourceSegment = route.Segments[sourceSegmentIndex];
            var entrySegment = graph.GetSegment(id + "." + sample) ??
                graph.AddSegment(id + "." + sample, previousNode, nextNode);
            entrySegment.style = sourceSegment.style;
            entrySegment.trackClass = TrackClass.Mainline;
            entrySegment.priority = sourceSegment.priority;
            entrySegment.speedLimit = sourceSegment.speedLimit;
            entrySegment.groupId = string.Empty;
            entrySegment.GroupEnabled = true;
            entrySegment.Available = true;
            entrySegment.transform.SetParent(graph.transform, true);
            created.Add(entrySegment);
            previousNode = nextNode;
        }
        RouteEntrySwitches[groupId] = (sourceStart, parallelEnd);
        return created;
    }

    private sealed class PassingSidingDetour
    {
        internal int StartIndex { get; set; }
        internal int EndIndex { get; set; }
        internal int ParallelStartIndex { get; set; }
        internal int ParallelEndIndex { get; set; }
        internal int EntrySidingNodeIndex { get; set; }
        internal int ExitSidingNodeIndex { get; set; }
        internal IReadOnlyList<TrackSegment> Segments { get; set; } = Array.Empty<TrackSegment>();
        internal IReadOnlyList<TrackSegment> EntryCrossoverSegments { get; set; } = Array.Empty<TrackSegment>();
        internal IReadOnlyList<TrackSegment> ExitCrossoverSegments { get; set; } = Array.Empty<TrackSegment>();
    }

    private static float? SidingSidePreference(RoutePath route, IReadOnlyList<PassingSidingDetour> sidings,
        IReadOnlyList<Vector3> offsets)
    {
        float score = 0f;
        foreach (var siding in sidings)
        {
            if (siding.Segments.Count == 0) continue;
            var firstInside = siding.Segments[0].GetOtherNode(route.Nodes[siding.StartIndex]);
            var delta = firstInside.transform.localPosition - route.Nodes[siding.StartIndex].transform.localPosition;
            delta.y = 0f;
            score += Vector3.Dot(delta, offsets[siding.StartIndex]);
        }
        if (Mathf.Abs(score) < 1f) return null;
        return score > 0f ? 1f : -1f;
    }

    private static List<PassingSidingDetour> BuildSidingExtensions(Graph graph, RoutePath route, string groupId,
        IReadOnlyList<PassingSidingDetour> candidates, IReadOnlyList<Vector3> offsets, float side,
        int entryEndIndex, int exitStartIndex, int firstSourceIndex, int lastSourceIndex, out bool changed)
    {
        changed = false;
        int sidingExitLimit = exitStartIndex > 0 ? exitStartIndex : lastSourceIndex + 1;
        var built = new List<PassingSidingDetour>();
        var controls = new List<(string Name, TrackNode First, TrackNode Second)>();
        foreach (var candidate in candidates)
        {
            if (candidate.Segments.Count < 2 || candidate.StartIndex < firstSourceIndex ||
                candidate.EndIndex > lastSourceIndex + 1 || candidate.StartIndex <= entryEndIndex ||
                sidingExitLimit <= 0 || candidate.EndIndex >= sidingExitLimit)
            {
                ModLog.Information("Passing siding extension skipped for {GroupId}: indexes {Start}-{End} are outside generated parallel route {ParallelStart}-{ParallelEnd}",
                    groupId, candidate.StartIndex, candidate.EndIndex, entryEndIndex, sidingExitLimit);
                continue;
            }

            var nodes = new List<TrackNode> { route.Nodes[candidate.StartIndex] };
            var cursor = route.Nodes[candidate.StartIndex];
            foreach (var segment in candidate.Segments)
            {
                cursor = segment.GetOtherNode(cursor);
                nodes.Add(cursor);
            }
            if (cursor != route.Nodes[candidate.EndIndex])
            {
                ModLog.Warning("Passing siding extension skipped for {GroupId}: siding path {Start}->{End} did not reach its far turnout",
                    groupId, route.Nodes[candidate.StartIndex].id, route.Nodes[candidate.EndIndex].id);
                continue;
            }

            float pathLength = candidate.Segments.Sum(segment => segment.GetLength());
            float attachmentDistance = Mathf.Clamp(pathLength * 0.33f, 120f, 260f);
            attachmentDistance = Mathf.Min(attachmentDistance, pathLength * 0.42f);
            int entryNodeIndex = Enumerable.Range(1, nodes.Count - 2)
                .OrderBy(index => Mathf.Abs(PathDistance(candidate.Segments, index) - attachmentDistance))
                .FirstOrDefault();
            int exitNodeIndex = Enumerable.Range(1, nodes.Count - 2)
                .OrderBy(index => Mathf.Abs((pathLength - PathDistance(candidate.Segments, index)) - attachmentDistance))
                .FirstOrDefault();
            if (entryNodeIndex <= 0 || exitNodeIndex <= 0 || entryNodeIndex >= exitNodeIndex)
            {
                ModLog.Information("Passing siding extension skipped for {GroupId}: no distinct interior attachment nodes on {Start}->{End}",
                    groupId, route.Nodes[candidate.StartIndex].id, route.Nodes[candidate.EndIndex].id);
                continue;
            }

            int parallelStartIndex = Math.Max(candidate.StartIndex + 1, entryEndIndex);
            int parallelEndIndex = Math.Min(candidate.EndIndex - 1, sidingExitLimit - 1);
            int entryMinimum = parallelStartIndex;
            int entryMaximum = Math.Min(parallelEndIndex - 1, candidate.EndIndex - 2);
            int exitMinimum = Math.Max(entryMinimum + 1, candidate.StartIndex + 2);
            int exitMaximum = parallelEndIndex;
            int parallelEntryIndex = ClosestRouteIndex(route, candidate.StartIndex, entryMinimum,
                entryMaximum, PathDistance(candidate.Segments, entryNodeIndex));
            int parallelExitIndex = ClosestRouteIndexFromEnd(route, exitMinimum, exitMaximum,
                candidate.EndIndex, pathLength - PathDistance(candidate.Segments, exitNodeIndex));
            if ((parallelEntryIndex < 0 || parallelExitIndex < 0 || parallelExitIndex - parallelEntryIndex < 2) &&
                entryMinimum < exitMaximum)
            {
                // A short siding has only a few useful offset nodes. Keep its two ends on
                // distinct parallel nodes instead of collapsing both Z connectors into one
                // unsupported four-leg switch.
                parallelEntryIndex = entryMinimum;
                parallelExitIndex = exitMaximum;
            }
            if (parallelEntryIndex < 0 || parallelExitIndex < 0 || parallelEntryIndex >= parallelExitIndex ||
                parallelExitIndex - parallelEntryIndex < 2 ||
                RouteLengthBetween(route, parallelEntryIndex, parallelExitIndex) < 150f ||
                HasTunnelNearInterval(route, Math.Max(candidate.StartIndex, parallelEntryIndex - 1),
                    Math.Min(candidate.EndIndex, parallelExitIndex + 1)))
            {
                ModLog.Information("Passing siding extension skipped for {GroupId}: no safe parallel interval for {Start}->{End}",
                    groupId, route.Nodes[candidate.StartIndex].id, route.Nodes[candidate.EndIndex].id);
                continue;
            }

            var parallelEntry = ParallelNode(graph, route.Nodes[parallelEntryIndex], offsets[parallelEntryIndex] * side, route, parallelEntryIndex);
            var parallelExit = ParallelNode(graph, route.Nodes[parallelExitIndex], offsets[parallelExitIndex] * side, route, parallelExitIndex);
            var entryInside = nodes[entryNodeIndex];
            var exitInside = nodes[exitNodeIndex];
            string sidingKey = groupId + ".Siding." + route.Nodes[candidate.StartIndex].id + "." + route.Nodes[candidate.EndIndex].id;
            var entryTangent = (nodes[entryNodeIndex + 1].transform.localPosition -
                nodes[entryNodeIndex - 1].transform.localPosition).normalized;
            var exitTangent = (nodes[exitNodeIndex + 1].transform.localPosition -
                nodes[exitNodeIndex - 1].transform.localPosition).normalized;
            var entryCurve = BuildSidingConnector(graph, route, sidingKey + ".Entry", parallelEntry,
                entryInside, parallelEntryIndex, entryTangent, route.Segments[parallelEntryIndex], out var entryChanged);
            var exitCurve = BuildSidingConnector(graph, route, sidingKey + ".Exit", exitInside,
                parallelExit, parallelExitIndex, exitTangent, route.Segments[parallelExitIndex], out var exitChanged);
            changed |= entryChanged || exitChanged;
            var detour = new PassingSidingDetour
            {
                StartIndex = candidate.StartIndex,
                EndIndex = candidate.EndIndex,
                ParallelStartIndex = parallelEntryIndex,
                ParallelEndIndex = parallelExitIndex,
                EntrySidingNodeIndex = entryNodeIndex,
                ExitSidingNodeIndex = exitNodeIndex,
                Segments = candidate.Segments,
                EntryCrossoverSegments = entryCurve,
                ExitCrossoverSegments = exitCurve
            };
            built.Add(detour);
            entryInside.IsCTCSwitch = entryInside.IsCTCSwitchUnlocked = true;
            exitInside.IsCTCSwitch = exitInside.IsCTCSwitchUnlocked = true;
            parallelEntry.IsCTCSwitch = parallelEntry.IsCTCSwitchUnlocked = true;
            parallelExit.IsCTCSwitch = parallelExit.IsCTCSwitchUnlocked = true;
            controls.Add((sidingKey + ".Entry", entryInside, parallelEntry));
            controls.Add((sidingKey + ".Exit", exitInside, parallelExit));
            ModLog.Information("Routed second mainline {GroupId} through passing siding {StartTurnout}->{EndTurnout}; native turnout nodes preserved; skipped parallel indexes [{ParallelStart},{ParallelEnd}), Z connectors at indexes {EntryIndex}/{ExitIndex}: {EntryNode}->{EntryParallel} and {ExitNode}->{ExitParallel}; siding-through segments=[{ThroughSegments}]",
                groupId, route.Nodes[candidate.StartIndex].id, route.Nodes[candidate.EndIndex].id,
                parallelEntryIndex, parallelExitIndex, parallelEntryIndex, parallelExitIndex,
                entryInside.id, parallelEntry.id, exitInside.id, parallelExit.id,
                string.Join(",", candidate.Segments.Skip(entryNodeIndex).Take(exitNodeIndex - entryNodeIndex)
                    .Select(segment => segment.id)));
        }
        if (controls.Count > 0) SidingCrossoverControls[groupId] = controls;
        else SidingCrossoverControls.Remove(groupId);
        return built;
    }

    private static float PathDistance(IReadOnlyList<TrackSegment> segments, int count)
    {
        float distance = 0f;
        for (int i = 0; i < Math.Min(count, segments.Count); i++) distance += segments[i].GetLength();
        return distance;
    }

    private static float RouteLengthBetween(RoutePath route, int startIndex, int endIndex)
    {
        float distance = 0f;
        for (int i = startIndex; i < endIndex; i++) distance += route.Segments[i].GetLength();
        return distance;
    }

    private static int ClosestRouteIndex(RoutePath route, int originIndex, int minimumIndex, int maximumIndex, float targetDistance)
    {
        if (minimumIndex > maximumIndex || originIndex < 0 || maximumIndex >= route.Nodes.Count) return -1;
        var choices = new List<(int Index, float Distance)>();
        float distance = 0f;
        for (int index = originIndex + 1; index <= maximumIndex; index++)
        {
            distance += route.Segments[index - 1].GetLength();
            if (index >= minimumIndex) choices.Add((index, Mathf.Abs(distance - targetDistance)));
        }
        return choices.Count == 0 ? -1 : choices.OrderBy(choice => choice.Distance).First().Index;
    }

    private static int ClosestRouteIndexFromEnd(RoutePath route, int startIndex, int endIndex, int endTurnout, float targetDistance)
    {
        int best = -1;
        float bestError = float.PositiveInfinity;
        for (int index = startIndex; index <= endIndex; index++)
        {
            float distance = 0f;
            for (int segment = index; segment < endTurnout; segment++) distance += route.Segments[segment].GetLength();
            float error = Mathf.Abs(distance - targetDistance);
            if (error >= bestError) continue;
            best = index;
            bestError = error;
        }
        return best;
    }

    private static IReadOnlyList<TrackSegment> BuildSidingConnector(Graph graph, RoutePath route,
        string id, TrackNode start, TrackNode end, int parallelRouteIndex, Vector3 sidingTangent,
        TrackSegment sourceSegment, out bool changed)
    {
        changed = false;
        if (BuiltSidingConnectorGeometry.Contains(id))
            return Enumerable.Range(1, DoubleMainlineSettings.CrossoverCurveSamples)
                .Select(sample => graph.GetSegment(IdPrefix + id + "." + sample))
                .OfType<TrackSegment>().ToArray();

        var startPosition = start.transform.localPosition;
        var endPosition = end.transform.localPosition;
        var routeTangent = RouteTangent(route, parallelRouteIndex);
        Vector3 startTangent;
        Vector3 endTangent;
        bool routeAtStart = start.id.StartsWith(IdPrefix + "Node.", StringComparison.Ordinal);
        if (routeAtStart)
        {
            startTangent = routeTangent;
            endTangent = sidingTangent;
        }
        else
        {
            startTangent = sidingTangent;
            endTangent = routeTangent;
        }
        if (Vector3.Dot(startTangent, endPosition - startPosition) < 0f) startTangent = -startTangent;
        if (Vector3.Dot(endTangent, endPosition - startPosition) < 0f) endTangent = -endTangent;
        float handleLength = Mathf.Clamp(Vector3.Distance(startPosition, endPosition) * 0.35f, 35f, 140f);
        var control1 = startPosition + startTangent.normalized * handleLength;
        var control2 = endPosition - endTangent.normalized * handleLength;
        var created = new List<TrackSegment>(DoubleMainlineSettings.CrossoverCurveSamples);
        TrackNode previous = start;
        for (int sample = 1; sample <= DoubleMainlineSettings.CrossoverCurveSamples; sample++)
        {
            float t = sample / (float)DoubleMainlineSettings.CrossoverCurveSamples;
            TrackNode next;
            if (sample == DoubleMainlineSettings.CrossoverCurveSamples) next = end;
            else
            {
                var point = CubicBezier(startPosition, control1, control2, endPosition, t);
                var tangent = CubicBezierTangent(startPosition, control1, control2, endPosition, t);
                var worldTangent = graph.transform.TransformDirection(tangent);
                var rotation = worldTangent.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(worldTangent.normalized, graph.transform.up)
                    : start.transform.rotation;
                string nodeId = IdPrefix + id + ".Node." + sample;
                next = graph.GetNode(nodeId) ?? graph.AddNode(nodeId, graph.transform.TransformPoint(point), rotation);
                if (next.transform.parent != graph.transform) next.transform.SetParent(graph.transform, true);
                next.transform.position = graph.transform.TransformPoint(point);
                next.transform.rotation = rotation;
            }
            string segmentId = IdPrefix + id + "." + sample;
            var segment = graph.GetSegment(segmentId) ?? graph.AddSegment(segmentId, previous, next);
            // Existing saves may already contain an earlier version of this connector. Rewire
            // every sample and move its generated nodes so the stale endpoints cannot survive
            // merely because the IDs are already present in the graph.
            segment.a = previous;
            segment.b = next;
            segment.style = sourceSegment.style == TrackSegment.Style.Tunnel ? TrackSegment.Style.Standard : sourceSegment.style;
            segment.trackClass = TrackClass.Mainline;
            segment.priority = sourceSegment.priority;
            segment.speedLimit = sourceSegment.speedLimit;
            segment.groupId = string.Empty;
            segment.GroupEnabled = true;
            segment.Available = true;
            segment.transform.SetParent(graph.transform, true);
            segment.InvalidateCurve();
            segment.RebuildBezier();
            created.Add(segment);
            previous = next;
        }
        changed = true;
        BuiltSidingConnectorGeometry.Add(id);
        ModLog.Information("Regenerated siding connector {ConnectorId}: {StartNode}->{EndNode}, route index={RouteIndex}, samples={Samples}",
            id, start.id, end.id, parallelRouteIndex, created.Count);
        return created;
    }

    private static List<PassingSidingDetour> FindPassingSidingDetours(Graph graph, RoutePath route, string groupId,
        int firstSourceIndex, int lastSourceIndex)
    {
        string cacheKey = groupId + ":" + firstSourceIndex + ":" + lastSourceIndex + ":" +
            route.Nodes[0].id + ":" + route.Nodes[route.Nodes.Count - 1].id;
        if (PassingSidingCache.TryGetValue(cacheKey, out var cached)) return cached;
        var result = new List<PassingSidingDetour>();
        if (route.Nodes.Count < 4)
        {
            ModLog.Warning("Passing siding scan for {GroupId} stopped: route has only {NodeCount} nodes", groupId, route.Nodes.Count);
            PassingSidingCache[cacheKey] = result;
            return result;
        }
        var routeSegments = route.Segments.ToHashSet();
        var routeDistances = new float[route.Nodes.Count];
        for (int i = 1; i < route.Nodes.Count; i++)
            routeDistances[i] = routeDistances[i - 1] + route.Segments[i - 1].GetLength();

        var candidates = new List<PassingSidingDetour>();
        int eligibleEntrySwitches = 0;
        int connectedSidingPaths = 0;
        for (int start = 0; start < route.Nodes.Count - 2; start++)
        {
            if (start < firstSourceIndex || start > lastSourceIndex) continue;
            var startNode = route.Nodes[start];
            if (!graph.IsSwitch(startNode)) continue;
            bool startHasAlternate = HasNonRouteTrack(graph, startNode, routeSegments);
            ModLog.Information("Siding scan {GroupId}: route switch index={Index} node={NodeId} distance={Distance:F0}m CTC={IsCtc} alternateTrack={HasAlternate}; connected=[{ConnectedTracks}]",
                groupId, start, startNode.id, routeDistances[start], startNode.IsCTCSwitch, startHasAlternate,
                DescribeConnectedTracks(graph, startNode));
            if (!startHasAlternate) continue;
            eligibleEntrySwitches++;
            for (int end = start + 2; end < route.Nodes.Count; end++)
            {
                if (end > lastSourceIndex + 1) break;
                float span = routeDistances[end] - routeDistances[start];
                var endNode = route.Nodes[end];
                if (!graph.IsSwitch(endNode)) continue;
                if (span > DoubleMainlineSettings.PassingSidingMaximumRouteSpan) break;
                bool endHasAlternate = HasNonRouteTrack(graph, endNode, routeSegments);
                if (span < DoubleMainlineSettings.PassingSidingMinimumLength)
                {
                    ModLog.Information("Siding switch pair rejected {GroupId}: {StartNode} -> {EndNode}, route span {Span:F0}m is below minimum {Minimum:F0}m",
                        groupId, startNode.id, endNode.id, span, DoubleMainlineSettings.PassingSidingMinimumLength);
                    continue;
                }
                if (!endHasAlternate)
                {
                    ModLog.Information("Siding switch pair rejected {GroupId}: far switch {EndNode} has no available non-route track; connected=[{EndTracks}]",
                        groupId, endNode.id, DescribeConnectedTracks(graph, endNode));
                    continue;
                }
                if (!TryFindSidingPath(graph, startNode, endNode, routeSegments,
                        span, out var path, out var rejectReason))
                {
                    ModLog.Information("Siding candidate rejected {GroupId}: {StartNode} -> {EndNode}, mainline span={Span:F0}m; reason={Reason}; end tracks=[{EndTracks}]",
                        groupId, startNode.id, endNode.id, span, rejectReason, DescribeConnectedTracks(graph, endNode));
                    continue;
                }
                connectedSidingPaths++;
                ModLog.Information("Siding candidate accepted for evaluation {GroupId}: {StartNode} -> {EndNode}, mainline span={Span:F0}m, siding length={SidingLength:F0}m, segments=[{Segments}]",
                    groupId, startNode.id, endNode.id, span, path.Sum(segment => segment.GetLength()),
                    string.Join(",", path.Select(segment => segment.id)));

                candidates.Add(new PassingSidingDetour
                {
                    StartIndex = start,
                    EndIndex = end,
                    Segments = path
                });
            }
        }

        foreach (var candidate in candidates.OrderByDescending(candidate => candidate.EndIndex - candidate.StartIndex)
                     .ThenBy(candidate => candidate.StartIndex))
        {
            if (result.Any(existing => candidate.StartIndex < existing.EndIndex && candidate.EndIndex > existing.StartIndex))
            {
                ModLog.Information("Siding candidate not selected for {GroupId}: {StartNode} -> {EndNode}, overlaps a longer selected path",
                    groupId, route.Nodes[candidate.StartIndex].id, route.Nodes[candidate.EndIndex].id);
                continue;
            }
            result.Add(candidate);
            ModLog.Information("Siding candidate selected for {GroupId}: route indexes {Start}-{End}, switches {StartNode} -> {EndNode}, path length={PathLength:F0}m",
                groupId, candidate.StartIndex, candidate.EndIndex, route.Nodes[candidate.StartIndex].id,
                route.Nodes[candidate.EndIndex].id, candidate.Segments.Sum(segment => segment.GetLength()));
        }
        result = result.OrderBy(candidate => candidate.StartIndex).ToList();
        PassingSidingCache[cacheKey] = result;
        ModLog.Information("Passing siding scan for {GroupId}: route source range={FirstSource}-{LastSource}, eligible switches={SwitchCount}, connected paths={PathCount}, selected={SelectedCount}",
            groupId, firstSourceIndex, lastSourceIndex, eligibleEntrySwitches, connectedSidingPaths, result.Count);
        return result;
    }

    private static bool HasNonRouteTrack(Graph graph, TrackNode node, HashSet<TrackSegment> routeSegments) =>
        graph.SegmentsConnectedTo(node).Any(segment => !routeSegments.Contains(segment) &&
            !segment.id.StartsWith(IdPrefix, StringComparison.Ordinal) && segment.Available &&
            segment.trackClass != TrackClass.Industrial);

    private static bool TryFindSidingPath(Graph graph, TrackNode start, TrackNode end,
        HashSet<TrackSegment> routeSegments, float routeSpan,
        out IReadOnlyList<TrackSegment> path, out string rejectReason)
    {
        path = Array.Empty<TrackSegment>();
        rejectReason = string.Empty;
        var distances = new Dictionary<TrackNode, float> { [start] = 0f };
        var previous = new Dictionary<TrackNode, (TrackNode Node, TrackSegment Segment)>();
        var frontier = new SortedSet<(float Distance, int Order, TrackNode Node)>();
        var settled = new HashSet<TrackNode>();
        int order = 0;
        frontier.Add((0f, order++, start));
        float maxSidingLength = routeSpan + Mathf.Max(1200f, routeSpan * 0.75f);
        while (frontier.Count > 0)
        {
            var currentEntry = frontier.Min;
            frontier.Remove(currentEntry);
            var current = currentEntry.Node;
            float currentDistance = currentEntry.Distance;
            if (!distances.TryGetValue(current, out float knownDistance) || currentDistance > knownDistance ||
                !settled.Add(current)) continue;
            if (current == end) break;

            foreach (var segment in graph.SegmentsConnectedTo(current))
            {
                if (routeSegments.Contains(segment) || !segment.Available ||
                    segment.id.StartsWith(IdPrefix, StringComparison.Ordinal) || segment.trackClass == TrackClass.Industrial) continue;
                var next = segment.GetOtherNode(current);
                if (settled.Contains(next)) continue;
                float candidateDistance = currentDistance + segment.GetLength();
                if (candidateDistance > maxSidingLength) continue;
                if (distances.TryGetValue(next, out float existing) && candidateDistance >= existing) continue;
                distances[next] = candidateDistance;
                previous[next] = (current, segment);
                frontier.Add((candidateDistance, order++, next));
            }
        }
        if (!previous.ContainsKey(end))
        {
            rejectReason = $"no available non-route path found within {maxSidingLength:F0}m";
            return false;
        }

        var reverse = new List<TrackSegment>();
        var cursor = end;
        while (cursor != start)
        {
            if (!previous.TryGetValue(cursor, out var step))
            {
                rejectReason = "path reconstruction ended before reaching the far switch";
                return false;
            }
            reverse.Add(step.Segment);
            cursor = step.Node;
        }
        reverse.Reverse();
        if (reverse.Count == 0) return false;
        path = reverse;
        return true;
    }

    private static float SpacingScaleNearSiding(RoutePath route, int nodeIndex, IReadOnlyList<PassingSidingDetour> sidings)
    {
        if (sidings.Count == 0) return 1f;
        var distances = new float[route.Nodes.Count];
        for (int i = 1; i < route.Nodes.Count; i++)
            distances[i] = distances[i - 1] + route.Segments[i - 1].GetLength();
        float distance = float.PositiveInfinity;
        foreach (var siding in sidings)
        {
            distance = Mathf.Min(distance, Mathf.Abs(distances[nodeIndex] - distances[siding.StartIndex]),
                Mathf.Abs(distances[nodeIndex] - distances[siding.EndIndex]));
        }
        float t = Mathf.Clamp01(distance / DoubleMainlineSettings.PassingSidingTransitionLength);
        return t * t * (3f - 2f * t);
    }

    private static float ChooseClearSide(Graph graph, RoutePath route, IReadOnlyList<Vector3> offsets, float? sidingSide)
    {
        float positive = ScoreSide(graph, route, offsets, 1f);
        float negative = ScoreSide(graph, route, offsets, -1f);
        float chosen = negative < positive ? -1f : 1f;
        if (sidingSide.HasValue)
        {
            // The existing passing siding fixes the side of the secondary route. Choosing
            // the nominally clearer opposite side sends the siding tie-ins across the native
            // mainline, which creates the crossing-track layout seen near Bryson and Ela.
            chosen = sidingSide.Value;
            ModLog.Information("Second-mainline side constrained by passing-siding alignment: selected {Side}; obstruction scores +={Positive:F0}, -={Negative:F0}",
                chosen > 0 ? "right" : "left", positive, negative);
        }
        else
            ModLog.Information("Second-mainline obstacle probe selected side {Side}; obstruction scores +={Positive:F0}, -={Negative:F0}",
                chosen > 0 ? "right" : "left", positive, negative);
        return chosen;
    }

    private static float ScoreSide(Graph graph, RoutePath route, IReadOnlyList<Vector3> offsets, float sign)
    {
        float score = 0f;
        var seen = new HashSet<int>();
        var routeSegments = route.Segments.ToHashSet();
        var nearbyTrackSegments = Graph.Shared?.Segments
            .Where(segment => segment != null && segment.Available && segment.trackClass == TrackClass.Mainline &&
                !routeSegments.Contains(segment) && !segment.id.StartsWith(IdPrefix, StringComparison.Ordinal))
            .ToArray() ?? Array.Empty<TrackSegment>();
        for (int i = 0; i < route.Nodes.Count; i++)
        {
            float spacing = NearTunnel(route, i) ? DoubleMainlineSettings.TunnelCenterSpacing : DoubleMainlineSettings.TrackCenterSpacing;
            var local = route.Nodes[i].transform.localPosition + offsets[i] * spacing * sign;
            foreach (var segment in nearbyTrackSegments)
            {
                float verticalDelta = Mathf.Abs(local.y - (segment.a.transform.localPosition.y + segment.b.transform.localPosition.y) * 0.5f);
                if (verticalDelta > 10f) continue;
                float clearance = DistanceToSegmentXZ(local, segment.a.transform.localPosition, segment.b.transform.localPosition);
                if (clearance < 14f) score += (14f - clearance) * 3f;
            }
            var world = graph.transform.TransformPoint(local) + graph.transform.up * 2f;
            foreach (var collider in Physics.OverlapSphere(world, DoubleMainlineSettings.PlacementObstacleProbeRadius,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider == null || collider.GetType().Name == "TerrainCollider" || collider.GetComponentInParent<TrackSegment>() != null ||
                    collider.GetComponentInParent<TrackNode>() != null || collider.transform.IsChildOf(graph.transform)) continue;
                if (!seen.Add(collider.GetInstanceID())) continue;
                var name = collider.name.ToLowerInvariant();
                if (name.Contains("terrain") || name.Contains("water") || name.Contains("track object")) continue;
                if (name.Contains("road") || name.Contains("crossing")) score += 10f;
                else if (name.Contains("telegraph") || name.Contains("pole") || name.Contains("wire")) score += 8f;
                else if (name.Contains("tree") || name.Contains("vegetation") || name.Contains("foliage")) score += 5f;
                else if (name.Contains("bridge") || name.Contains("building") || name.Contains("structure")) score += 10f;
                else score += 2f;
            }
        }
        return score;
    }

    private static float DistanceToSegmentXZ(Vector3 point, Vector3 start, Vector3 end)
    {
        point.y = 0f;
        start.y = 0f;
        end.y = 0f;
        var delta = end - start;
        float lengthSquared = delta.sqrMagnitude;
        if (lengthSquared < 0.001f) return Vector3.Distance(point, start);
        float t = Mathf.Clamp01(Vector3.Dot(point - start, delta) / lengthSquared);
        return Vector3.Distance(point, start + delta * t);
    }

    private static Vector3[] OffsetDirections(IReadOnlyList<TrackNode> nodes)
    {
        var result = new Vector3[nodes.Count];
        var routeAxis = RouteAxis();
        for (int i = 0; i < nodes.Count; i++)
        {
            var before = nodes[Math.Max(0, i - 1)].transform.localPosition;
            var after = nodes[Math.Min(nodes.Count - 1, i + 1)].transform.localPosition;
            var tangent = after - before;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.001f) tangent = nodes[i].transform.forward;
            if (Vector3.Dot(tangent, routeAxis) < 0f) tangent = -tangent;
            tangent.Normalize();

            var side = Vector3.Cross(tangent, Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.forward;
            side.Normalize();
            if (i > 0 && Vector3.Dot(side, result[i - 1]) < 0f) side = -side;
            result[i] = side;
        }
        return result;
    }

    private static Vector3 RouteAxis()
    {
        var areas = OpsController.Shared?.Areas?.OfType<Area>().ToArray();
        var andrews = areas?.FirstOrDefault(area => area.identifier.Equals("andrews", StringComparison.OrdinalIgnoreCase));
        var whittier = areas?.FirstOrDefault(area => area.identifier.Equals("whittier", StringComparison.OrdinalIgnoreCase));
        if (andrews != null && whittier != null)
        {
            var axis = WorldTransformer.WorldToGame(whittier.transform.position) - WorldTransformer.WorldToGame(andrews.transform.position);
            axis.y = 0f;
            if (axis.sqrMagnitude > 0.001f) return axis.normalized;
        }
        return Vector3.right;
    }

    private static TrackNode ParallelNode(Graph graph, TrackNode source, Vector3 side, RoutePath route, int routeIndex)
    {
        var id = IdPrefix + "Node." + source.id;
        float spacing = NearTunnel(route, routeIndex) ? DoubleMainlineSettings.TunnelCenterSpacing : DoubleMainlineSettings.TrackCenterSpacing;
        var localPosition = source.transform.localPosition + side * spacing;
        // Keep the parallel rail at the source rail's elevation so the copied profile
        // preserves the existing mainline grades exactly.
        localPosition.y = source.transform.localPosition.y;
        var worldPosition = graph.transform.TransformPoint(localPosition);
        if (!ParallelNodes.TryGetValue(source.id, out var node) || node == null)
            node = graph.GetNode(id);
        if (node == null)
        {
            node = graph.AddNode(id, worldPosition, source.transform.rotation);
            node.transform.SetParent(graph.transform, true);
            parallelNodeSnapRevision++;
        }
        else
        {
            if (node.transform.parent != graph.transform) node.transform.SetParent(graph.transform, true);
            if (Vector3.Distance(node.transform.position, worldPosition) > 0.01f ||
                Quaternion.Angle(node.transform.rotation, source.transform.rotation) > 0.1f)
            {
                node.transform.position = worldPosition;
                node.transform.rotation = source.transform.rotation;
                parallelNodeSnapRevision++;
            }
        }
        node.IsCTCSwitch = source.IsCTCSwitch;
        node.IsCTCSwitchUnlocked = source.IsCTCSwitchUnlocked;
        ParallelNodes[source.id] = node;
        return node;
    }

    private static bool NearTunnel(RoutePath route, int nodeIndex)
    {
        int start = Math.Max(0, nodeIndex - 2);
        int end = Math.Min(route.Segments.Count - 1, nodeIndex + 1);
        for (int i = start; i <= end; i++)
            if (route.Segments[i].style == TrackSegment.Style.Tunnel) return true;
        return false;
    }

    private static bool TryBuildCrossover(Graph graph, string name, string startAreaId, string endAreaId,
        out TrackNode? firstSwitch, out TrackNode? secondSwitch)
    {
        firstSwitch = null;
        secondSwitch = null;
        if (AddedCrossovers.Contains(name)) return false;
        var id = IdPrefix + "Crossover." + name;
        var firstExisting = graph.GetSegment(id);
        var finalExisting = graph.GetSegment(id + "." + DoubleMainlineSettings.CrossoverCurveSamples);
        if (firstExisting != null && finalExisting != null)
        {
            firstSwitch = firstExisting.a;
            secondSwitch = finalExisting.b;
            AddedCrossovers.Add(name);
            return false;
        }
        if (firstExisting != null || finalExisting != null)
        {
            ModLog.Warning("Second-mainline crossover {Name} has an incomplete generated curve; leaving it unchanged", name);
            return false;
        }
        if (!TryShortestMainlinePath(graph, startAreaId, endAreaId, out var route) || route.Nodes.Count < 3) return false;

        var distances = new float[route.Nodes.Count];
        for (int i = 1; i < route.Nodes.Count; i++)
            distances[i] = distances[i - 1] + route.Segments[i - 1].GetLength();
        float routeMidpoint = distances[distances.Length - 1] * 0.5f;
        var routeSidingDetours = route.Segments.Select(segment => segment.groupId)
            .Where(groupId => !string.IsNullOrWhiteSpace(groupId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(groupId => FindPassingSidingDetours(graph, route, groupId, 0, route.Segments.Count - 1))
            .GroupBy(detour => (detour.StartIndex, detour.EndIndex))
            .Select(group => group.First());
        if (routeSidingDetours.Any(detour => distances[detour.StartIndex] <= routeMidpoint &&
                                               distances[detour.EndIndex] >= routeMidpoint))
        {
            var detour = routeSidingDetours.First(candidate => distances[candidate.StartIndex] <= routeMidpoint &&
                                                               distances[candidate.EndIndex] >= routeMidpoint);
            AddedCrossovers.Add(name);
            ModLog.Information("Skipping generated crossover {Name}; integrated siding {StartNode}->{EndNode} spans route midpoint {Midpoint:F0}m",
                name, route.Nodes[detour.StartIndex].id, route.Nodes[detour.EndIndex].id, routeMidpoint);
            return false;
        }

        int bestStart = -1;
        int bestEnd = -1;
        float bestScore = float.PositiveInfinity;
        float minimumLength = DoubleMainlineSettings.CrossoverTransitionLength * 0.65f;
        float maximumLength = DoubleMainlineSettings.CrossoverTransitionLength * 1.5f;

        for (int start = 1; start < route.Nodes.Count - 2; start++)
        {
            if (!ParallelNodes.ContainsKey(route.Nodes[start].id)) continue;
            for (int end = start + 1; end < route.Nodes.Count - 1; end++)
            {
                float length = distances[end] - distances[start];
                if (length < minimumLength) continue;
                if (length > maximumLength) break;
                if (!ParallelNodes.ContainsKey(route.Nodes[end].id) || HasTunnelNearInterval(route, start, end)) continue;
                if (!HasContinuousParallelTrack(graph, route, start, end)) continue;

                float midpointError = Mathf.Abs((distances[start] + distances[end]) * 0.5f - routeMidpoint);
                float lengthError = Mathf.Abs(length - DoubleMainlineSettings.CrossoverTransitionLength);
                float score = midpointError + lengthError * 0.25f;
                if (score >= bestScore) continue;
                bestScore = score;
                bestStart = start;
                bestEnd = end;
            }
        }
        if (bestStart < 0 || bestEnd < 0)
        {
            ModLog.Warning("Could not place second-mainline crossover {Name}: no complete parallel interval near route midpoint {Midpoint:F0}m",
                name, routeMidpoint);
            return false;
        }

        var sourceStart = route.Nodes[bestStart];
        var sourceEnd = route.Nodes[bestEnd];
        var parallelEnd = ParallelNodes[sourceEnd.id];
        var startPosition = sourceStart.transform.localPosition;
        var endPosition = parallelEnd.transform.localPosition;
        var startTangent = RouteTangent(route, bestStart);
        var endTangent = RouteTangent(route, bestEnd);
        float handleLength = Mathf.Min(DoubleMainlineSettings.CrossoverTransitionLength * 0.3f,
            Vector3.Distance(startPosition, endPosition) * 0.42f);
        var control1 = startPosition + startTangent * handleLength;
        var control2 = endPosition - endTangent * handleLength;

        sourceStart.IsCTCSwitch = true;
        sourceStart.IsCTCSwitchUnlocked = true;
        parallelEnd.IsCTCSwitch = true;
        parallelEnd.IsCTCSwitchUnlocked = true;
        firstSwitch = sourceStart;
        secondSwitch = parallelEnd;
        ModLog.Information("Second-mainline crossover {Name}: native switch={NativeNode} @ {NativePosition}, parallel switch={ParallelNode} @ {ParallelPosition}; native links=[{NativeLinks}], parallel links=[{ParallelLinks}]",
            name, sourceStart.id, FormatPosition(sourceStart), parallelEnd.id, FormatPosition(parallelEnd),
            DescribeConnectedTracks(graph, sourceStart), DescribeConnectedTracks(graph, parallelEnd));

        var previousNode = sourceStart;
        for (int sample = 1; sample <= DoubleMainlineSettings.CrossoverCurveSamples; sample++)
        {
            float t = sample / (float)DoubleMainlineSettings.CrossoverCurveSamples;
            TrackNode nextNode;
            if (sample == DoubleMainlineSettings.CrossoverCurveSamples)
            {
                nextNode = parallelEnd;
            }
            else
            {
                var point = CubicBezier(startPosition, control1, control2, endPosition, t);
                var tangent = CubicBezierTangent(startPosition, control1, control2, endPosition, t);
                var worldTangent = graph.transform.TransformDirection(tangent);
                var rotation = worldTangent.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(worldTangent.normalized, graph.transform.up)
                    : sourceStart.transform.rotation;
                var worldPosition = graph.transform.TransformPoint(point);
                nextNode = graph.AddNode(id + ".Node." + sample, worldPosition, rotation);
                nextNode.transform.SetParent(graph.transform, true);
            }

            int sourceSegmentIndex = Mathf.Clamp(bestStart + Mathf.RoundToInt((bestEnd - bestStart - 1) * ((sample - 0.5f) /
                DoubleMainlineSettings.CrossoverCurveSamples)), 0, route.Segments.Count - 1);
            var sourceSegment = route.Segments[sourceSegmentIndex];
            var crossover = graph.AddSegment(sample == 1 ? id : id + "." + sample, previousNode, nextNode);
            crossover.style = sourceSegment.style == TrackSegment.Style.Tunnel ? TrackSegment.Style.Standard : sourceSegment.style;
            crossover.trackClass = TrackClass.Mainline;
            crossover.speedLimit = sourceSegment.speedLimit;
            crossover.priority = sourceSegment.priority;
            crossover.groupId = string.Empty;
            crossover.GroupEnabled = true;
            crossover.Available = true;
            crossover.transform.SetParent(graph.transform, true);
            previousNode = nextNode;
        }

        AddedCrossovers.Add(name);
        return true;
    }

    private static bool HasContinuousParallelTrack(Graph graph, RoutePath route, int startIndex, int endIndex)
    {
        for (int i = startIndex; i < endIndex; i++)
            if (graph.GetSegment(IdPrefix + "Segment." + route.Segments[i].id) == null) return false;
        return true;
    }

    private static bool HasTunnelNearInterval(RoutePath route, int startIndex, int endIndex)
    {
        int start = Math.Max(0, startIndex - 1);
        int end = Math.Min(route.Segments.Count - 1, endIndex);
        for (int i = start; i <= end; i++)
            if (route.Segments[i].style == TrackSegment.Style.Tunnel) return true;
        return false;
    }

    private static Vector3 RouteTangent(RoutePath route, int nodeIndex)
    {
        var before = route.Nodes[Math.Max(0, nodeIndex - 1)].transform.localPosition;
        var after = route.Nodes[Math.Min(route.Nodes.Count - 1, nodeIndex + 1)].transform.localPosition;
        var chord = after - before;
        if (chord.sqrMagnitude < 0.001f) chord = route.Nodes[nodeIndex].transform.forward;

        // Use the native spline's actual endpoint frame, rather than a chord across
        // neighboring nodes. Generated connectors inherit its height and inclination.
        if (route.Segments.Count > 0)
        {
            bool atRouteEnd = nodeIndex >= route.Segments.Count;
            var segment = route.Segments[Mathf.Clamp(atRouteEnd ? route.Segments.Count - 1 : nodeIndex,
                0, route.Segments.Count - 1)];
            float distance = atRouteEnd ? segment.GetLength() : 0f;
            var end = atRouteEnd ? TrackSegment.End.B : TrackSegment.End.A;
            var worldTangent = new Location(segment, distance, end).GetRotation(PositionAccuracy.High) * Vector3.forward;
            var parent = route.Nodes[nodeIndex].transform.parent;
            var tangent = parent != null ? parent.InverseTransformDirection(worldTangent) : worldTangent;
            if (tangent.sqrMagnitude > 0.001f)
            {
                if (Vector3.Dot(tangent, chord) < 0f) tangent = -tangent;
                return tangent.normalized;
            }
        }

        return chord.normalized;
    }

    private static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * inverse * p0 + 3f * inverse * inverse * t * p1 +
               3f * inverse * t * t * p2 + t * t * t * p3;
    }

    private static Vector3 CubicBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float inverse = 1f - t;
        return 3f * inverse * inverse * (p1 - p0) + 6f * inverse * t * (p2 - p1) +
               3f * t * t * (p3 - p2);
    }

    private static float TunnelClearance(RoutePath route, int nodeIndex)
    {
        var point = route.Nodes[nodeIndex].transform.localPosition;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < route.Segments.Count; i++)
        {
            var segment = route.Segments[i];
            if (segment.style != TrackSegment.Style.Tunnel) continue;
            nearest = Mathf.Min(nearest,
                Vector3.Distance(point, segment.a.transform.localPosition),
                Vector3.Distance(point, segment.b.transform.localPosition));
        }
        return float.IsPositiveInfinity(nearest) ? float.MaxValue : nearest;
    }

    private static bool TryShortestMainlinePath(Graph graph, string startAreaId, string endAreaId, out RoutePath route)
    {
        route = RoutePath.Empty;
        string cacheKey = startAreaId + ":" + endAreaId;
        if (NativeRouteCache.TryGetValue(cacheKey, out var cachedRoute))
        {
            if (cachedRoute == null) return false;
            route = cachedRoute;
            return true;
        }

        var areas = OpsController.Shared?.Areas?.OfType<Area>().ToArray();
        var startArea = areas?.FirstOrDefault(area => area.identifier.Equals(startAreaId, StringComparison.OrdinalIgnoreCase));
        var endArea = areas?.FirstOrDefault(area => area.identifier.Equals(endAreaId, StringComparison.OrdinalIgnoreCase));
        if (startArea == null || endArea == null)
        {
            NativeRouteCache[cacheKey] = null;
            return false;
        }

        var startPosition = WorldTransformer.WorldToGame(startArea.transform.position);
        var endPosition = WorldTransformer.WorldToGame(endArea.transform.position);
        var nodes = graph.Nodes.Where(node => !node.id.StartsWith(IdPrefix, StringComparison.Ordinal) &&
            graph.SegmentsConnectedTo(node).Any(segment => segment.trackClass == TrackClass.Mainline &&
                !segment.id.StartsWith(IdPrefix, StringComparison.Ordinal))).ToArray();
        if (nodes.Length == 0)
        {
            NativeRouteCache[cacheKey] = null;
            return false;
        }
        var start = nodes.OrderBy(node => (node.transform.localPosition - startPosition).sqrMagnitude).First();
        var end = nodes.OrderBy(node => (node.transform.localPosition - endPosition).sqrMagnitude).First();
        if (start == end)
        {
            NativeRouteCache[cacheKey] = null;
            return false;
        }

        if (!TryShortestMainlinePath(graph, start, end, out route))
        {
            NativeRouteCache[cacheKey] = null;
            return false;
        }
        NativeRouteCache[cacheKey] = route;
        return true;
    }

    private static bool TryShortestMainlinePath(Graph graph, TrackNode start, TrackNode end, out RoutePath route)
    {
        route = RoutePath.Empty;
        if (start == null || end == null || start == end) return false;

        var pending = new HashSet<TrackNode>(graph.Nodes.Where(node => !node.id.StartsWith(IdPrefix, StringComparison.Ordinal) &&
            graph.SegmentsConnectedTo(node).Any(segment => segment.trackClass == TrackClass.Mainline &&
                segment.Available && !segment.id.StartsWith(IdPrefix, StringComparison.Ordinal))));
        if (!pending.Contains(start) || !pending.Contains(end)) return false;
        var distances = new Dictionary<TrackNode, float> { [start] = 0f };
        var previous = new Dictionary<TrackNode, (TrackNode Node, TrackSegment Segment)>();

        while (pending.Count > 0)
        {
            TrackNode current = null!;
            float currentDistance = float.PositiveInfinity;
            foreach (var candidate in pending)
            {
                if (!distances.TryGetValue(candidate, out float candidateDistance) || candidateDistance >= currentDistance) continue;
                current = candidate;
                currentDistance = candidateDistance;
            }
            if (current == null) break;
            pending.Remove(current);
            if (current == end) break;

            foreach (var segment in graph.SegmentsConnectedTo(current))
            {
                if (segment.trackClass != TrackClass.Mainline || !segment.Available ||
                    segment.id.StartsWith(IdPrefix, StringComparison.Ordinal)) continue;
                var next = segment.GetOtherNode(current);
                if (!pending.Contains(next)) continue;
                float candidateDistance = currentDistance + segment.GetLength();
                if (distances.TryGetValue(next, out float existingDistance) && candidateDistance >= existingDistance) continue;
                distances[next] = candidateDistance;
                previous[next] = (current, segment);
            }
        }

        if (!previous.ContainsKey(end))
        {
            return false;
        }
        var reverseNodes = new List<TrackNode> { end };
        var reverseSegments = new List<TrackSegment>();
        var cursor = end;
        while (cursor != start)
        {
            if (!previous.TryGetValue(cursor, out var step))
            {
                return false;
            }
            reverseSegments.Add(step.Segment);
            cursor = step.Node;
            reverseNodes.Add(cursor);
        }
        reverseNodes.Reverse();
        reverseSegments.Reverse();
        route = new RoutePath(reverseNodes, reverseSegments);
        return true;
    }

    private sealed class RoutePath
    {
        internal static readonly RoutePath Empty = new(new List<TrackNode>(), new List<TrackSegment>());
        internal IReadOnlyList<TrackNode> Nodes { get; }
        internal IReadOnlyList<TrackSegment> Segments { get; }

        internal RoutePath(IReadOnlyList<TrackNode> nodes, IReadOnlyList<TrackSegment> segments)
        {
            Nodes = nodes;
            Segments = segments;
        }
    }
}

[HarmonyPatch(typeof(Progression), nameof(Progression.Configure))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class DoubleMainlineProgression_Configure_Patch
{
    private static void Prefix(Progression __instance) => DoubleMainlineRuntime.AddProgressionLevels(__instance);
}

[HarmonyPatch(typeof(Progression), nameof(Progression.Unconfigure))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class DoubleMainlineProgression_Unconfigure_Patch
{
    private static void Postfix(Progression __instance) => DoubleMainlineRuntime.RemoveProgressionLevels(__instance);
}

[HarmonyPatch(typeof(TrackRebuilder), "IsVisibleDidChange")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class DoubleMainlineTrackRebuilder_Visibility_Patch
{
    private static void Postfix(object entry) => DoubleMainlineRuntime.ForceRendererEntryVisible(entry);
}

[HarmonyPatch(typeof(Graph), nameof(Graph.RebuildCollections))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class DoubleMainlineGraph_RebuildCollections_Patch
{
    private static void Postfix() => DoubleMainlineRuntime.InvalidateTrackScanCache();
}
