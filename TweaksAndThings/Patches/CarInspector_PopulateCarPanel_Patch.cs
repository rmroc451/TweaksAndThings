using Core;
using Game.Messages;
using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Model;
using Network;
using Railloader;
using RMROC451.TweaksAndThings.Enums;
using RMROC451.TweaksAndThings.Extensions;
using RollingStock;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using UI;
using UI.Builder;
using UI.CarInspector;
using UI.ContextMenu;
using UI.Tags;
using static Model.Car;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(CarInspector))]
[HarmonyPatch(nameof(CarInspector.PopulateCarPanel), typeof(UIPanelBuilder))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class CarInspector_PopulateCarPanel_Patch
{
    private static ILogger _log => Log.ForContext<CarInspector_PopulateCarPanel_Patch>();
    internal static IEnumerable<LogicalEnd> ends = Enum.GetValues(typeof(LogicalEnd)).Cast<LogicalEnd>();

    /// <summary>
    /// If a caboose inspector is opened, it will auto set Anglecocks, gladhands and hand brakes
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="builder"></param>
    /// <returns></returns>
	private static bool Prefix(CarInspector __instance, UIPanelBuilder builder)
    {

        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return true;
        bool buttonsHaveCost = tweaksAndThings.EndGearHelpersRequirePayment();

        var consist = __instance._car.EnumerateCoupled();

        builder.HStack(delegate (UIPanelBuilder hstack)
        {
            hstack = AddCarConsistRebuildObservers(hstack, consist);
            var buttonName = $"{(consist.Any(c => c.HandbrakeApplied()) ? "Release " : "Set ")} {TextSprites.HandbrakeWheel}";
            hstack.AddButtonCompact(buttonName, delegate
            {
                MrocConsistHelper(__instance._car, MrocHelperType.Handbrake, buttonsHaveCost);
                hstack.Rebuild();
            }).Tooltip(buttonName, $"Iterates over cars in this consist and {(consist.Any(c => c.HandbrakeApplied()) ? "releases" : "sets")} {TextSprites.HandbrakeWheel}.");

            if (consist.Any(c => c.EndAirSystemIssue()))
            {
                hstack.AddButtonCompact("Connect Air", delegate
                {
                    MrocConsistHelper(__instance._car, MrocHelperType.GladhandAndAnglecock, buttonsHaveCost);
                    hstack.Rebuild();
                }).Tooltip("Connect Consist Air", "Iterates over each car in this consist and connects gladhands and opens anglecocks.");
            }

            hstack.AddButtonCompact("Bleed Consist", delegate
            {
                MrocConsistHelper(__instance._car, MrocHelperType.BleedAirSystem, buttonsHaveCost);
                hstack.Rebuild();
            }).Tooltip("Bleed Air Lines", "Iterates over each car in this consist and bleeds the air out of the lines.");
        });

        CabooseUiEnhancer(__instance, builder, consist, tweaksAndThings);

        return true;
    }

    private static void CabooseUiEnhancer(CarInspector __instance, UIPanelBuilder builder, IEnumerable<Car> consist, TweaksAndThingsPlugin plugin)
    {
        if (plugin.CabooseNonMotiveAllowedSetting(__instance._car))
        {
            builder.HStack(delegate (UIPanelBuilder hstack)
            {
                hstack = AddCarConsistRebuildObservers(hstack, consist, all: false);
                hstack.AddField("Consist Info", hstack.HStack(delegate (UIPanelBuilder field)
                {
                    int consistLength = consist.Count();
                    int tonnage = LocomotiveControlsHoverArea.CalculateTonnage(consist);
                    int lengthInMeters = UnityEngine.Mathf.CeilToInt(LocomotiveControlsHoverArea.CalculateLengthInMeters(consist.ToList()) * 3.28084f);
                    var newSubTitle = () => string.Format("{0}, {1:N0}T, {2:N0}ft, {3:0.0} mph", consistLength.Pluralize("car"), tonnage, lengthInMeters, __instance._car.VelocityMphAbs);

                    field.AddLabel(() => newSubTitle(), UIPanelBuilder.Frequency.Fast)
                    .Tooltip("Consist Info", "Reflects info about consist.").FlexibleWidth();
                }));
            });
        }
    }

    private static UIPanelBuilder AddCarConsistRebuildObservers(UIPanelBuilder builder, IEnumerable<Model.Car> consist, bool all = true)
    {
        TagController tagController = UnityEngine.Object.FindFirstObjectByType<TagController>();
        foreach (Model.Car car in consist.Where(c => c.Archetype != Model.Definition.CarArchetype.Tender))
        {
            builder = AddObserver(builder, car, PropertyChange.KeyForControl(PropertyChange.Control.Handbrake), tagController);
            foreach (LogicalEnd logicalEnd in ends)
            {
                builder = AddObserver(builder, car, KeyValueKeyFor(EndGearStateKey.IsCoupled, car.LogicalToEnd(logicalEnd)), tagController);
                if (all) builder = AddObserver(builder, car, KeyValueKeyFor(EndGearStateKey.IsAirConnected, car.LogicalToEnd(logicalEnd)), tagController);
                if (all) builder = AddObserver(builder, car, KeyValueKeyFor(EndGearStateKey.Anglecock, car.LogicalToEnd(logicalEnd)), tagController);
            }
        }

        return builder;
    }

    private static UIPanelBuilder AddObserver(UIPanelBuilder builder, Model.Car car, string key, TagController tagController)
    {
        builder.AddObserver(
            car.KeyValueObject.Observe(
                key,
                delegate (Value value)
                {
                    try
                    {
                        builder.Rebuild();
                        if (car.TagCallout != null) tagController.UpdateTags(CameraSelector.shared._currentCamera.GroundPosition, true);
                        if (ContextMenu.IsShown && ContextMenu.Shared.centerLabel.text == car.DisplayName) CarPickable.HandleShowContextMenu(car);
                    }
                    catch (Exception ex)
                    {
                        _log.ForContext("car", car).Warning(ex, $"{nameof(AddObserver)} {car} Exception logged for {key}");
                    }
                },
                false
            )
        );

        return builder;
    }

    //var dh = new DownloadHandlerAudioClip($"file://{cacheFileName}", AudioType.MPEG);
    //dh.compressed = true; // This

    //using (UnityWebRequest wr = new UnityWebRequest($"file://{cacheFileName}", "GET", dh, null)) {
    //    yield return wr.SendWebRequest();
    //    if (wr.responseCode == 200) {
    //        audioSource.clip = dh.audioClip;
    //    }
    //}

    public static int MrocConsistHelper(Model.Car car, MrocHelperType mrocHelperType, bool buttonsHaveCost)
    {
        int output = 0;
        TrainController tc = UnityEngine.Object.FindObjectOfType<TrainController>();
        IEnumerable<Model.Car> consist = car.EnumerateCoupled();
        _log.ForContext("car", car).Verbose($"{car} => {mrocHelperType} => {string.Join("/", consist.Select(c => c.ToString()))}");

        CalculateCostIfEnabled(car, mrocHelperType, buttonsHaveCost, consist);

        switch (mrocHelperType)
        {
            case MrocHelperType.Handbrake:
                if (consist.Any(c => c.HandbrakeApplied()))
                {
                    consist.Do(c => c.SetHandbrake(false));
                }
                else
                {
                    consist = consist.Where(c => c is not BaseLocomotive && c.Archetype != Model.Definition.CarArchetype.Tender);
                    //when ApplyHandbrakesAsNeeded is called, and the consist contains an engine, it stops applying brakes.
                    tc.ApplyHandbrakesAsNeeded(consist.ToList(), PlaceTrainHandbrakes.Automatic);
                }
                break;

            case MrocHelperType.GladhandAndAnglecock:
                consist.Do(c =>
                    CarEndAirUpdate(c)
                );
                break;

            case MrocHelperType.BleedAirSystem:
                consist = consist.Where(c => !c.MotivePower());
                _log.ForContext("car", car).Debug($"{car} => {mrocHelperType} => {string.Join("/", consist.Select(c => c.ToString()))}");
                foreach (Model.Car bleed in consist)
                {
                    StateManager.ApplyLocal(new PropertyChange(bleed.id, PropertyChange.Control.Bleed, 1));
                }
                break;

            case MrocHelperType.Oil:
                consist = consist.Where(c => c.NeedsOiling || c.HasHotbox);
                _log.ForContext("car", car).Debug($"{car} => {mrocHelperType} => {string.Join("/", consist.Select(c => c.ToString()))}");
                foreach (Model.Car oil in consist)
                {
                    StateManager.ApplyLocal(new PropertyChange(oil.id, nameof(Car.Oiled).ToLower(), new FloatPropertyValue(1)));
                    output += car.HasHotbox ? 1 : 0;
                    if (car.HasHotbox) car.AdjustHotboxValue();
                }
                break;
        }
        return output;
    }

    internal static void CarEndAirUpdate(Car c)
    {
        ends.Do(end =>
        {
            EndGear endGear = c[end];

            StateManager.ApplyLocal(
                new PropertyChange(
                    c.id,
                    KeyValueKeyFor(EndGearStateKey.Anglecock, c.LogicalToEnd(end)),
                    new FloatPropertyValue(endGear.IsCoupled ? 1f : 0f)
                )
            );

            if (c.TryGetAdjacentCar(end, out Model.Car c2)) StateManager.ApplyLocal(new SetGladhandsConnected(c.id, c2.id, true));
        });
    }

    internal static void CalculateCostIfEnabled(Car car, MrocHelperType mrocHelperType, bool buttonsHaveCost, IEnumerable<Car> consist)
    {
        if (buttonsHaveCost)
        {
            float originalTimeCost = consist.CalculateCostForAutoEngineerEndGearSetting();
            float timeCost = originalTimeCost;
            float crewCost = timeCost / 3600; //hours of time deducted from caboose.
            var tsString = crewCost.FormatCrewHours(OpsController_AnnounceCoalescedPayments_Patch.CrewLoadHours.description);
            Car? cabooseWithAvailCrew = car.FindMyCabooseWithLoadRequirement(crewCost, buttonsHaveCost);
            if (cabooseWithAvailCrew == null) timeCost *= 1.5f;
            var cabooseFoundDisplay = cabooseWithAvailCrew?.DisplayName ?? "No caboose";

            _log.ForContext("car", car).Debug($"{nameof(MrocConsistHelper)} {mrocHelperType} : [VACINITY CABEESE FOUND:{cabooseWithAvailCrew?.ToString() ?? "NONE"}] => Consist Length {consist.Count()} => costs {timeCost / 60} minutes  of AI Engineer time, $5 per hour = ~${Math.Ceiling((decimal)(timeCost / 3600) * 5)} (*2 if no caboose nearby)");


            Multiplayer.SendError(StateManager.Shared._playersManager.LocalPlayer, $"{(cabooseWithAvailCrew != null ? $"{cabooseWithAvailCrew.DisplayName} Hours Adjusted: ({tsString})\n" : string.Empty)}Wages: ~(${Math.Ceiling((decimal)(timeCost / 3600) * 5)})");

            StateManager_OnDayDidChange_Patch.UnbilledAutoBrakeCrewRunDuration += timeCost;
        }
    }
}
