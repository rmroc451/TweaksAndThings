using System;
using System.Collections.Generic;
using System.Linq;
using Game.Events;
using Game;
using Model.Ops.Timetable;
using TMPro;
using UI;
using UI.Builder;
using UI.Timetable;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Timetable = Model.Ops.Timetable.Timetable;

namespace RMROC451.TweaksAndThings;

internal static class TimetableStringChart
{
    internal sealed class View
    {
        internal readonly UIState<string> Train = new("");
        internal bool Zoom;
        internal bool DistanceSpacing;
        internal Track.Graph? DistanceGraph;
        internal int DistanceRevision = -1;
        internal readonly Dictionary<string, double> Distances = new();
        internal readonly Dictionary<string, string> MileLabels = new();
        internal readonly Dictionary<string, (string prefix, double miles)> MileValues = new();
    }

    internal static void Build(UIPanelBuilder builder, View view)
    {
        builder.RebuildOnEvent<TimetableDidChange>();
        builder.RebuildOnEvent<Game.Events.ProgressionStateDidChange>();
        builder.AddLabel("STRING CHART  ·  Time →  /  Locations ↓");
        builder.AddLabel("Solid blue/amber: plan · Green: recorded actual · Dashed: live forecast · Named marker: current position · White diamonds: scheduled meets");
        builder.AddLabel("Blue: eastbound · Amber: westbound · Red: current time · Grey italic locations: locked territory");
        builder.HStack(row =>
        {
            row.AddButtonCompact(view.Zoom ? "Fit schedule" : "Expand time scale", () => { view.Zoom = !view.Zoom; builder.Rebuild(); });
            row.AddButtonCompact(view.DistanceSpacing ? "Spacing: track distance" : "Spacing: even rows",
                () => { view.DistanceSpacing = !view.DistanceSpacing; builder.Rebuild(); })
                .Tooltip("Toggle distance spacing", "Space locations proportionally to their track distance across the displayed connected network.");
            if (TimetableController.CanEdit)
                row.AddButtonCompact("Edit Timetable", () => TimetableEditorWindow.Shared.Show());
        });
        builder.AddLabel("Click a train for its times and meets. Drag to pan; wheel over the graph to zoom. Location labels and time headers stay pinned.");
        builder.VScrollView(content =>
        {
            var controller = TimetableController.Shared;
            var timetable = controller.Current?.ToAbsolute() ?? new Timetable(new Dictionary<string, Timetable.Train>());
            // Saved NPC plans remain visible even before the company unlocks
            // its player timetable feature. Use absolute times for both sources.
            NpcTimetableRegistry.IncludeSavedPlans(timetable);
            if (timetable.Trains.Count == 0)
            { content.AddLabelEmptyState(controller.HasError ? "Timetable has errors" : "Empty timetable"); return; }
            TimetableHistory.IncludeArchived(timetable);
            var connectivity = new TimetableTrackConnectivity();
            var branches = controller.branches.Select(branch => controller.GetAllStations(branch,
                includeDisabled: true, includeDuplicates: true).ToList()).ToList();
            var visible = branches.Select(list => list.Where(connectivity.Visible).Reverse().ToList()).ToList();
            var distances = visible.Select(list => view.DistanceSpacing ? Distances(view, list) : null).ToList();
            double totalMeters = distances.Where(d => d != null).Sum(d => d!.Sum());
            double pixelsPerMeter = totalMeters > 0 ? distances.Where(d => d != null).Sum(d => d!.Length * 46d) / totalMeters : 0;
            if (view.DistanceSpacing && totalMeters > 0)
                content.AddLabel($"Distance scale: {totalMeters / 1609.344:N1} mi across the displayed connected network.");
            for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
            {
                // Use the complete branch, including stations outside owned territory.
                // Reverse native east-to-west order: Andrews above Sylva on the mainline.
                var allStations = branches[branchIndex];
                var stations = visible[branchIndex];
                if (stations.Count < allStations.Count)
                    ModDiagnosticLog.Write("TIMETABLE", "Locations hidden outside the physically connected playable railroad: " +
                        string.Join(", ", allStations.Where(s => !connectivity.Visible(s)).Select(s => s.code)),
                        "chart-connectivity:" + allStations.FirstOrDefault()?.code, 60);
                var codes = new HashSet<string>(stations.Where(s => !s.IsBranchJunctionDuplicate).Select(s => s.code));
                var trains = timetable.Trains.Values.Where(t => t.Entries.Any(e => codes.Contains(e.Station)))
                    .OrderBy(t => t.TrainClass).ThenBy(t => t.Name).ToList();
                if (stations.Count < 2 || trains.Count == 0) continue;
                content.AddLabel(stations[0].DisplayName + " — " + stations[stations.Count - 1].DisplayName);
                if (view.DistanceSpacing && distances[branchIndex] == null)
                    content.AddLabel("Track distance unavailable for this branch; using evenly spaced rows.");
                // Small groups keep the selector usable even with a busy NPC timetable.
                for (int offset = 0; offset < trains.Count; offset += 5)
                {
                    var group = trains.Skip(offset).Take(5).ToList();
                    content.HStack(row =>
                    {
                        foreach (var train in group)
                            row.AddButtonCompact((view.Train.Value == train.Name ? "● " : "") + train.DisplayStringShort,
                                () => { view.Train.Value = train.Name; builder.Rebuild(); })
                                .Tooltip(train.DisplayStringLong, "Select to show station times and scheduled meets.");
                    });
                }
                var panel = new GameObject("Chart explorer", typeof(RectTransform), typeof(LayoutElement), typeof(ScrollRect));
                panel.transform.SetParent(content._container, false);
                var panelLayout = panel.GetComponent<LayoutElement>();
                panelLayout.minWidth = 400; panelLayout.preferredWidth = -1; panelLayout.flexibleWidth = 1;
                panelLayout.minHeight = panelLayout.preferredHeight = 520;
                var viewport = new GameObject("Chart viewport", typeof(RectTransform), typeof(RectMask2D));
                viewport.transform.SetParent(panel.transform, false);
                var viewportRect = viewport.GetComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
                var go = new GameObject("Timetable string chart", typeof(RectTransform), typeof(LayoutElement));
                go.transform.SetParent(viewport.transform, false);
                var scroll = panel.GetComponent<ScrollRect>();
                scroll.viewport = viewportRect; scroll.content = go.GetComponent<RectTransform>();
                scroll.horizontal = scroll.vertical = true; scroll.inertia = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                var chart = go.AddComponent<TimetableStringChartGraphic>();
                chart.Initialize(stations, trains, view.Train.Value, view.Zoom,
                    name => { view.Train.Value = name; builder.Rebuild(); },
                    view.DistanceSpacing && distances[branchIndex] != null && pixelsPerMeter > 0 ?
                        TimetableChartGeometry.DistanceRows(distances[branchIndex]!, pixelsPerMeter) : null,
                    stations.Select(s => MileLabel(view, s)).ToList());
                chart.PinAxes(scroll);
                var selected = trains.FirstOrDefault(t => t.Name == view.Train.Value);
                if (selected != null)
                {
                    content.AddLabel(selected.DisplayStringLong + (NpcTimetableRegistry.IsProtected(selected.Name) ? " · NPC schedule locked" : ""));
                    if (controller.Current?.Trains.ContainsKey(selected.Name) != true && !NpcTimetableRegistry.IsProtected(selected.Name))
                        content.AddLabel("Recorded completed train · retained for today's chart");
                    string description = NpcDailyTrafficPlans.Description(selected.Name);
                    if (description.Length > 0) content.AddLabel(description);
                    content.AddLabel(() => TimetableLiveForecast.Status(selected.Name), UIPanelBuilder.Frequency.Periodic);
                    content.AddLabel(() => TimetableHistory.Status(selected.Name), UIPanelBuilder.Frequency.Periodic);
                    foreach (var entry in selected.Entries)
                    {
                        var station = stations.FirstOrDefault(s => s.code == entry.Station);
                        if (station == null) continue;
                        string arrival = entry.ArrivalTime.HasValue ? "Arr " + TimetableChartGeometry.Clock(entry.ArrivalTime.Value.Minutes) + " · " : "";
                        string plan = station.DisplayName + "  ·  " + arrival + "Dep " + TimetableChartGeometry.Clock(entry.DepartureTime.Minutes) +
                            (entry.Meets.Count == 0 ? "" : "  ·  Meet " + string.Join(", ", entry.Meets));
                        content.AddLabel(() => plan + "  ·  " + TimetableLiveForecast.StopText(selected.Name, entry.Station), UIPanelBuilder.Frequency.Periodic);
                    }
                }
            }
        });
    }

    private static double[]? Distances(View view, IReadOnlyList<TimetableStation> stations)
    {
        if (view.DistanceGraph != Track.Graph.Shared || view.DistanceRevision != WorldPatchReadiness.Revision)
        {
            view.DistanceGraph = Track.Graph.Shared; view.DistanceRevision = WorldPatchReadiness.Revision;
            view.Distances.Clear();
            view.MileLabels.Clear();
            view.MileValues.Clear();
        }
        var result = new double[Math.Max(0, stations.Count - 1)];
        for (int i = 0; i < result.Length; i++)
        {
            var a = stations[i]; var b = stations[i + 1];
            if (a.code == b.code) continue;
            string key = string.CompareOrdinal(a.code, b.code) < 0 ? a.code + ":" + b.code : b.code + ":" + a.code;
            if (!view.Distances.TryGetValue(key, out double meters))
            {
                MileLabel(view, a); MileLabel(view, b);
                if (view.MileValues.TryGetValue(a.code, out var ma) && view.MileValues.TryGetValue(b.code, out var mb) && ma.prefix == mb.prefix)
                    meters = Math.Abs(ma.miles - mb.miles) * 1609.344;
                else if (ThroughTrafficSpawner.TryGetStationLocation(a, out var from) &&
                    ThroughTrafficSpawner.TryGetStationLocation(b, out var to) &&
                    NpcTrainOperations.Route(from, to, out _, out float length)) meters = length;
                else if (a.mapFeature != null && b.mapFeature != null)
                    meters = Vector3.Distance(a.mapFeature.transform.position, b.mapFeature.transform.position);
                else return null;
                view.Distances[key] = meters;
            }
            result[i] = meters;
        }
        return result;
    }

    private static string MileLabel(View view, TimetableStation station)
    {
        if (view.DistanceGraph != Track.Graph.Shared || view.DistanceRevision != WorldPatchReadiness.Revision)
            Distances(view, Array.Empty<TimetableStation>());
        if (view.MileLabels.TryGetValue(station.code, out var label)) return label;
        label = "MP —";
        if (ThroughTrafficSpawner.TryGetStationLocation(station, out var location))
        {
            var candidates = Resources.FindObjectsOfTypeAll<Helpers.MilePost>()
                .Where(p => p.gameObject.scene.IsValid())
                .Select(p =>
                {
                    var marker = p.TrackMarker?.Location ?? Track.Location.Invalid;
                    if (!marker.IsValid) Track.Graph.Shared.TryGetLocationFromGamePoint(
                        Helpers.WorldTransformer.WorldToGame(p.transform.position), 100f, out marker);
                    return (post: p, location: marker);
                }).Where(p => p.location.IsValid)
                .OrderBy(p => Vector3.SqrMagnitude(p.location.GetPosition() - location.GetPosition()))
                .Select(p => (post: p.post, distance: NpcTrainOperations.Route(location, p.location,
                    out _, out float meters) ? (double)meters : double.NaN))
                .Where(p => !double.IsNaN(p.distance));
            var posts = new List<(Helpers.MilePost post, double distance)>();
            foreach (var candidate in candidates)
            {
                posts.Add(candidate);
                // Usually the nearby markers suffice. Search farther only if
                // those do not provide a usable surveyed pair for this branch.
                if (posts.Count >= 8 && posts.Any(a => posts.Any(b => a.post.prefix == b.post.prefix &&
                    Math.Abs(a.post.mileage - b.post.mileage) > 0.01))) break;
            }
            posts = posts.OrderBy(p => p.distance).ToList();
            if (posts.Count > 1)
            {
                var a = posts[0];
                var b = posts.Skip(1).FirstOrDefault(p => p.post.prefix == a.post.prefix && Math.Abs(p.post.mileage - a.post.mileage) > 0.01);
                if (b.post != null)
                {
                    double miles = TimetableChartGeometry.Milepost(a.post.mileage, b.post.mileage, a.distance, b.distance);
                    view.MileValues[station.code] = (a.post.prefix, miles);
                    label = "MP " + a.post.prefix + miles.ToString("0.0");
                }
            }
        }
        view.MileLabels[station.code] = label;
        return label;
    }
}

// A single mesh for the grid and paths; no per-frame UI rebuild or object creation.
internal sealed class TimetableStringChartGraphic : MaskableGraphic, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerDownHandler
{
    private const float Left = 250, Top = 36, Row = 46;
    private float plotWidth;
    private float height;
    private Action<string>? select;
    private readonly List<Path> paths = new();
    private readonly List<Path> forecasts = new();
    private readonly List<Path> actuals = new();
    private readonly List<(Vector2 point, Color color)> positions = new();
    private readonly Dictionary<string, TextMeshProUGUI> liveLabels = new();
    private IReadOnlyList<TimetableStation> stations = Array.Empty<TimetableStation>();
    private List<Timetable.Train> trains = new();
    private string selected = "";
    private float nextRefresh;
    private float currentMinute;
    private double minimumMinute, maximumMinute = 1440;
    private IReadOnlyList<double> rowPositions = Array.Empty<double>();
    private ScrollRect? explorer;
    private bool dragged;
    private readonly List<(RectTransform rect, Vector2 position)> locationLabels = new();
    private readonly List<(RectTransform rect, Vector2 position)> timeLabels = new();
    private sealed class Path
    {
        internal string Name = "";
        internal Color Color;
        internal float Width;
        internal readonly List<Vector2> Points = new();
        internal readonly List<Vector2> Meets = new();
    }

    internal void Initialize(IReadOnlyList<TimetableStation> stations, List<Timetable.Train> trains, string selected, bool zoom, Action<string> onSelect,
        IReadOnlyList<double>? distanceRows = null, IReadOnlyList<string>? mileLabels = null)
    {
        select = onSelect;
        this.stations = stations; this.trains = trains; this.selected = selected;
        plotWidth = zoom ? 2400 : 1200;
        var plannedTimes = new List<double>();
        foreach (var train in trains) {
            double previous = double.NegativeInfinity;
            foreach (var entry in train.Entries) {
                double arrival = TimetableChartGeometry.Unwrap(entry.ArrivalTime?.Minutes ?? entry.DepartureTime.Minutes, previous);
                previous = TimetableChartGeometry.Unwrap(entry.DepartureTime.Minutes, arrival);
                plannedTimes.Add(arrival); plannedTimes.Add(previous);
            }
        }
        minimumMinute = plannedTimes.Count == 0 ? 0 : plannedTimes.Min() - 60;
        maximumMinute = plannedTimes.Count == 0 ? 1440 : plannedTimes.Max() + 60;
        rowPositions = distanceRows ?? Enumerable.Range(0, stations.Count).Select(i => i * (double)Row).ToArray();
        height = Top + (float)rowPositions.LastOrDefault() + 35;
        var layout = GetComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = Left + plotWidth + 25;
        layout.minHeight = layout.preferredHeight = height;
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.sizeDelta = new Vector2(layout.preferredWidth, height);
        raycastTarget = true;
        for (int i = 0; i < stations.Count; i++)
        {
            bool locked = !stations[i].IsEnabled;
            var label = Label(stations[i].DisplayName, 4, Top + RowY(i) - 10, Left - 85, 20,
                locked ? new Color(0.6f, 0.6f, 0.6f) : Color.white);
            if (locked) label.fontStyle = FontStyles.Italic;
            var mile = Label(mileLabels?[i] ?? "MP —", Left - 78, Top + RowY(i) - 10, 72, 20,
                locked ? new Color(0.6f, 0.6f, 0.6f) : Color.white);
            if (locked) mile.fontStyle = FontStyles.Italic;
            locationLabels.Add((label.rectTransform, label.rectTransform.anchoredPosition));
            locationLabels.Add((mile.rectTransform, mile.rectTransform.anchoredPosition));
        }
        for (int hour = (int)Math.Ceiling(minimumMinute / 60); hour * 60 <= maximumMinute; hour++)
        {
            var label = Label(TimetableChartGeometry.Clock(hour * 60), Left + (float)((hour * 60 - minimumMinute) / (maximumMinute - minimumMinute)) * plotWidth - 18, 4, 45, 22, Color.white);
            timeLabels.Add((label.rectTransform, label.rectTransform.anchoredPosition));
        }
        foreach (var train in trains)
        {
            var path = new Path { Name = train.Name,
                Color = train.Direction == Timetable.Direction.East ? new Color(0.3f, 0.75f, 1) : new Color(1, 0.7f, 0.3f),
                Width = train.Name == selected ? 4 : 2 };
            double previous = double.NegativeInfinity;
            foreach (var entry in train.Entries)
            {
                // Update the running time even for stops outside this branch.
                double arrival = TimetableChartGeometry.Unwrap(entry.ArrivalTime?.Minutes ?? entry.DepartureTime.Minutes, previous);
                double departure = TimetableChartGeometry.Unwrap(entry.DepartureTime.Minutes, arrival);
                previous = departure;
                int station = -1;
                for (int i = 0; i < stations.Count; i++) if (stations[i].code == entry.Station) { station = i; break; }
                if (station < 0)
                {
                    // Do not invent a line across a branch the train leaves and later re-enters.
                    if (path.Points.Count > 0) { paths.Add(path); path = new Path { Name = path.Name, Color = path.Color, Width = path.Width }; }
                    continue;
                }
                path.Points.Add(new Vector2((float)arrival, station));
                if (departure != arrival) path.Points.Add(new Vector2((float)departure, station));
                if (entry.Meets.Count > 0) path.Meets.Add(new Vector2((float)departure, station));
                if (train.Name == selected)
                    Label(TimetableChartGeometry.Clock(arrival) + (arrival != departure ? " / " + TimetableChartGeometry.Clock(departure) : ""),
                        PixelPosition((float)arrival, 0).x + 5, Top + RowY(station) - 23, 120, 20, path.Color);
            }
            if (path.Points.Count > 0) paths.Add(path);
        }
        currentMinute = (float)(TimeWeather.Now.TotalSeconds / 60 % 1440);
        SetVerticesDirty();
    }

    internal void PinAxes(ScrollRect scroll)
    {
        explorer = scroll;
        RectTransform Layer(string name, bool top)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            go.transform.SetParent(scroll.viewport, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, top ? 1 : 0); rect.anchorMax = new Vector2(top ? 1 : 0, 1);
            rect.pivot = new Vector2(0, 1); rect.sizeDelta = top ? new Vector2(0, Top) : new Vector2(Left, 0);
            var image = go.GetComponent<Image>(); image.color = new Color(0.12f, 0.14f, 0.17f, 1); image.raycastTarget = false;
            return rect;
        }
        var left = Layer("Pinned locations", false);
        var header = Layer("Pinned clock", true);
        foreach (var label in locationLabels) label.rect.SetParent(left, false);
        foreach (var label in timeLabels) label.rect.SetParent(header, false);
        var corner = new GameObject("Axis corner", typeof(RectTransform), typeof(Image));
        corner.transform.SetParent(scroll.viewport, false);
        var cornerRect = corner.GetComponent<RectTransform>();
        cornerRect.anchorMin = cornerRect.anchorMax = cornerRect.pivot = new Vector2(0, 1);
        cornerRect.sizeDelta = new Vector2(Left, Top);
        corner.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.17f, 1);
        corner.GetComponent<Image>().raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (explorer == null) return;
        foreach (var label in locationLabels)
            label.rect.anchoredPosition = new Vector2(label.position.x, rectTransform.anchoredPosition.y + label.position.y * rectTransform.localScale.y);
        foreach (var label in timeLabels)
            label.rect.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x + label.position.x * rectTransform.localScale.x, label.position.y);
    }
    public void OnBeginDrag(PointerEventData data) { dragged = true; explorer?.OnBeginDrag(data); }
    public void OnPointerDown(PointerEventData data) => dragged = false;
    public void OnDrag(PointerEventData data) => explorer?.OnDrag(data);
    public void OnEndDrag(PointerEventData data) => explorer?.OnEndDrag(data);
    public void OnScroll(PointerEventData data)
    {
        if (explorer == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(explorer.viewport, data.position, data.pressEventCamera, out var point)) return;
        // Convert viewport coordinates to its upper-left origin.
        point -= new Vector2(explorer.viewport.rect.xMin, explorer.viewport.rect.yMax);
        float old = rectTransform.localScale.x;
        float zoom = Mathf.Clamp(old * Mathf.Pow(1.15f, data.scrollDelta.y), 0.5f, 4f);
        rectTransform.anchoredPosition = point - (point - rectTransform.anchoredPosition) * (zoom / old);
        rectTransform.localScale = new Vector3(zoom, zoom, 1);
        explorer.StopMovement();
    }

    private TextMeshProUGUI Label(string text, float x, float y, float width, float labelHeight, Color tint)
    {
        var go = new GameObject("Chart label", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = transform.parent.GetComponentInChildren<TextMeshProUGUI>()?.font ?? TMP_Settings.defaultFontAsset;
        label.text = text; label.fontSize = 12; label.color = tint;
        label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, labelHeight);
        return label;
    }

    private float RowY(float station) => (float)TimetableChartGeometry.RowPosition(rowPositions, station);
    private Vector2 Position(float minute, float station) => PixelPosition(minute, RowY(station));
    private Vector2 PixelPosition(float minute, float y) => new(Left + (float)((minute - minimumMinute) / (maximumMinute - minimumMinute)) * plotWidth, -Top - y);

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (height <= 0) return;
        for (int hour = (int)Math.Ceiling(minimumMinute / 60); hour * 60 <= maximumMinute; hour++)
            Line(mesh, new Vector2(Left + (float)((hour * 60 - minimumMinute) / (maximumMinute - minimumMinute)) * plotWidth, -Top), new Vector2(Left + (float)((hour * 60 - minimumMinute) / (maximumMinute - minimumMinute)) * plotWidth, -height + 25), 1, new Color(1, 1, 1, hour % 3 == 0 ? 0.25f : 0.1f));
        foreach (double row in rowPositions)
            Line(mesh, new Vector2(Left, -Top - (float)row), new Vector2(Left + plotWidth, -Top - (float)row), 1, new Color(1, 1, 1, 0.2f));
        foreach (var path in paths)
        {
            for (int i = 1; i < path.Points.Count; i++)
                EachSegment(path.Points[i - 1], path.Points[i], (a, b) => Line(mesh, a, b, path.Width, path.Color));
            foreach (var point in path.Points)
            {
                var p = Position(point.x, point.y);
                Line(mesh, p - new Vector2(0, 3), p + new Vector2(0, 3), path.Width + 2, path.Color);
            }
            foreach (var meet in path.Meets)
            {
                var p = Position(meet.x, meet.y);
                var top = p + new Vector2(0, 7); var right = p + new Vector2(7, 0);
                var bottom = p - new Vector2(0, 7); var left = p - new Vector2(7, 0);
                Line(mesh, top, right, 2, Color.white); Line(mesh, right, bottom, 2, Color.white);
                Line(mesh, bottom, left, 2, Color.white); Line(mesh, left, top, 2, Color.white);
            }
        }
        foreach (var path in actuals)
            for (int i = 1; i < path.Points.Count; i++)
                EachSegment(path.Points[i - 1], path.Points[i], (a, b) => Line(mesh, a, b, path.Width, path.Color));
        foreach (var path in forecasts)
            for (int i = 1; i < path.Points.Count; i++)
                EachSegment(path.Points[i - 1], path.Points[i], (a, b) => Dashed(mesh, a, b, path.Width, path.Color));
        Line(mesh, Position(currentMinute, 0), new Vector2(PixelPosition(currentMinute, 0).x, -height + 25), 2, new Color(1, 0.35f, 0.4f));
        foreach (var marker in positions)
        {
            Line(mesh, marker.point - new Vector2(0, 6), marker.point + new Vector2(0, 6), 12, Color.white);
            Line(mesh, marker.point - new Vector2(0, 4), marker.point + new Vector2(0, 4), 8, marker.color);
        }
    }

    private void EachSegment(Vector2 a, Vector2 b, Action<Vector2, Vector2> draw)
    {
        a.y = RowY(a.y); b.y = RowY(b.y);
        if (TimetableChartGeometry.ClipRange(a.x, a.y, b.x, b.y, minimumMinute, maximumMinute,
            out var x1, out var y1, out var x2, out var y2))
            draw(PixelPosition((float)x1, (float)y1), PixelPosition((float)x2, (float)y2));
    }

    private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
    {
        if ((b - a).sqrMagnitude < 0.001f) return;
        var delta = (b - a).normalized;
        var normal = new Vector2(-delta.y, delta.x) * width / 2;
        int index = mesh.currentVertCount;
        mesh.AddVert(a - normal, tint, Vector2.zero); mesh.AddVert(a + normal, tint, Vector2.zero);
        mesh.AddVert(b + normal, tint, Vector2.zero); mesh.AddVert(b - normal, tint, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
    }

    private static void Dashed(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
    {
        float length = Vector2.Distance(a, b);
        var direction = (b - a).normalized;
        for (float d = 0; d < length; d += 12)
            Line(mesh, a + direction * d, a + direction * Mathf.Min(length, d + 7), width, tint);
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (dragged) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out var point)) return;
        // Local coordinates are relative to the pivot; our chart uses its top-left.
        point -= new Vector2(-rectTransform.rect.width * rectTransform.pivot.x, rectTransform.rect.height * (1 - rectTransform.pivot.y));
        float best = 9; string? name = null;
        foreach (var path in paths.Concat(forecasts))
        {
            foreach (var stop in path.Points)
            { float distance = Vector2.Distance(point, Position(stop.x, stop.y)); if (distance < best) { best = distance; name = path.Name; } }
            for (int i = 1; i < path.Points.Count; i++)
                EachSegment(path.Points[i - 1], path.Points[i], (a, b) =>
                {
                    var delta = b - a;
                    float t = delta.sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
                    float distance = Vector2.Distance(point, a + delta * t);
                    if (distance < best) { best = distance; name = path.Name; }
                });
        }
        if (name != null) select?.Invoke(name);
    }

    private void Update()
    {
        TimetableLiveForecast.Advance();
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1;
        currentMinute = (float)(TimeWeather.Now.TotalSeconds / 60 % 1440);
        forecasts.Clear(); actuals.Clear(); positions.Clear();
        double day = Math.Floor(TimeWeather.Now.TotalSeconds / 86400) * 1440;
        var codes = stations.Select(s => s.code).ToList();
        var live = trains.ToDictionary(t => t.Name, TimetableLiveForecast.Get);
        foreach (var train in trains) {
            var past = new Path { Name = train.Name, Color = new Color(0.35f, 1, 0.55f), Width = train.Name == selected ? 4 : 2.5f };
            TimetableHistorySample? previous = null;
            foreach (var sample in TimetableHistory.Samples(train.Name).Where(s => s.Minute >= day && s.Minute < day + 1440)) {
                double? row = TimetableHistoryPolicy.Row(sample, codes);
                if (live.TryGetValue(train.Name, out var current) && current != null && current.VehicleId == sample.Vehicle)
                    row = TimetableLiveForecast.HistoryRow(current, sample, stations) ?? row;
                if (!row.HasValue || previous != null && !TimetableHistoryPolicy.Connect(previous, sample)) {
                    if (past.Points.Count > 1) actuals.Add(past);
                    past = new Path { Name = past.Name, Color = past.Color, Width = past.Width };
                }
                if (row.HasValue) past.Points.Add(new Vector2((float)(sample.Minute - day), (float)row.Value));
                previous = sample;
            }
            if (past.Points.Count > 1) actuals.Add(past);
        }
        foreach (var label in liveLabels.Values) label.gameObject.SetActive(false);
        foreach (var train in trains)
        {
            var forecast = live[train.Name];
            if (forecast == null) continue;
            var color = train.Direction == Timetable.Direction.East ? new Color(0.3f, 0.75f, 1) : new Color(1, 0.7f, 0.3f);
            var path = new Path { Name = train.Name, Color = Color.Lerp(color, Color.white, 0.35f), Width = train.Name == selected ? 3 : 2 };
            float? row = TimetableLiveForecast.Row(forecast, stations);
            if (row.HasValue)
            {
                path.Points.Add(new Vector2((float)(forecast.Now - day), row.Value));
                if (forecast.ShowPosition)
                {
                    var p = Position(currentMinute, row.Value);
                    positions.Add((p, color));
                    if (!liveLabels.TryGetValue(train.Name, out var label))
                        liveLabels[train.Name] = label = Label("", 0, 0, 175, 22, color);
                    label.text = train.Name + $" · {forecast.Speed:N0} mph";
                    label.rectTransform.anchoredPosition = p + new Vector2(8, -4);
                    label.gameObject.SetActive(true);
                }
            }
            foreach (var stop in forecast.Stops)
            {
                int index = -1;
                for (int i = 0; i < stations.Count; i++) if (stations[i].code == stop.Code) { index = i; break; }
                if (index < 0)
                {
                    if (path.Points.Count > 1) forecasts.Add(path);
                    path = new Path { Name = path.Name, Color = path.Color, Width = path.Width };
                    continue;
                }
                path.Points.Add(new Vector2((float)(stop.Arrival - day), index));
                if (stop.Departure > stop.Arrival) path.Points.Add(new Vector2((float)(stop.Departure - day), index));
            }
            if (path.Points.Count > 1) forecasts.Add(path);
        }
        SetVerticesDirty();
    }
}
