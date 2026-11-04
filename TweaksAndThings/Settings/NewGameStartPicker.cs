using System;
using System.Collections;
using System.Linq;
using Core;
using Game.Progression;
using Game.State;
using Helpers;
using KeyValue.Runtime;
using UI.Common;
using UI.Builder;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

/// <summary>Choose against the loaded map before native starter placement runs.</summary>
internal static class NewGameStartPicker
{
    internal static bool Choosing { get; private set; }
    internal static bool Resuming { get; private set; }

    internal static IEnumerator ChooseThenResume(StateManager state, SetupDescriptor originalSetup)
    {
        if (!NewGameStartOptions.TryDecode(state?.Storage.SetupId, out var choice) || choice?.ChooseAfterMapLoad != true)
            yield break;
        Choosing = true;
        float previousScale = Time.timeScale;
        bool paused = false;
        try
        {
            // Restore and late map patches must complete before inspecting areas.
            float readinessStarted = Time.realtimeSinceStartup;
            while (!WorldPatchReadiness.Ready || state?.HasRestoredProperties != true ||
                   NewGameStartOptions.FindProgression(choice.ProgressionId) == null || ModalAlertController.Shared == null)
            {
                if (StateManager.IsUnloading || StateManager.Shared != state) yield break;
                if (Time.realtimeSinceStartup - readinessStarted >= 30f)
                {
                    // Do not leave new-save creation suspended forever if a late map patch
                    // prevents the custom picker from becoming ready. Continue with the
                    // vanilla setup selected in the New Game menu.
                    if (state != null)
                    {
                        state.Storage.SetupId = choice.BaseSetup;
                        state._gameSetupPropertyPresets.RemoveAll(p =>
                            p.Item1 == "_progression" && p.Item2 == "unlocked" ||
                            p.Item1 == GameStorage.ObjectId && p.Item2 == NewGameStartOptions.SaveKey);
                    }
                    ModDiagnosticLog.Write("NEW GAME", "Starting-area picker readiness timed out; continuing with the original East Whittier setup.");
                    Choosing = false;
                    ResumeSession(state, originalSetup);
                    yield break;
                }
                yield return null;
            }
            var progression = NewGameStartOptions.FindProgression(choice.ProgressionId)!;
            NewGameStartOptions.InspectLoadedMap();
            Time.timeScale = 0; paused = true;
            bool confirmed = false;
            SetupDescriptor? descriptor = null;
            ModalAlertController.Present((builder, dismiss) =>
            {
                builder.AddLabel("Choose starting area and progression");
                builder.AddLabel("The map is loaded. Starter equipment and NPC planning wait for your selection.");
                builder.HVScrollView(list => NewGameStartOptions.BuildLoaded(choice, progression, list)).Height(450);
                builder.AddButton("Begin railroad", () =>
                {
                    if (!NewGameStartOptions.ValidateLoaded(choice, out var error)) { Toast.Present(error); return; }
                    try
                    {
                        // Validate equipment placement before changing world state.
                        choice.ChooseAfterMapLoad = false;
                        descriptor = NewGameStartRuntime.GetSetup(NewGameStartOptions.Encode(choice));
                        if (descriptor == null) throw new InvalidOperationException("Starting equipment is unavailable.");
                        confirmed = true; dismiss();
                    }
                    catch (Exception ex)
                    {
                        choice.ChooseAfterMapLoad = true;
                        Toast.Present(ex.Message);
                        TweaksAndThingsPlugin.LogException("Starting area validation", ex);
                    }
                });
                builder.AddButtonCompact("Use original East Whittier start", () =>
                {
                    choice.Origin = StartingProgressionPolicy.OriginalOrigin; choice.AreaId = "";
                    choice.Unlocked.Clear(); choice.ChooseAfterMapLoad = false;
                    descriptor = NewGameStartRuntime.GetSetup(NewGameStartOptions.Encode(choice));
                    confirmed = descriptor != null;
                    if (confirmed) dismiss();
                });
            }, true, 650);
            while (!confirmed)
            {
                if (StateManager.IsUnloading || StateManager.Shared != state) yield break;
                yield return null;
            }
            // Reconfigure progression before adding equipment. Native Configure
            // applies feature states and the save retains the selected origin.
            var progressionState = progression._keyValueObject;
            if (progressionState == null) throw new InvalidOperationException("Progression state has not restored.");
            progression.Unconfigure();
            state!.Storage.SetupId = NewGameStartOptions.Encode(choice);
            state._setupDescriptor = descriptor;
            progressionState["unlocked"] = Value.Array(choice.Unlocked.Select(Value.String).ToList());
            state.KeyValueObjectForId(GameStorage.ObjectId)[NewGameStartOptions.SaveKey] =
                Value.String(Newtonsoft.Json.JsonConvert.SerializeObject(choice));
            progression.Configure(progressionState);
            state._setupDescriptor = descriptor;
            var pendingSetup = HarmonyLib.AccessTools.Field(typeof(StateManager), "_pendingCompanySetup");
            if (pendingSetup == null) throw new InvalidOperationException("Pending company setup field was not found.");
            pendingSetup.SetValue(state, descriptor);
            Time.timeScale = previousScale; paused = false;
            var spawn = descriptor!.spawnPoint.transform;
            CameraSelector.shared.JumpToPoint(spawn.position, spawn.rotation, null);
            ResumeSession(state, descriptor);
            ModDiagnosticLog.Write("NEW GAME", "Starting area confirmed and starter equipment placed: " + choice.Origin + "/" + choice.AreaId);
        }
        finally
        {
            if (paused && Time.timeScale == 0) Time.timeScale = previousScale;
            Choosing = false;
        }
    }

    private static void ResumeSession(StateManager state, SetupDescriptor setup)
    {
        var pendingSetup = HarmonyLib.AccessTools.Field(typeof(StateManager), "_pendingCompanySetup");
        if (pendingSetup == null) throw new InvalidOperationException("Pending company setup field was not found.");
        pendingSetup.SetValue(state, setup);
        Resuming = true;
        try { state.SessionDidBecomeActive(); }
        finally { Resuming = false; }
    }
}
