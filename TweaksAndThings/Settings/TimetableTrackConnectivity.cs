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
    private readonly Dictionary<TimetableStation, HashSet<int>> stationComponents = new();

    internal TimetableTrackConnectivity()
    {
        var segments = Graph.Shared?.Segments.ToList() ?? new List<TrackSegment>();
        components = TimetableConnectivityPolicy.Components(segments.Select(s =>
            new TimetableConnectivityPolicy.Edge(s.id, s.a.id, s.b.id, s.GroupEnabled, s.Available)), out owned);
    }

    private HashSet<int> For(TimetableStation station)
    {
        if (stationComponents.TryGetValue(station, out var known)) return known;
        var result = new HashSet<int>();
        // Resolve the station to its platform or interchange track. Broad
        // progression areas can span disconnected track, so avoid inferring
        // connectivity from the old timetable map-feature association.
        if (ThroughTrafficSpawner.TryGetStationLocation(station, out var location))
        {
            if (components.TryGetValue(location.segment.id, out int component)) result.Add(component);
        }
        stationComponents[station] = result;
        return result;
    }

    internal bool Visible(TimetableStation station) => For(station).Overlaps(owned);
    internal bool Connected(TimetableStation a, TimetableStation b) => For(a).Overlaps(For(b));
}
