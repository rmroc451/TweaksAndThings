using Game.Events;
using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Model.Ops;
using Network;
using RMROC451.TweaksAndThings.Extensions;
using System.Linq;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(OpsController))]
[HarmonyPatch(nameof(OpsController.DayDidChange), typeof(TimeDayDidChange))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class StateManager_OnDayDidChange_Patch
{
    private const string unbilledBrakeCrewDuration = "unbilledBrakeCrewDuration";

    private static void Postfix()
    {
        TweaksAndThingsPlugin tweaksAndThings = TweaksAndThingsPlugin.Instance!;
        if (!tweaksAndThings.IsEnabled()) return;

        if (StateManager.IsHost) DoNewDayActivites(tweaksAndThings);
    }

    private static void DoNewDayActivites(TweaksAndThingsPlugin tweaksAndThings)
    {
        if (tweaksAndThings.EndGearHelpersRequirePayment() && tweaksAndThings.DayLoadCrewHours()) LoadCabeese();
        if (tweaksAndThings.EndGearHelpersRequirePayment()) PayAutoBrakeCrewWages();
    }

    private static void LoadCabeese()
    {
        foreach (var car in TrainController.Shared.Cars.Where(Car_Extensions.IsCaboose))
        {
            if (NpcServiceCaboose.IsServiceCar(car)) continue;
            var data = car.QuantityCapacityOfLoad(OpsController_AnnounceCoalescedPayments_Patch.CrewLoadHours);
            if (data.quantity < data.capacity)
            {
                Multiplayer.Broadcast($"{Hyperlink.To(car)}: \"Caboose crew topped off.\"");
                new OpsCarAdapter(car, OpsController.Shared).Load(OpsController_AnnounceCoalescedPayments_Patch.CrewLoadHours, data.capacity - data.quantity, string.Empty);
            }
        }
    }

    private static void PayAutoBrakeCrewWages()
    {
        float unbilledRunDuration = UnbilledAutoBrakeCrewRunDuration;
        int num = Mathf.FloorToInt(unbilledRunDuration / 3600f * 5f);
        float num2 = (float)num / 5f;
        float unbilledAutoEngineerRunDuration2 = unbilledRunDuration - num2 * 3600f;
        if (num > 0)
        {
            StateManager.Shared.ApplyToBalance(-num, Game.History.Ledger.Category.WagesAI, null, memo: "AI Brake Crew");
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
