using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using Model;
using Model.Definition;
using Model.Definition.Data;
using Model.Ops;
using Game.Progression;
using Track;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NewGameEquipmentPlacement
{
    internal static List<(SetupDescriptor.CarPlacement placement, Location front)> Plan(
        SetupDescriptor template, Model.Ops.Area area, HashSet<string> groups)
    {
        var controller = TrainController.Shared;
        var center = WorldTransformer.WorldToGame(area.transform.position);
        bool Inside(Location point) => point.IsValid && groups.Contains(point.segment.groupId) &&
            Vector3.Distance(point.GetPosition(), center) <= area.radius;
        var repairs = Resources.FindObjectsOfTypeAll<RepairTrack>().Where(r => r.gameObject.scene.IsValid())
            .SelectMany(r => r.TrackSpans).Where(s => s.IsValid && s.lower.HasValue && Inside(s.lower.Value)).Distinct().ToList();
        var serviceSegments = repairs.SelectMany(s => s.GetSegments()).ToHashSet();
        var industrySegments = Resources.FindObjectsOfTypeAll<IndustryComponent>().Where(c => c.gameObject.scene.IsValid())
            .SelectMany(c => c.TrackSpans).Where(s => s.IsValid).SelectMany(s => s.GetSegments()).ToHashSet();
        var spans = Resources.FindObjectsOfTypeAll<TrackSpan>().Where(s => s.gameObject.scene.IsValid() &&
            s.IsValid && s.lower.HasValue && Inside(s.lower.Value)).ToList();
        var interchangeSpans = Resources.FindObjectsOfTypeAll<Interchange>().Where(i => i.gameObject.scene.IsValid())
            .SelectMany(i => i.TrackSpans).ToHashSet();
        var reserved = new List<Vector3>();
        var plan = new List<(SetupDescriptor.CarPlacement, Location)>();
        bool Power(string identifier)
        {
            var definition = controller.PrefabStore.CarDefinitionInfoForIdentifier(identifier).Definition;
            return definition is SteamLocomotiveDefinition || definition is DieselLocomotiveDefinition || definition.Archetype == CarArchetype.Tender;
        }
        foreach (var original in template.placements)
        {
            // Keep contiguous locomotive/tender pairs together; freight goes to yard tracks.
            var identifiers = original.carIdentifier.ToArray();
            for (int start = 0; start < identifiers.Length;)
            {
                bool power = Power(identifiers[start]);
                int end = start + 1;
                while (end < identifiers.Length && Power(identifiers[end]) == power) end++;
                var cut = identifiers.Skip(start).Take(end - start).ToArray();
                start = end;
                float length = TrainController.ApproximateLength(cut.Select(car => new CarDescriptor(
                    controller.PrefabStore.CarDefinitionInfoForIdentifier(car), default, null, null, false, null)));
                Location? selected = null;
                bool Fit(Location front, TrackSpan? span)
                {
                    var points = new List<Vector3>();
                    try
                    {
                        for (float d = 0; d <= length + 2; d += 1)
                        {
                            var point = Graph.Shared.LocationByMoving(front, -d, checkSwitchAgainstMovement: false);
                            if (!Inside(point) || span != null && !span.Contains(point)) return false;
                            var position = point.GetPosition();
                            if (reserved.Any(p => Vector3.SqrMagnitude(p - position) < 16)) return false;
                            foreach (var node in new[] { point.segment.a, point.segment.b })
                            {
                                float clearance = NpcTrackClearance.Fouling(node);
                                if (clearance > 0 && Vector3.Distance(position, node.transform.GamePosition()) < clearance + 3) return false;
                            }
                            var occupied = new HashSet<Car>();
                            controller.CheckForCarsAtPoint(position, 2, occupied, point);
                            if (occupied.Count != 0) return false;
                            points.Add(position);
                        }
                    }
                    catch (EndOfTrack) { return false; }
                    catch (SwitchAgainstMovement) { return false; }
                    reserved.AddRange(points);
                    return true;
                }
                var preferred = power ? repairs : spans.Where(s => !s.GetSegments().Any(serviceSegments.Contains) &&
                    (interchangeSpans.Contains(s) || (s.id + " " + s.name).IndexOf("yard", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                foreach (var span in preferred.OrderBy(s => Vector3.SqrMagnitude(s.lower!.Value.GetPosition() - center)))
                {
                    foreach (var boundary in new[] { span.lower, span.upper })
                    {
                        if (!boundary.HasValue) continue;
                        var inward = boundary.Value;
                        try
                        {
                            if (!span.Contains(Graph.Shared.LocationByMoving(inward, 1, false))) inward = inward.Flipped();
                            for (float offset = length + 5; offset <= span.Length - 5; offset += 2)
                            {
                                var front = Graph.Shared.LocationByMoving(inward, offset, false);
                                if (Fit(front, span)) { selected = front; break; }
                            }
                        }
                        catch (EndOfTrack) { }
                        if (selected.HasValue) break;
                    }
                    if (selected.HasValue) break;
                }
                // Maps without yard spans can use clear, low-priority yard segments.
                // With engine-service spans present, do not silently place engines elsewhere.
                if (!selected.HasValue && (!power || repairs.Count == 0))
                {
                    var anchor = power && repairs.Count > 0 ? repairs[0].lower!.Value.GetPosition() : center;
                    foreach (var segment in Graph.Shared.Segments.Where(s => s.turntable == null && groups.Contains(s.groupId) &&
                        !industrySegments.Contains(s) && !serviceSegments.Contains(s) && s.GetLength() >= length + 10)
                        .OrderBy(s => s.priority).ThenBy(s => Vector3.SqrMagnitude(new Location(s, s.GetLength() / 2, TrackSegment.End.A).GetPosition() - anchor)))
                    {
                        for (float offset = length + 5; offset <= segment.GetLength() - 5; offset += 2)
                        {
                            var front = new Location(segment, offset, TrackSegment.End.A);
                            if (Fit(front, null)) { selected = front; break; }
                        }
                        if (selected.HasValue) break;
                    }
                }
                if (!selected.HasValue) throw new InvalidOperationException("No switch-clear space for " + string.Join(", ", cut) +
                    " in " + area.name + (power ? " engine service / roundhouse tracks." : " yard tracks."));
                var placement = new SetupDescriptor.CarPlacement { carIdentifier = cut, marker = original.marker,
                    wreck = original.wreck, oiled = original.oiled, load = original.load, loadPercent = original.loadPercent };
                plan.Add((placement, selected.Value));
                ModDiagnosticLog.Write("NEW GAME", $"{area.name}: {(power ? "power/tenders" : "yard cars")} {string.Join(", ", cut)} -> {Graph.Shared.LocationToString(selected.Value)}; length={length:N1} m.");
            }
        }
        return plan;
    }
}
