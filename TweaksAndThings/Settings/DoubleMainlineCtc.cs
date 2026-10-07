using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using Track;
using Track.Signals;
using Track.Signals.Panel;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

/// <summary>Creates native CTC blocks, signals, and occupancy lamps for generated track.</summary>
internal static class DoubleMainlineCtc
{
    private const string Prefix = "RMROC451.DoubleMainline.";
    private static readonly HashSet<string> addedBlocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> addedSignalGroups = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> addedSwitches = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CTCBlock> blocksByGroup = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> panelSwitchLookupSignatures = new(StringComparer.OrdinalIgnoreCase);
    private const string AutoFollowMarkerId = "RMROC451-AutoFollowTrain";
    private static float nextMarkerUpdate;
    private static bool markerEnabledLastUpdate;
    private static readonly FieldInfo BlockSpans = typeof(CTCBlock).GetField("_spans", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo SpanGraph = typeof(TrackSpan).GetField("_graph", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static CTCPanelController? CurrentPanel => CTCPanelController.Shared ??
        UnityEngine.Object.FindObjectsByType<CTCPanelController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

    internal static IReadOnlyList<TrackNode> PanelSwitchNodes(string displayNumber)
    {
        var panel = CurrentPanel;
        if (panel == null)
        {
            ModLog.Warning("Could not resolve CTC panel switch {DisplayNumber}: no CTCPanelController exists", displayNumber);
            return Array.Empty<TrackNode>();
        }
        var groups = panel.GetComponentsInChildren<CTCPanelGroup>(true);
        var matches = groups
            .Where(group => string.Equals(group.switchKnob?.displayNumber?.Trim(), displayNumber,
                    StringComparison.OrdinalIgnoreCase) ||
                group.name.StartsWith($"Column {displayNumber}-", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var nodes = matches
            .SelectMany(group => group.switches ?? new List<TrackNode>())
            .Where(node => node != null)
            .Distinct()
            .ToArray();
        string groupDetails = string.Join(";", groups.Select(group =>
            $"{group.name}[interlocking={group.interlockingId ?? "-"},switch={group.switchKnob?.displayNumber ?? "-"},signal={group.signalKnob?.displayNumber ?? "-"},nodes={string.Join(",", (group.switches ?? new List<TrackNode>()).Where(node => node != null).Select(node => node.id))}]")
            .ToArray());
        string signature = panel.name + "|" + groups.Length + "|" + groupDetails + "|" + string.Join(",", nodes.Select(node => node.id));
        if (!panelSwitchLookupSignatures.TryGetValue(displayNumber, out var oldSignature) || !string.Equals(signature, oldSignature, StringComparison.Ordinal))
        {
            panelSwitchLookupSignatures[displayNumber] = signature;
            ModLog.Information("CTC panel switch lookup {DisplayNumber}: panel={Panel}, total groups={TotalGroups}, matching groups={MatchingGroups}, nodes={Nodes}, groups=[{GroupDetails}]",
                displayNumber, panel.name, groups.Length, matches.Length,
                string.Join(",", nodes.Select(node => node.id)), groupDetails);
        }
        return nodes;
    }

    internal static void Reset()
    {
        foreach (var tag in UnityEngine.Object.FindObjectsByType<DoubleMainlineCtcTag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            tag.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(tag.gameObject);
        }
        addedBlocks.Clear();
        addedSignalGroups.Clear();
        addedSwitches.Clear();
        blocksByGroup.Clear();
        RefreshPanelCache();
    }

    internal static void UpdateTrainMarker(bool enabled)
    {
        if (Time.unscaledTime < nextMarkerUpdate && enabled == markerEnabledLastUpdate) return;
        nextMarkerUpdate = Time.unscaledTime + (enabled ? 0.4f : 1f);
        markerEnabledLastUpdate = enabled;
        var managers = UnityEngine.Object.FindObjectsByType<CTCPanelMarkerManager>(FindObjectsSortMode.None);
        if (!enabled)
        {
            foreach (var manager in managers) manager.RemoveMarker(AutoFollowMarkerId);
            return;
        }

        var locomotive = TrainController.Shared?.SelectedLocomotive;
        if (locomotive == null || Graph.Shared == null) return;
        var location = locomotive.LocationF;
        if (!location.IsValid) return;

        var samples = new List<(CTCPanelSchematicFace Face, float Distance, Vector2 Position)>();
        var signalMarkers = UnityEngine.Object.FindObjectsByType<TrackMarker>(FindObjectsSortMode.None)
            .Where(marker => marker.type == TrackMarkerType.Signal && marker.Signal != null && marker.Location.HasValue)
            .ToArray();
        foreach (var manager in managers)
        foreach (var face in manager.faces ?? new List<CTCPanelSchematicFace>())
        foreach (var label in face.GetComponentsInChildren<CTCPanelSignalLabel>(true))
        {
            var labelId = (label.number ?? string.Empty).Trim();
            var marker = signalMarkers.FirstOrDefault(candidate =>
                candidate.Signal.id.Equals(labelId, StringComparison.OrdinalIgnoreCase) ||
                candidate.Signal.DisplayName.Equals(labelId, StringComparison.OrdinalIgnoreCase));
            if (marker == null || !Graph.Shared.TryGetDistanceBetweenSameRoute(location, marker.Location!.Value, out var distance)) continue;
            var rect = label.GetComponent<RectTransform>();
            if (rect == null) continue;
            var faceRect = face.RectTransform;
            if (faceRect == null || faceRect.rect.width <= 0f || faceRect.rect.height <= 0f) continue;
            var localPoint = faceRect.InverseTransformPoint(rect.position);
            var normalized = new Vector2(
                Mathf.InverseLerp(faceRect.rect.xMin, faceRect.rect.xMax, localPoint.x),
                Mathf.InverseLerp(faceRect.rect.yMin, faceRect.rect.yMax, localPoint.y));
            samples.Add((face, distance, normalized));
        }
        if (samples.Count == 0) return;

        var selectedSamples = samples.OrderBy(sample => Math.Abs(sample.Distance)).Take(6).ToArray();
        var faceChoice = selectedSamples.GroupBy(sample => sample.Face)
            .OrderByDescending(group => group.Count())
            .First();
        var faceSamples = samples.Where(sample => sample.Face == faceChoice.Key).ToArray();
        var before = faceSamples.Where(sample => sample.Distance <= 0).OrderByDescending(sample => sample.Distance).FirstOrDefault();
        var after = faceSamples.Where(sample => sample.Distance >= 0).OrderBy(sample => sample.Distance).FirstOrDefault();
        Vector2 position;
        if (before.Face != null && after.Face != null && before.Distance < 0 && after.Distance > 0)
        {
            float t = Mathf.Clamp01(-before.Distance / (after.Distance - before.Distance));
            position = Vector2.Lerp(before.Position, after.Position, t);
        }
        else
        {
            position = faceSamples.OrderBy(sample => Math.Abs(sample.Distance)).First().Position;
        }

        foreach (var manager in managers)
        {
            if (!manager._markers.TryGetValue(AutoFollowMarkerId, out var marker))
            {
                manager.AddMarker(AutoFollowMarkerId, Vector2.zero);
                manager._markers.TryGetValue(AutoFollowMarkerId, out marker);
            }
            if (marker == null) continue;
            int faceIndex = manager.faces.IndexOf(faceChoice.Key);
            if (faceIndex < 0) continue;
            string trainName = locomotive.Ident.RoadNumber ?? "TRAIN";
            if (marker.label != null && marker.label.text != trainName) manager.UpdateMarker(AutoFollowMarkerId, trainName);
            marker.Position(new Vector2(faceIndex + Mathf.Clamp01(position.x), Mathf.Clamp01(position.y)));
        }
    }

    internal static void AddCrossover(string name, TrackNode originalNode, TrackNode parallelNode)
    {
        if (addedSwitches.Contains(name)) return;
        var panel = CurrentPanel;
        if (panel == null || !panel.isActiveAndEnabled || !panel.gameObject.activeInHierarchy) return;
        SystemMode panelMode;
        try { panelMode = panel.SystemMode; }
        catch { return; }
        var template = panel.GetComponentsInChildren<CTCPanelGroup>(true)
            .FirstOrDefault(group => group.switchKnob != null && group.signalKnob != null);
        if (template == null) return;

        string interlockingId = Prefix + "Interlocking." + name;
        var interlockingObject = new GameObject(interlockingId);
        interlockingObject.SetActive(false);
        interlockingObject.transform.SetParent(Graph.Shared.transform, false);
        interlockingObject.AddComponent<DoubleMainlineCtcTag>();
        var interlocking = interlockingObject.AddComponent<CTCInterlocking>();
        interlocking.id = interlockingId;
        interlocking.displayName = name + " Second Mainline Crossover";

        var switchSetType = typeof(CTCInterlocking).GetNestedType("SwitchSet", BindingFlags.Public | BindingFlags.NonPublic)!;
        var switchSet = Activator.CreateInstance(switchSetType)!;
        AddToFieldList(switchSet, "switchNodes", originalNode);
        AddToFieldList(switchSet, "switchNodes", parallelNode);
        interlocking.switchSets = new List<CTCInterlocking.SwitchSet> { (CTCInterlocking.SwitchSet)switchSet };

        var outletType = typeof(CTCInterlocking).GetNestedType("Outlet", BindingFlags.Public | BindingFlags.NonPublic)!;
        var left = Activator.CreateInstance(outletType)!;
        var right = Activator.CreateInstance(outletType)!;
        SetField(left, "direction", CTCDirection.Left);
        SetField(right, "direction", CTCDirection.Right);
        interlocking.outlets = new List<CTCInterlocking.Outlet> { (CTCInterlocking.Outlet)left, (CTCInterlocking.Outlet)right };

        var routeType = typeof(CTCInterlocking).GetNestedType("Route", BindingFlags.Public | BindingFlags.NonPublic)!;
        var normal = Activator.CreateInstance(routeType)!;
        var reversed = Activator.CreateInstance(routeType)!;
        AddToFieldList(normal, "switchFilters", SwitchFilter.Normal);
        AddToFieldList(reversed, "switchFilters", SwitchFilter.Reversed);
        SetField(normal, "outletLeft", 0);
        SetField(normal, "outletRight", 1);
        SetField(reversed, "outletLeft", 0);
        SetField(reversed, "outletRight", 1);
        interlocking.routes = new List<CTCInterlocking.Route> { (CTCInterlocking.Route)normal, (CTCInterlocking.Route)reversed };
        interlockingObject.SetActive(true);

        var copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
        copy.name = Prefix + "Switch." + name;
        copy.SetActive(false);
        copy.AddComponent<DoubleMainlineCtcTag>();
        var group = copy.GetComponent<CTCPanelGroup>();
        group.interlockingId = interlockingId;
        group.switches = new List<TrackNode> { originalNode, parallelNode };
        group.switchKnob.knobId = interlockingId;
        group.signalKnob.knobId = interlockingId + ".Signal";
        group.switchKnob.displayNumber = name;
        group.signalKnob.displayNumber = name;
        group._switchKnobExists = true;
        group._signalKnobExists = true;
        foreach (var lamp in copy.GetComponentsInChildren<CTCPanelLamp>(true))
        {
            if (lamp.mode == CTCPanelLamp.Mode.Switch)
            {
                lamp.targetId = originalNode.id;
                lamp.filterValue = (int)SwitchSetting.Normal;
            }
        }
        copy.SetActive(true);
        group.UpdateForMode(panelMode);
        typeof(CTCPanelController).GetField("_panelGroups", BindingFlags.Instance | BindingFlags.NonPublic)?
            .SetValue(panel, panel.GetComponentsInChildren<CTCPanelGroup>(true));
        RefreshPanelCache();
        addedSwitches.Add(name);
    }

    private static void AddToFieldList<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        var list = field.GetValue(target) as System.Collections.IList;
        if (list == null)
        {
            list = (System.Collections.IList)Activator.CreateInstance(field.FieldType)!;
            field.SetValue(target, list);
        }
        list.Add(value);
    }

    private static void SetField(object target, string fieldName, object value) =>
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    internal static void AddUnsignaled(string groupId, IReadOnlyList<TrackSegment> segments)
    {
        if (addedBlocks.Contains(groupId) || segments.Count == 0) return;
        var panel = CurrentPanel;
        var lampTemplate = panel == null ? null : panel.GetComponentsInChildren<CTCPanelLamp>(true)
            .FirstOrDefault(lamp => lamp.mode == CTCPanelLamp.Mode.BlockOccupancy && lamp.GetComponentInParent<CTCPanelGroup>() == null);
        if (panel == null || lampTemplate == null) return;

        var blockId = Prefix + "Block." + groupId;
        var blockObject = new GameObject(blockId);
        blockObject.SetActive(false);
        blockObject.transform.SetParent(Graph.Shared.transform, false);
        blockObject.AddComponent<DoubleMainlineCtcTag>();
        var spans = new List<TrackSpan>();
        foreach (var segment in segments.Distinct())
        {
            var spanObject = new GameObject(blockId + ".Span." + spans.Count);
            spanObject.transform.SetParent(blockObject.transform, false);
            var span = spanObject.AddComponent<TrackSpan>();
            span.id = spanObject.name;
            SpanGraph.SetValue(span, Graph.Shared);
            span.lower = new Location(segment, 0f, TrackSegment.End.A);
            span.upper = new Location(segment, segment.GetLength(), TrackSegment.End.B);
            Graph.Shared.AddSpan(span);
            spans.Add(span);
        }
        var block = blockObject.AddComponent<CTCBlock>();
        block.id = blockId;
        BlockSpans.SetValue(block, spans.ToArray());
        blockObject.SetActive(true);
        AddOccupancyLamp(panel!, lampTemplate, blockId, groupId);
        blocksByGroup[groupId] = block;
        addedBlocks.Add(groupId);
        RefreshPanelCache();
    }

    internal static void Add(string groupId, IReadOnlyList<TrackSegment> segments,
        TrackNode sourceStart, TrackNode sourceEnd, TrackNode parallelStart, TrackNode parallelEnd)
    {
        if (addedSignalGroups.Contains(groupId) || segments.Count == 0) return;
        AddUnsignaled(groupId, segments);
        if (!blocksByGroup.TryGetValue(groupId, out var block) || block == null) return;

        var signalTemplates = UnityEngine.Object.FindObjectsByType<CTCAutoSignal>(FindObjectsSortMode.None)
            .Where(signal => signal != null && signal.gameObject.scene.IsValid() && signal.GetComponent<CTCSignal>() != null)
            .ToArray();
        if (signalTemplates.Length == 0) return;

        var startSegment = segments.FirstOrDefault(segment => segment.a == parallelStart || segment.b == parallelStart);
        var endSegment = segments.FirstOrDefault(segment => segment.a == parallelEnd || segment.b == parallelEnd);
        if (startSegment == null || endSegment == null) return;

        AddSignal(signalTemplates, block, sourceStart, parallelStart, startSegment, block.id + ".A");
        AddSignal(signalTemplates, block, sourceEnd, parallelEnd, endSegment, block.id + ".B");
        addedSignalGroups.Add(groupId);
        RefreshPanelCache();
    }

    private static void AddSignal(CTCAutoSignal[] templates, CTCBlock block, TrackNode sourceNode,
        TrackNode parallelNode, TrackSegment segment, string id)
    {
        var template = templates.OrderBy(signal =>
            (signal.transform.position - WorldTransformer.GameToWorld(sourceNode.transform.position)).sqrMagnitude).First();
        var copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
        copy.name = id;
        copy.AddComponent<DoubleMainlineCtcTag>();
        var signal = copy.GetComponent<CTCSignal>();
        var auto = copy.GetComponent<CTCAutoSignal>();
        if (signal == null || auto == null)
        {
            UnityEngine.Object.Destroy(copy);
            return;
        }
        auto.blocks = new List<CTCBlock> { block };
        auto.interlockingRouteMapping = new List<int>();
        signal.id = id;
        var gamePosition = parallelNode.transform.position;
        copy.transform.position = WorldTransformer.GameToWorld(gamePosition) + Vector3.up * 2f;
        if (segment != null)
        {
            if (DoubleMainlineSettings.MirrorExistingCtcSignalLocations)
            {
                var sourceGamePosition = WorldTransformer.WorldToGame(template.transform.position);
                var offset = parallelNode.transform.position - sourceNode.transform.position;
                copy.transform.position = WorldTransformer.GameToWorld(sourceGamePosition + offset);
                copy.transform.rotation = template.transform.rotation;
            }
            else
            {
                copy.transform.rotation = new Location(segment, 0f, TrackSegment.End.A).GetRotation(PositionAccuracy.High);
            }
        }
    }

    private static void AddOccupancyLamp(CTCPanelController panel, CTCPanelLamp template, string blockId, string groupId)
    {
        var copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
        copy.name = Prefix + "Occupancy." + groupId;
        copy.SetActive(false);
        copy.AddComponent<DoubleMainlineCtcTag>();
        var lamp = copy.GetComponent<CTCPanelLamp>();
        lamp.mode = CTCPanelLamp.Mode.BlockOccupancy;
        lamp.targetId = blockId;
        lamp.filterValue = 0;

        var panelGroups = panel.GetComponentsInChildren<CTCPanelGroup>(true);
        var reference = panelGroups.FirstOrDefault(group => group.switchKnob != null && group.signalKnob != null);
        var down = reference == null ? -panel.transform.up :
            (reference.signalKnob.transform.position - reference.switchKnob.transform.position).normalized;
        int row = UnityEngine.Object.FindObjectsByType<DoubleMainlineCtcTag>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        copy.transform.position = template.transform.position + down * (0.32f * row);
        copy.SetActive(true);
    }

    private static void RefreshPanelCache()
    {
        var panel = CurrentPanel;
        if (panel == null) return;
        typeof(CTCPanelController).GetField("_cachedBlocks", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(panel, null);
        typeof(CTCPanelController).GetField("_cachedInterlockings", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(panel, null);
    }
}

internal sealed class DoubleMainlineCtcTag : MonoBehaviour { }
