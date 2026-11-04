using System;
using System.Collections.Generic;
using System.Linq;
using Game.State;
using KeyValue.Runtime;
using Model.Ops.Timetable;
using Track.Signals.Panel;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcDispatchBoardMarkers
{
    private static CTCPanelMarkerManager[] boards = Array.Empty<CTCPanelMarkerManager>();
    private static float nextDiscovery;
    private static float nextSync;
    internal static void Reset() { boards = Array.Empty<CTCPanelMarkerManager>(); nextDiscovery = nextSync = 0; }

    private static void Discover()
    {
        if (Time.realtimeSinceStartup < nextDiscovery) return;
        nextDiscovery = Time.realtimeSinceStartup + 5;
        boards = UnityEngine.Object.FindObjectsByType<CTCPanelMarkerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    internal static void Tick(Settings settings, double now)
    {
        if (!StateManager.IsHost || !NpcServiceStore.Load() || TimetableController.Shared?.Current == null) return;
        if (Time.realtimeSinceStartup < nextSync) return;
        nextSync = Time.realtimeSinceStartup + 1;
        Discover();
        int day = (int)Math.Floor(now / 86400);
        var state = NpcServiceStore.State;
        var trains = TimetableController.Shared.Current.ToAbsolute().Trains.Values.Where(t =>
            NpcTrafficPlanPolicy.Owns(state, t.Name) || settings.ThroughTrafficEnabled &&
            ThroughTrafficPolicy.IsMarkedTrain(t.Name, settings.ThroughTrafficTrainSymbolPrefix) && t.TrainClass == Timetable.TrainClass.First).ToList();
        var wanted = trains.Where(t =>
        {
            // Carry active traffic onto the new day's board with a fresh marker;
            // yesterday's marker is still removed at the boundary.
            if (state.Services.Any(s => s.TrainSymbol == t.Name || s.ReturnTrainSymbol == t.Name)) return true;
            var plan = state.TrafficPlans.FirstOrDefault(p => p.Symbol == t.Name || p.ReturnSymbol == t.Name);
            if (plan != null) return NpcTrafficPlanPolicy.InPlanningDay(plan.Dispatch, plan.InterchangeId.Length == 0 ? plan.Dispatch : plan.ServiceTime, now);
            return true;
        }).OrderBy(t => t.Entries.FirstOrDefault().DepartureTime.Minutes).ToList();
        var symbols = wanted.Select(t => t.Name).ToHashSet();
        foreach (var board in boards)
        {
            if (board == null || board.faces.Count == 0) continue;
            var kv = board.GetComponentInParent<KeyValueObject>();
            if (kv == null || string.IsNullOrEmpty(kv.RegisteredId)) continue;
            foreach (string key in kv.Keys.Where(k => k.StartsWith(NpcBoardMarkerPolicy.Prefix)).ToList())
            {
                var marker = kv[key];
                if (NpcBoardMarkerPolicy.Expired(key, (int)marker["day"].FloatValue, day) || !symbols.Contains(marker["symbol"].StringValue)) Delete(board, kv, key);
            }
            // Native marker removal leaves destroyed objects in its dictionary.
            foreach (var id in board._markers.Keys.Where(id => ("marker-" + id).StartsWith(NpcBoardMarkerPolicy.Prefix) && board._markers[id] == null).ToList()) board._markers.Remove(id);
            int east = 0, west = 0;
            foreach (var train in wanted)
            {
                if (train.Entries.Count == 0) continue;
                bool toEast = train.Direction == Timetable.Direction.East;
                int index = toEast ? east++ : west++;
                string key = NpcBoardMarkerPolicy.Key(day, train.Name);
                // Respect manually moved/deleted board markers, including after reload.
                if (!NpcBoardMarkerPolicy.TryClaim(state.BoardMarkerDays, kv.RegisteredId, train.Name, day)) continue;
                if (!kv[key].IsNull) continue;
                string text = train.Name + " " + TimetableChartGeometry.Clock(train.Entries[0].DepartureTime.Minutes);
                float x = toEast ? board.faces.Count - 0.12f - (index / 10) * 0.24f : 0.12f + (index / 10) * 0.24f;
                kv[key] = Value.Dictionary(new Dictionary<string, Value>
                {
                    ["text"] = Value.String(toEast ? "<" + text : text + ">"),
                    ["x"] = Value.Float(Mathf.Clamp(x, 0.05f, board.faces.Count - 0.05f)),
                    ["y"] = Value.Float(0.08f + index % 10 * 0.085f),
                    ["symbol"] = Value.String(train.Name), ["day"] = Value.Float(day)
                });
            }
        }
        foreach (var id in state.BoardMarkerDays.Where(p => p.Value != day).Select(p => p.Key).ToList()) state.BoardMarkerDays.Remove(id);
        NpcServiceStore.Save();
    }

    internal static void Remove(string symbol)
    {
        if (symbol.Length == 0 || !StateManager.IsHost) return;
        Discover();
        foreach (var board in boards)
        {
            if (board == null) continue;
            var kv = board.GetComponentInParent<KeyValueObject>();
            if (kv == null) continue;
            foreach (var key in kv.Keys.Where(k => k.StartsWith(NpcBoardMarkerPolicy.Prefix) && kv[k]["symbol"].StringValue == symbol).ToList()) Delete(board, kv, key);
        }
    }

    private static void Delete(CTCPanelMarkerManager board, KeyValueObject kv, string key)
    {
        kv[key] = Value.Null();
        board._markers.Remove(key.Substring("marker-".Length));
    }
}
