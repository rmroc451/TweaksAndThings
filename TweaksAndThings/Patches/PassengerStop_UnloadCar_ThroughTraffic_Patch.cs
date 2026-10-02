using Game.State;
using HarmonyLib;
using Model;
using Model.Ops;
using Model.Ops.Timetable;
using RMROC451.TweaksAndThings.Extensions;
using System;
using System.Linq;

namespace RMROC451.TweaksAndThings.Patches;

/// <summary>Settles the scheduled reward or late penalty only for passengers actually delivered.</summary>
[HarmonyPatch(typeof(PassengerStop))]
[HarmonyPatch(nameof(PassengerStop.UnloadCar))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class PassengerStop_UnloadCar_ThroughTraffic_Patch
{
    private struct UnloadState
    {
        internal int PassengerCount;
        internal bool TrustedMutation;
    }

    private static void Prefix(PassengerStop __instance, Car car, ref UnloadState __state)
    {
        __state = default;
        if (!StateManager.IsHost || car == null || !car.TryGetTimetableTrain(out Timetable.Train train) ||
            !ThroughTrafficPolicy.IsMarkedTrain(train.Name, TweaksAndThingsPlugin.Instance?.settings?.ThroughTrafficTrainSymbolPrefix) ||
            train.TrainClass != Timetable.TrainClass.First) return;

        __state.TrustedMutation = ThroughTrafficGuard.IsGenerated(car);
        if (__state.TrustedMutation) ThroughTrafficGuard.BeginTrustedMutation();
        var marker = car.GetPassengerMarker();
        __state.PassengerCount = marker?.CountPassengersForStop(__instance.identifier) ?? 0;
    }

    private static void Postfix(PassengerStop __instance, Car car, UnloadState __state)
    {
        if (__state.PassengerCount <= 0 || !StateManager.IsHost || TweaksAndThingsPlugin.Instance?.settings == null ||
            !car.TryGetTimetableTrain(out Timetable.Train train)) return;

        var station = TimetableController.Shared.GetAllStations().FirstOrDefault(item => item.passengerStop == __instance);
        if (station == null) return;
        int entryIndex = -1;
        for (int i = 0; i < train.Entries.Count; i++)
        {
            if (string.Equals(train.Entries[i].Station, station.code, StringComparison.OrdinalIgnoreCase))
            {
                entryIndex = i;
                break;
            }
        }
        if (entryIndex < 0 || !train.TryGetAbsoluteTimeForEntry(entryIndex, TimetableTimeType.Arrival, out int scheduledMinutes)) return;

        var now = StateManager.Now;
        int actualMinutes = now.Hours * 60 + now.Minutes;
        bool onTime = ThroughTrafficPolicy.IsWithinGracePeriod(scheduledMinutes, actualMinutes,
            TweaksAndThingsPlugin.Instance.settings.ThroughTrafficOnTimeGraceMinutes);
        int amount = ThroughTrafficPolicy.PassengerCarSettlement(
            __state.PassengerCount, onTime, TweaksAndThingsPlugin.Instance.settings.ThroughTrafficDollarsPerPassenger);
        if (amount == 0) return;

        // The native passenger fare is still paid by the game. This separate entry is the schedule bonus/penalty.
        StateManager.Shared.ApplyToBalance(amount, Ledger.Category.WagesAI, null,
            memo: $"Through traffic {train.Name}: {__state.PassengerCount} passengers {(onTime ? "on time" : "late")} at {station.code}");
    }

    private static Exception? Finalizer(UnloadState __state, Exception? __exception)
    {
        if (__state.TrustedMutation) ThroughTrafficGuard.EndTrustedMutation();
        return __exception;
    }
}
