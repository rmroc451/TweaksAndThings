using Game;
using Game.State;
using HarmonyLib;
using Model;
using Model.Ops;
using Network;
using Railloader;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UI.Builder;
using UnityEngine;
using static Model.Ops.RepairTrack;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(RepairTrack))]
[HarmonyPatch(nameof(RepairTrack.DailyPayables), typeof(GameDateTime), typeof(IIndustryContext))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class RepairTrack_DailyPayables_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<RepairTrack_DailyPayables_Patch>();
    private static Serilog.ILogger ContextualLogger(RepairTrack __instance) =>
        _log.ForContext("RepairTrackIdentifier", __instance?.Identifier ?? "???")
        .ForContext("RepairTrackName", __instance?.name ?? "???")
        .ForContext("RepairTrackDisplayName", __instance?.DisplayName ?? "???");

    public static bool Prefix(RepairTrack __instance, GameDateTime now, IIndustryContext ctx)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled() || !tweaksAndThings.ServiceFundPenalties()) return true;


        RepairRateState rateState = __instance.RateState;
        StateManager shared = StateManager.Shared;

        if (rateState.PayDue < 1E-06f)
        {
            rateState.PayDue = 0f;
            rateState.PaidCurrent = true;
        }
        else
        {
            int num = Mathf.CeilToInt(rateState.PayDue);
            rateState.PaidCurrent = shared.CanAfford(num);
            int penalty = Mathf.CeilToInt(!rateState.PaidCurrent ? num * 0.2f : 0);
            Multiplayer.Broadcast($"{Hyperlink.To(__instance.Industry)}: Paid {num:C0} wages for shop crew${(penalty > 0 ? $"; Overdraft fee of {penalty:C0}" : ".")}");

            __instance.Industry.ApplyToBalance(-num, Ledger.Category.WagesRepair, null, 0, quiet: true);
            if (penalty > 0) 
                StateManager.Shared.ApplyToBalance(-penalty, Ledger.Category.WagesRepair, null, $"Overdraft: {__instance.Industry.name}", 0, quiet: true);
            rateState.PayDue = 0f;
            rateState.PaidCurrent = true;
        }
        __instance.RateState = rateState;
        return false;
    }
}

[HarmonyPatch(typeof(RepairTrack))]
[HarmonyPatch(nameof(RepairTrack.EnumerateCarsActual), typeof(IIndustryContext))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class RepairTrack_NeedsRepair_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<RepairTrack_NeedsRepair_Patch>();
    public static void Postfix(RepairTrack __instance, ref IEnumerable<Car> __result)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return;

        __result = EnumerateCarsActualPatched(__result);
    }

    internal static IEnumerable<Car> EnumerateCarsActualPatched(IEnumerable<Car> __result, bool usePatchedVersion = true) =>
        usePatchedVersion ? __result.Where(NeedsRepairPatched) : __result;

    internal static bool NeedsRepairPatched(Car car)
    {
        bool result = TryGetRepairDestination(car, out var overrideTag);
        return result;
    }
}

[HarmonyPatch(typeof(RepairTrack))]
[HarmonyPatch(nameof(RepairTrack.BuildCars), typeof(UIPanelBuilder))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class RepairTrack_BuildCars_Patch
{
    public static bool Prefix(RepairTrack __instance, UIPanelBuilder builder)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return true;

        BuildCarsPatched(__instance, builder);

        return false;
    }

    public static IEnumerable<Car> EnumerateCarsActualOrig(RepairTrack __instance, IIndustryContext ctx)
    {
        TrainController trainController = TrainController.Shared;
        return from car in __instance.EnumerateCars(ctx)
               select trainController.CarForId(car.Id);
    }


    public static void BuildCarsPatched(RepairTrack __instance, UIPanelBuilder builder)
    {
        IndustryContext industryContext = __instance.CreateContext(TimeWeather.Now, 0f);
        IOrderedEnumerable<IGrouping<RepairGroup, Car>> carGroups = from @group in EnumerateCarsActualOrig(__instance, industryContext).GroupBy(delegate (Car car)
        {
            if (InForOverhaul(car))
            {
                return RepairGroup.Overhaul;
            }
            return RepairTrack_NeedsRepair_Patch.NeedsRepairPatched(car) ? RepairGroup.NeedsRepair : RepairGroup.None;
        })
                                                                    orderby @group.Key
                                                                    select @group;
        builder.VStack(delegate (UIPanelBuilder uIPanelBuilder)
        {
            float repairRateMultiplier = __instance.RateState.PayRateMultiplier;
            foreach (IGrouping<RepairGroup, Car> item in carGroups)
            {
                RepairGroup repairGroup = item.Key;
                uIPanelBuilder.AddSection(repairGroup switch
                {
                    RepairGroup.Overhaul => "Overhauling",
                    RepairGroup.NeedsRepair => "Repairing",
                    RepairGroup.None => "No Work Order Assigned",
                    _ => throw new ArgumentOutOfRangeException(),
                });
                foreach (Car car in item.OrderBy((Car car2) => car2.SortName))
                {
                    uIPanelBuilder.HStack(delegate (UIPanelBuilder uIPanelBuilder2)
                    {
                        uIPanelBuilder2.AddLabel(Hyperlink.To(car)).Width(130f);
                        uIPanelBuilder2.AddLabel($"{Mathf.RoundToInt(car.Condition * 100f)}%").Width(60f).HorizontalTextAlignment(HorizontalAlignmentOptions.Right);
                        string text = "";
                        string title = "";
                        if (repairGroup == RepairGroup.Overhaul)
                        {
                            float overhaulProgress = car.OverhaulProgress;
                            if (overhaulProgress > 0f)
                            {
                                text = $"{(int)(overhaulProgress * 100f)}%";
                                title = "Overhaul Progress";
                            }
                        }
                        uIPanelBuilder2.AddLabel(text).Width(60f).HorizontalTextAlignment(HorizontalAlignmentOptions.Right)
                            .Tooltip(title, null);
                        if (repairGroup != RepairGroup.None)
                        {
                            float num = CalculateRepairWorkOverall(car);
                            float num2 = num * 12000f * 0.0005f;
                            string text2 = ((!(repairRateMultiplier > 0f)) ? "Never" : GameDateTimeInterval.DeltaStringMinutes((int)(num / repairRateMultiplier * 60f * 24f), GameDateTimeInterval.Style.Short));
                            uIPanelBuilder2.AddLabel(text2).Width(80f).HorizontalTextAlignment(HorizontalAlignmentOptions.Right)
                                .Tooltip("Time Remaining", "D:HH:MM or H:MM");
                            uIPanelBuilder2.AddLabel($"{num2:F1}T").Width(80f).HorizontalTextAlignment(HorizontalAlignmentOptions.Right)
                                .Tooltip("Repair Parts Needed", null);
                        }
                    });
                }
            }
        }).Padding(new RectOffset(20, 0, 0, 0));
    }
}
