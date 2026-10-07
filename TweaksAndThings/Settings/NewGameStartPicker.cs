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
    [ThreadStatic] internal static bool Placing;

    internal static IEnumerator Wrap(IEnumerator original, TrainController controller)
    {
        var state = StateManager.Shared;
        if (!NewGameStartOptions.TryDecode(state?.Storage.SetupId, out var choice) || choice?.ChooseAfterMapLoad != true)
        {
            while (original.MoveNext()) yield return original.Current;
            yield break;
        }
        Choosing = true;
        float previousScale = Time.timeScale;
        bool paused = false;
        IEnumerator? placement = null;
        try
        {
            // Restore and late map patches must complete before inspecting areas.
            while (!WorldPatchReadiness.Ready || state?.HasRestoredProperties != true ||
                   NewGameStartOptions.FindProgression(choice.ProgressionId) == null || ModalAlertController.Shared == null)
            {
                if (StateManager.IsUnloading || StateManager.Shared != state) yield break;
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
            }, 650);
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
            var spawn = descriptor!.spawnPoint.transform;
            CameraSelector.shared.DefaultSpawn = new PositionRotation(WorldTransformer.WorldToGame(spawn.position), spawn.rotation);
            Time.timeScale = previousScale; paused = false;
            CameraSelector.shared.JumpToSpawn();
            Placing = true;
            try { placement = CompanyModeSetup.Setup(controller, descriptor); }
            finally { Placing = false; }
            while (placement.MoveNext()) yield return placement.Current;
            ModDiagnosticLog.Write("NEW GAME", "Starting area confirmed and starter equipment placed: " + choice.Origin + "/" + choice.AreaId);
        }
        finally
        {
            if (paused && Time.timeScale == 0) Time.timeScale = previousScale;
            Choosing = false;
            (placement as IDisposable)?.Dispose();
            (original as IDisposable)?.Dispose();
        }
    }
}
