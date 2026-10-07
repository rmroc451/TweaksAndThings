using System;
using HarmonyLib;
using Track;
using UnityEngine;
using UnityModManagerNet;
using System.Collections;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class WorldPatchReadiness
{
    private static readonly System.Reflection.FieldInfo started = AccessTools.Field(typeof(UnityModManager), "started");
    private static readonly System.Reflection.FieldInfo entries = AccessTools.Field(typeof(UnityModManager), "modEntries");
    private static float changedAt;
    internal static int Revision { get; private set; }
    internal static void Changed() { Revision++; changedAt = Time.realtimeSinceStartup; }
    internal static bool ModsLoaded => started?.GetValue(null) is bool ready && ready &&
        entries?.GetValue(null) is IEnumerable mods && mods.Cast<UnityModManager.ModEntry>().All(m => !m.Enabled || m.Started || m.ErrorOnLoading);
    internal static bool Ready => ModsLoaded && Graph.Shared?.HasPopulatedCollections == true && Time.realtimeSinceStartup - changedAt >= 0.5f;
}
