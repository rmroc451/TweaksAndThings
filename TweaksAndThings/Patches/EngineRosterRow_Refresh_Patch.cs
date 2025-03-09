using HarmonyLib;
using Model;
using Model.Definition.Data;
using Model.Ops;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using RMROC451.TweaksAndThings.Enums;
using UI;
using UI.EngineRoster;
using UI.Tooltips;
using UnityEngine;


namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(EngineRosterRow))]
[HarmonyPatch(nameof(EngineRosterRow.Refresh))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class EngineRosterRow_Refresh_Patch
{
    public static void Postfix(EngineRosterRow __instance)
    {
        TweaksAndThingsPlugin? tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        RosterFuelColumnSettings? rosterFuelColumnSettings = tweaksAndThings?.settings?.EngineRosterFuelColumnSettings;

        if (tweaksAndThings == null ||
            rosterFuelColumnSettings == null || 
            !tweaksAndThings.IsEnabled ||
            rosterFuelColumnSettings.EngineRosterFuelStatusColumn == EngineRosterFuelDisplayColumn.None || (!GameInput.IsAltDown && !rosterFuelColumnSettings.EngineRosterShowsFuelStatusAlways))
        {
            return;
        }

        try
        {
            string fuelInfoText = string.Empty;
            string fuelInfoTooltip = string.Empty;
            Car engineOrTender = __instance._engine;
            List<LoadSlot> loadSlots = __instance._engine.Definition.LoadSlots;
            if (!loadSlots.Any())
            {
                engineOrTender = __instance._engine.DetermineFuelCar()!;
                loadSlots = engineOrTender != null ? engineOrTender.Definition.LoadSlots : Enumerable.Empty<LoadSlot>().ToList();
            }

            var offender = loadSlots.OrderBy(ls => (engineOrTender.GetLoadInfo(ls.RequiredLoadIdentifier, out int slotIndex)?.Quantity ?? 0) / loadSlots[slotIndex].MaximumCapacity).FirstOrDefault().RequiredLoadIdentifier;

            for (int i = 0; i < loadSlots.Count; i++)
            {
                CarLoadInfo? loadInfo = engineOrTender.GetLoadInfo(i);
                if (loadInfo.HasValue)
                {
                    CarLoadInfo valueOrDefault = loadInfo.GetValueOrDefault();
                    var fuelLevel = FuelLevel(valueOrDefault.Quantity, loadSlots[i].MaximumCapacity);
                    fuelInfoText += loadSlots[i].RequiredLoadIdentifier == offender ? fuelLevel + " " : string.Empty;
                    //fuelInfoText += TextSprites.PiePercent(valueOrDefault.Quantity, loadSlots[i].MaximumCapacity) + " ";
                    fuelInfoTooltip += $"{TextSprites.PiePercent(valueOrDefault.Quantity, loadSlots[i].MaximumCapacity)} {valueOrDefault.LoadString(CarPrototypeLibrary.instance.LoadForId(valueOrDefault.LoadId))}\n";
                }
            }

            switch (rosterFuelColumnSettings.EngineRosterFuelStatusColumn)
            {
                case EngineRosterFuelDisplayColumn.Engine:
                    SetLabelAndTooltip(ref __instance.nameLabel, ref __instance.nameTooltip, fuelInfoText, fuelInfoTooltip);
                    break;
                case EngineRosterFuelDisplayColumn.Crew:
                    SetLabelAndTooltip(ref __instance.crewLabel, ref __instance.crewTooltip, fuelInfoText, fuelInfoTooltip);
                    break;
                case EngineRosterFuelDisplayColumn.Status:
                    SetLabelAndTooltip(ref __instance.infoLabel, ref __instance.infoTooltip, fuelInfoText, fuelInfoTooltip);
                    break;
                default:
                    break;
            }

        } catch (Exception ex)
        {
            rosterFuelColumnSettings.EngineRosterFuelStatusColumn = EngineRosterFuelDisplayColumn.None;
            Log.Error(ex, "Error Detecting fuel status for engine roster");
        }
    }

    private static void SetLabelAndTooltip(ref TMP_Text label, ref UITooltipProvider tooltip, string fuelInfoText, string fuelInfoTooltip)
    {
        label.text = $" {fuelInfoText} {label.text}";
        tooltip.TooltipInfo = new TooltipInfo(tooltip.tooltipTitle, fuelInfoTooltip);
    }

    public static string FuelLevel(float quantity, float capacity)
    {
        float num = capacity <= 0f ? 0 : Mathf.Clamp(quantity / capacity * 100, 0, 100);

        return $"{Mathf.FloorToInt(num):D2}%";
    }
}
