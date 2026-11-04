using System;
using HarmonyLib;
using Track;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class WorldPatchReadiness
{
    private static float changedAt;
    internal static bool WorldLoaded { get; set; }
    internal static int Revision { get; private set; }
    internal static void Changed() { Revision++; changedAt = Time.realtimeSinceStartup; }
    internal static bool Ready => WorldLoaded && Graph.Shared?.HasPopulatedCollections == true && Time.realtimeSinceStartup - changedAt >= 0.5f;
}
