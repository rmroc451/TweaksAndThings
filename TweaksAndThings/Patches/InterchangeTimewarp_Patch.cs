using System.Collections;
using Game;
using Game.Messages;
using Game.State;
using HarmonyLib;
using Model;
using Model.Ops;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(StateManager), "WaitTimeCoroutine")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class InterchangeTimewarp_Patch
{
    private static readonly NpcTimewarpGate gate = new();

    internal static void CancelForSpawn(string service)
    {
        if (StateManager.IsHost) NpcSimulationSpeed.Stop("NPC traffic spawned: " + service);
        var state = StateManager.Shared;
        if (!StateManager.IsHost || state == null || !state.IsWaiting) return;
        gate.Cancel();
        state.IsWaiting = false;
        ModDiagnosticLog.Write("TIMEWARP", $"Canceled active wait immediately after NPC spawn: {service}; game time={TimeWeather.Now}.");
    }

    private static bool Prefix(StateManager __instance, float hours, ref IEnumerator __result)
    {
        var plugin = TweaksAndThingsPlugin.Instance;
        if (plugin?.IsEnabled != true || plugin.settings == null ||
            !StateManager.IsHost || OpsController.Shared == null || TrainController.Shared == null) return true;
        NpcSimulationSpeed.Stop("Clock-only wait requested");
        __result = Wait(__instance, hours, plugin.settings);
        return false;
    }

    private static IEnumerator Wait(StateManager state, float hours, Settings settings)
    {
        long ticket = gate.Start();
        state.IsWaiting = true;
        try
        {
            // Resolve incremental routes before choosing the cutoff; never jump
            // over a dispatch merely because its approach is still being computed.
            while (settings.InterchangeService == InterchangeServiceMode.Simulated && !SimulatedInterchangeService.PrepareWarp(settings))
            {
                if (!gate.IsCurrent(ticket)) yield break;
                NpcTrainOperations.AdvancePlanning();
                yield return null;
            }
            if (!gate.IsCurrent(ticket)) yield break;
            double cursor = TimeWeather.Now.TotalSeconds;
            double requestedEnd = cursor + Mathf.Max(0, hours) * 3600d;
            double? boundary = settings.InterchangeService == InterchangeServiceMode.Simulated ? SimulatedInterchangeService.WarpBoundary(settings, cursor, requestedEnd) : null;
            double? randomBoundary = NpcRandomThroughFreights.WarpBoundary(settings, cursor, requestedEnd);
            if (randomBoundary.HasValue && (!boundary.HasValue || randomBoundary.Value < boundary.Value)) boundary = randomBoundary;
            var switching = NpcSimulationSpeed.SwitchingBoundary(cursor, out string switchingName);
            bool switchingStop = switching.HasValue && switching.Value <= requestedEnd && (!boundary.HasValue || switching.Value <= boundary.Value);
            if (switchingStop) boundary = switching;
            double end = boundary ?? requestedEnd;
            float multiplier = TimeWeather.TimeMultiplier;
            while (cursor < end)
            {
                if (!gate.IsCurrent(ticket)) yield break;
                float step = (float)System.Math.Min(3600d, end - cursor);
                Industry.TickAll(step / multiplier);
                // A spawn during native industry processing must not be followed
                // by another clock jump from this now-canceled wait.
                if (!gate.IsCurrent(ticket)) yield break;
                cursor += step;
                StateManager.ApplyLocal(new SetTimeOfDay((float)cursor));
                if (!gate.IsCurrent(ticket)) yield break;
                yield return new WaitForSeconds(0.25f);
            }
            if (!gate.IsCurrent(ticket)) yield break;
            if (boundary.HasValue)
            {
                NpcDailyTrafficPlans.Tick(settings, TimeWeather.Now.TotalSeconds, force: true);
                if (settings.InterchangeService == InterchangeServiceMode.Simulated) SimulatedInterchangeService.DispatchAtWarpBoundary(settings);
                NpcRandomThroughFreights.Tick(settings, TimeWeather.Now.TotalSeconds, force: true);
                if (switchingStop) UI.Common.Toast.Present("Wait stopped for scheduled switching: " + switchingName);
                TweaksAndThingsPlugin.LogDiagnostic(switchingStop ? "Timewarp stopped for switching: " + switchingName :
                    "Timewarp stopped at NPC interchange dispatch time " + TimeWeather.Now + ". Check /npcTraffic for service or placement blockers.");
            }
        }
        finally { if (gate.IsCurrent(ticket)) state.IsWaiting = false; }
    }
}
