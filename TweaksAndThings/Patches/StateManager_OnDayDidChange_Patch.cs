﻿using Game.Events;
using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Network;
using Railloader;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(StateManager))]
[HarmonyPatch(nameof(StateManager.OnDayDidChange), typeof(TimeDayDidChange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class StateManager_OnDayDidChange_Patch
{
    private const string unbilledBrakeCrewDuration = "unbilledBrakeCrewDuration";

    private static void Postfix(StateManager __instance)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled || !(tweaksAndThings?.settings?.EndGearHelpersRequirePayment ?? false)) return;
        if (StateManager.IsHost) PayAutoBrakeCrewWages(__instance);
    }

    private static void PayAutoBrakeCrewWages(StateManager __instance)
    {
        float unbilledRunDuration = UnbilledAutoBrakeCrewRunDuration;
        int num = Mathf.FloorToInt(unbilledRunDuration / 3600f * 5f);
        float num2 = (float)num / 5f;
        float unbilledAutoEngineerRunDuration2 = unbilledRunDuration - num2 * 3600f;
        if (num > 0)
        {
            __instance.ApplyToBalance(-num, Ledger.Category.WagesAI, null, memo: "AI Brake Crew");
            Multiplayer.Broadcast($"Paid {num:C0} for {num2:F1} hours of Brake Crew services.");
            UnbilledAutoBrakeCrewRunDuration = unbilledAutoEngineerRunDuration2;
        }
    }

    public static float UnbilledAutoBrakeCrewRunDuration
    {
        get
        {
            return StateManager.Shared._storage._gameKeyValueObject[unbilledBrakeCrewDuration].FloatValue;
        }
        set
        {
            StateManager.Shared._storage._gameKeyValueObject[unbilledBrakeCrewDuration] = Value.Float(value);
        }
    }
}
