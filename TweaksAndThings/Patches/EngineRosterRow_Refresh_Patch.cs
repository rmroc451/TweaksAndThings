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
using Game.State;


namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(EngineRosterRow))]
[HarmonyPatch(nameof(EngineRosterRow.Refresh))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class EngineRosterRow_Refresh_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<EngineRosterRow_Refresh_Patch>();

    public static void Postfix(EngineRosterRow __instance)
    {
        TweaksAndThingsPlugin? tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        RosterFuelColumnSettings? rosterFuelColumnSettings = tweaksAndThings?.settings?.EngineRosterFuelColumnSettings;

        string fuelInfoText = string.Empty;
        string fuelInfoTooltip = string.Empty;

        if (tweaksAndThings == null ||
            rosterFuelColumnSettings == null || 
            !tweaksAndThings.IsEnabled() ||
            rosterFuelColumnSettings.EngineRosterFuelStatusColumn == EngineRosterFuelDisplayColumn.None || (!GameInput.IsAltDown && !rosterFuelColumnSettings.EngineRosterShowsFuelStatusAlways) ||
            __instance._engine.IsMuEnabled
            )
        {
            return;
        }

        try
        {
            Car engineOrTender = __instance._engine;
            IEnumerable<Car> locos = engineOrTender.EnumerateCoupled().Where(c => c.IsLocomotive).ToList();
            IEnumerable<Car> consist = engineOrTender.EnumerateCoupled().Where(c => c.EnableOiling).ToList();
            bool cabooseRequirementFulfilled = 
                !tweaksAndThings.RequireConsistCabooseForOilerAndHotboxSpotter() 
                || consist.ConsistNoFreight() 
                ||  (bool)engineOrTender.FindMyCabooseSansLoadRequirement();
            float offendingPercentage = 100f;

            foreach (Car loco in locos)
            {
                var investigate = loco;
                List<LoadSlot> loadSlots = investigate.Definition.LoadSlots;
                if (!loadSlots.Any())
                {
                    investigate = investigate.DetermineFuelCar()!;
                    loadSlots = investigate != null ? investigate.Definition.LoadSlots : Enumerable.Empty<LoadSlot>().ToList();
                }

                var offender = loadSlots.OrderBy(ls => (investigate.GetLoadInfo(ls.RequiredLoadIdentifier, out int slotIndex)?.Quantity ?? 0) / loadSlots[slotIndex].MaximumCapacity).FirstOrDefault().RequiredLoadIdentifier;


                for (int i = 0; i < loadSlots.Count; i++)
                {
                    CarLoadInfo? loadInfo = investigate.GetLoadInfo(i);
                    if (loadInfo.HasValue)
                    {
                        CarLoadInfo valueOrDefault = loadInfo.GetValueOrDefault();
                        var fuelLevel = FuelLevel(valueOrDefault.Quantity, loadSlots[i].MaximumCapacity);
                        var offenderCheck = CalcPercentLoad(valueOrDefault.Quantity, loadSlots[i].MaximumCapacity);

                        if (offenderCheck < offendingPercentage)
                        {
                            offendingPercentage = offenderCheck;
                            fuelInfoText = loadSlots[i].RequiredLoadIdentifier == offender ? $"{fuelLevel} " : string.Empty;
                        }

                        fuelInfoTooltip += $"{TextSprites.PiePercent(valueOrDefault.Quantity, loadSlots[i].MaximumCapacity)} {valueOrDefault.LoadString(CarPrototypeLibrary.instance.LoadForId(valueOrDefault.LoadId))} {(!loco.id.Equals(__instance._engine.id) ? $"{loco.DisplayName}" : "")}\n";
                    }
                }
            }

            try
            {
                if (cabooseRequirementFulfilled && StateManager.Shared.Storage.OilFeature && consist.Any())
                {
                    float lowestOilLevel = consist.OrderBy(c => c.Oiled).FirstOrDefault().Oiled;
                    var oilLevel = FuelLevel(lowestOilLevel, 1);
                    fuelInfoTooltip += $"{lowestOilLevel.TriColorPiePercent(1)} {oilLevel} Consist Oil Lowest Level\n";
                    if (CalcPercentLoad(lowestOilLevel, 1) < offendingPercentage)
                    {
                        fuelInfoText = $"{oilLevel} ";
                    }

                    if (consist.Any(c => c.HasHotbox))
                    {
                        fuelInfoText = $"{TextSprites.Hotbox} ";
                        fuelInfoTooltip = $"{TextSprites.Hotbox} Hotbox detected!\n{fuelInfoTooltip}";
                    }
                }
                else if (!cabooseRequirementFulfilled && StateManager.Shared.Storage.OilFeature)
                {
                    fuelInfoTooltip += $"Add Caboose To Consist For Consist Oil Level Reporting\n";
                }

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error Detecting oiling consist status for engine roster");
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

    public static float CalcPercentLoad(float quantity, float capacity)
    {
        float num = capacity <= 0f ? 0 : Mathf.Clamp(quantity / capacity * 100, 0, 100);

        return num;
    }

    public static string FuelLevel(float quantity, float capacity)
    {
        return $"{Mathf.FloorToInt(CalcPercentLoad(quantity, capacity)):D2}%";
    }
}
