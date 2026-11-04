using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings;

// Reservations are atomic and are retained behind the engine until the tail clears.
internal sealed class NpcCorridorPolicy
{
    private readonly Dictionary<string, HashSet<string>> claims = new();
    internal IEnumerable<string> Owners => claims.Keys.ToList();
    internal bool Covers(string owner, IEnumerable<string> segments) => claims.TryGetValue(owner, out var claim) && segments.All(claim.Contains);
    internal string? OwnerOf(string segment) => claims.FirstOrDefault(c => c.Value.Contains(segment)).Key;
    // Negotiate distant claims atomically; supplied firm corridors include each
    // train's occupied tail and a reachable refuge beyond its braking distance.
    internal bool TryAdmit(IEnumerable<string> requested, IEnumerable<string> occupied,
        IReadOnlyDictionary<string, HashSet<string>> firm)
    {
        var next = requested.ToHashSet();
        if (next.Overlaps(occupied)) return false;
        var conflicts = claims.Where(c => c.Value.Overlaps(next)).ToList();
        if (conflicts.Any(c => !firm.TryGetValue(c.Key, out var keep) || keep.Overlaps(next))) return false;
        foreach (var claim in conflicts) claim.Value.IntersectWith(firm[claim.Key]);
        return true;
    }
    internal bool TryReserve(string owner, IEnumerable<string> requested, IEnumerable<string> occupiedByOwner,
        IEnumerable<string> occupiedByOthers)
    {
        var next = requested.ToHashSet();
        if (next.Overlaps(occupiedByOthers) || claims.Any(c => c.Key != owner && c.Value.Overlaps(next))) return false;
        if (claims.TryGetValue(owner, out var old)) next.UnionWith(old.Intersect(occupiedByOwner));
        claims[owner] = next;
        return true;
    }
    internal void RetainOccupied(string owner, IEnumerable<string> occupied)
    {
        if (claims.TryGetValue(owner, out var old)) {
            old.IntersectWith(occupied);
            if (old.Count == 0) claims.Remove(owner);
        }
    }
    internal void Remove(string owner) => claims.Remove(owner);
    internal void Clear() => claims.Clear();
}
