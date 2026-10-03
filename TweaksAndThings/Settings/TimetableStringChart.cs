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
    }

    internal static void Build(UIPanelBuilder builder, View view)
    {
        builder.RebuildOnEvent<TimetableDidChange>();
        builder.AddLabel("STRING CHART  ·  Time →  /  Locations ↓");
        builder.AddLabel("Solid blue/amber: plan · Green: recorded actual · Dashed: live forecast · Named marker: current position · White diamonds: scheduled meets");
        builder.AddLabel("Blue: eastbound · Amber: westbound · Red: current time");
        builder.HStack(row =>
        {
            row.AddButtonCompact(view.Zoom ? "Fit day" : "Expand time scale", () => { view.Zoom = !view.Zoom; builder.Rebuild(); });
            if (TimetableController.CanEdit)
                row.AddButtonCompact("Edit Timetable", () => TimetableEditorWindow.Shared.Show());
        });
        builder.AddLabel("Click a train line or symbol for its times and meets. Station spacing is schematic; crossings alone are not scheduled meets.");
        builder.HVScrollView(content =>
        {
            var controller = TimetableController.Shared;
            if (controller.Current == null || controller.Current.Trains.Count == 0)
            { content.AddLabelEmptyState(controller.HasError ? "Timetable has errors" : "Empty timetable"); return; }
            var timetable = controller.Current.ToAbsolute();
            TimetableHistory.IncludeArchived(timetable);
            foreach (var branch in controller.branches)
            {
                TimetableWindow.GetTimetableStationsAndTrains(controller, timetable, branch, out var stations, out var west, out var east);
                if (stations.Count < 2 || west.Count + east.Count == 0) continue;
                var trains = east.Concat(west).ToList();
                content.AddLabel(stations[0].DisplayName + " — " + stations[stations.Count - 1].DisplayName);
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
                var go = new GameObject("Timetable string chart", typeof(RectTransform), typeof(LayoutElement));
                go.transform.SetParent(content._container, false);
                var chart = go.AddComponent<TimetableStringChartGraphic>();
                chart.Initialize(stations, trains, view.Train.Value, view.Zoom,
                    name => { view.Train.Value = name; builder.Rebuild(); });
                var selected = trains.FirstOrDefault(t => t.Name == view.Train.Value);
                if (selected != null)
                {
                    content.AddLabel(selected.DisplayStringLong + (NpcTimetableRegistry.IsProtected(selected.Name) ? " · NPC schedule locked" : ""));
                    if (!controller.Current.Trains.ContainsKey(selected.Name)) content.AddLabel("Recorded completed train · retained for today's chart");
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
}

// A single mesh for the grid and paths; no per-frame UI rebuild or object creation.
internal sealed class TimetableStringChartGraphic : MaskableGraphic, IPointerClickHandler
{
    private const float Left = 175, Top = 36, Row = 46;
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
    private sealed class Path
    {
        internal string Name = "";
        internal Color Color;
        internal float Width;
        internal readonly List<Vector2> Points = new();
        internal readonly List<Vector2> Meets = new();
    }

    internal void Initialize(IReadOnlyList<TimetableStation> stations, List<Timetable.Train> trains, string selected, bool zoom, Action<string> onSelect)
    {
        select = onSelect;
        this.stations = stations; this.trains = trains; this.selected = selected;
        plotWidth = zoom ? 2400 : 1200;
        height = Top + (stations.Count - 1) * Row + 35;
        var layout = GetComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = Left + plotWidth + 25;
        layout.minHeight = layout.preferredHeight = height;
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.sizeDelta = new Vector2(layout.preferredWidth, height);
        raycastTarget = true;
        for (int i = 0; i < stations.Count; i++) Label(stations[i].DisplayName, 4, Top + i * Row - 10, Left - 10, 20, Color.white);
        for (int hour = 0; hour <= 24; hour++)
            Label($"{hour:00}:00", Left + plotWidth * hour / 24 - 18, 4, 45, 22, Color.white);
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
                        Left + (float)(arrival % 1440) / 1440 * plotWidth + 5, Top + station * Row - 23, 120, 20, path.Color);
            }
            if (path.Points.Count > 0) paths.Add(path);
        }
        currentMinute = (float)(TimeWeather.Now.TotalSeconds / 60 % 1440);
        SetVerticesDirty();
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

    private Vector2 Position(float minute, float station) => new(Left + minute / 1440 * plotWidth, -Top - station * Row);

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (height <= 0) return;
        for (int hour = 0; hour <= 24; hour++)
            Line(mesh, new Vector2(Left + plotWidth * hour / 24, -Top), new Vector2(Left + plotWidth * hour / 24, -height + 25), 1, new Color(1, 1, 1, hour % 3 == 0 ? 0.25f : 0.1f));
        for (float y = Top; y <= height - 35; y += Row)
            Line(mesh, new Vector2(Left, -y), new Vector2(Left + plotWidth, -y), 1, new Color(1, 1, 1, 0.2f));
        foreach (var path in paths)
        {
            for (int i = 1; i < path.Points.Count; i++)
                EachSegment(path.Points[i - 1], path.Points[i], (a, b) => Line(mesh, a, b, path.Width, path.Color));
            foreach (var point in path.Points)
            {
                var p = Position(point.x % 1440, point.y);
                Line(mesh, p - new Vector2(0, 3), p + new Vector2(0, 3), path.Width + 2, path.Color);
            }
            foreach (var meet in path.Meets)
            {
                var p = Position(meet.x % 1440, meet.y);
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
        Line(mesh, Position(currentMinute, 0), new Vector2(Left + currentMinute / 1440 * plotWidth, -height + 25), 2, new Color(1, 0.35f, 0.4f));
        foreach (var marker in positions)
        {
            Line(mesh, marker.point - new Vector2(0, 6), marker.point + new Vector2(0, 6), 12, Color.white);
            Line(mesh, marker.point - new Vector2(0, 4), marker.point + new Vector2(0, 4), 8, marker.color);
        }
    }

    private void EachSegment(Vector2 a, Vector2 b, Action<Vector2, Vector2> draw)
    {
        int firstDay = (int)Math.Floor(Math.Min(a.x, b.x) / 1440);
        int lastDay = (int)Math.Floor(Math.Max(a.x, b.x) / 1440);
        for (int day = firstDay; day <= lastDay; day++)
            if (TimetableChartGeometry.Clip(a.x - day * 1440, a.y, b.x - day * 1440, b.y, out var x1, out var y1, out var x2, out var y2))
                draw(Position((float)x1, (float)y1), Position((float)x2, (float)y2));
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
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out var point)) return;
        // Local coordinates are relative to the pivot; our chart uses its top-left.
        point -= new Vector2(-rectTransform.rect.width * rectTransform.pivot.x, rectTransform.rect.height * (1 - rectTransform.pivot.y));
        float best = 9; string? name = null;
        foreach (var path in paths.Concat(forecasts))
        {
            foreach (var stop in path.Points)
            { float distance = Vector2.Distance(point, Position(stop.x % 1440, stop.y)); if (distance < best) { best = distance; name = path.Name; } }
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
        foreach (var train in trains) {
            var past = new Path { Name = train.Name, Color = new Color(0.35f, 1, 0.55f), Width = train.Name == selected ? 4 : 2.5f };
            TimetableHistorySample? previous = null;
            foreach (var sample in TimetableHistory.Samples(train.Name).Where(s => s.Minute >= day && s.Minute < day + 1440)) {
                double? row = TimetableHistoryPolicy.Row(sample, codes);
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
            var forecast = TimetableLiveForecast.Get(train);
            if (forecast == null) continue;
            var color = train.Direction == Timetable.Direction.East ? new Color(0.3f, 0.75f, 1) : new Color(1, 0.7f, 0.3f);
            var path = new Path { Name = train.Name, Color = Color.Lerp(color, Color.white, 0.35f), Width = train.Name == selected ? 3 : 2 };
            float? row = TimetableLiveForecast.Row(forecast, stations);
            if (row.HasValue)
            {
                path.Points.Add(new Vector2((float)forecast.Now, row.Value));
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
                path.Points.Add(new Vector2((float)stop.Arrival, index));
                if (stop.Departure > stop.Arrival) path.Points.Add(new Vector2((float)stop.Departure, index));
            }
            if (path.Points.Count > 1) forecasts.Add(path);
        }
        SetVerticesDirty();
    }
}
