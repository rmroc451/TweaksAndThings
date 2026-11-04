using System.Collections.Generic;

namespace RMROC451.TweaksAndThings;

internal static class NpcBoardMarkerPolicy
{
    internal const string Prefix = "marker-tat-npc-";
    internal static string Key(int day, string symbol) => Prefix + day + "-" + symbol;
    internal static bool TryClaim(Dictionary<string, int> ledger, string board, string symbol, int day)
    {
        string id = board + ":" + symbol;
        if (ledger.TryGetValue(id, out int previous) && previous == day) return false;
        ledger[id] = day;
        return true;
    }
    internal static bool Expired(string key, int markerDay, int currentDay) => key.StartsWith(Prefix) && markerDay != currentDay;
}
