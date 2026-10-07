using System;
using System.Collections.Generic;
using System.Linq;
using Model.Ops.Timetable;
using Track;

namespace RMROC451.TweaksAndThings;

// Physical connectivity ignores ownership, switch position and occupancy.
// Disabled track (including an unfinished bridge) is not traversable.
internal sealed class TimetableTrackConnectivity
{
    private static TimetableTrackConnectivity? cached;
    private static Graph? cachedGraph;
    private static int cachedFrame = -1;
    internal static TimetableTrackConnectivity Current
    {
        get
        {
            if (cached == null || cachedGraph != Graph.Shared || cachedFrame != UnityEngine.Time.frameCount)
            {
                cached = new TimetableTrackConnectivity(); cachedGraph = Graph.Shared;
                cachedFrame = UnityEngine.Time.frameCount;
            }
            return cached;
        }
    }
    private readonly Dictionary<string, int> components;
    private readonly HashSet<int> owned;
    private readonly Dictionary<string, HashSet<int>> groups;
    private readonly Dictionary<TimetableStation, HashSet<int>> stationComponents = new();

    internal TimetableTrackConnectivity()
    {
        var segments = Graph.Shared?.Segments.ToList() ?? new List<TrackSegment>();
        components = TimetableConnectivityPolicy.Components(segments.Select(s =>
            new TimetableConnectivityPolicy.Edge(s.id, s.a.id, s.b.id, s.GroupEnabled, s.Available)), out owned);
        groups = segments.Where(s => components.ContainsKey(s.id)).GroupBy(s => s.groupId)
            .ToDictionary(g => g.Key, g => g.Select(s => components[s.id]).ToHashSet());
    }

    private HashSet<int> For(TimetableStation station)
    {
        if (stationComponents.TryGetValue(station, out var known)) return known;
        var result = new HashSet<int>();
        // A station's actual platform/interchange span is more precise than a
        // large unlock area's groups and must not snap across a broken bridge.
        if (ThroughTrafficSpawner.TryGetStationLocation(station, out var location))
        {
            if (components.TryGetValue(location.segment.id, out int component)) result.Add(component);
        }
        if (result.Count == 0 && station.mapFeature != null)
        {
            var feature = station.mapFeature;
            var ids = feature.trackGroupsAvailableOnUnlock ?? Array.Empty<string>();
            if (ids.Length == 0) ids = feature.trackGroupsEnableOnUnlock ?? Array.Empty<string>();
            foreach (var id in ids)
                if (groups.TryGetValue(id, out var matches)) result.UnionWith(matches);
        }
        stationComponents[station] = result;
        return result;
    }

    internal bool Visible(TimetableStation station) => For(station).Overlaps(owned);
    internal bool Connected(TimetableStation a, TimetableStation b) => For(a).Overlaps(For(b));
}
