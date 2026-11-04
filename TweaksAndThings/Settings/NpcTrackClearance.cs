using System;
using Track;

namespace RMROC451.TweaksAndThings;

internal static class NpcTrackClearance
{
    internal static float Fouling(TrackNode? node)
    {
        // Route steps also contain ordinary joints and terminal nodes. The
        // game's fouling calculator accepts only decoded switch nodes.
        if (node == null || !Graph.Shared.DecodeSwitchAt(node, out _, out _, out _)) return 0;
        try { return Graph.Shared.CalculateFoulingDistance(node); }
        catch (ArgumentException)
        {
            NpcTrafficDiagnostics.Detail("fouling:" + node.id,
                "Unable to calculate switch clearance for " + node.id + "; reserving 50 m.");
            return 50;
        }
    }
}
