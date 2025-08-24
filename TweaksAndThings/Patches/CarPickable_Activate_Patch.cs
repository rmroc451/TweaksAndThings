using Core;
using HarmonyLib;
using Model;
using Model.Ops;
using Model.Physics;
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
using UI.Tags;
using UnityEngine;


namespace RMROC451.TweaksAndThings.Patches;

enum MrocAction
{
    Follow,
    Inspect,
    ConnectConsistAir,
    ToggleConsistBrakes
}

[HarmonyPatch(typeof(CarPickable))]
[HarmonyPatch(nameof(CarPickable.Activate), typeof(PickableActivateEvent))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class CarPickable_Activate_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<CarPickable_Activate_Patch>();

    static float clicked = 0;
    static float clicktime = 0;
    static float clickdelay = 0.5f;

    private static bool Prefix(CarPickable __instance, PickableActivateEvent evt)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return true;

        _log.ForContext("car", __instance.car).Debug($"{GameInput.IsShiftDown} {GameInput.IsControlDown} {GameInput.IsAltDown}");

        if (OnPointerDown(evt, PickableActivation.Primary))
        {
            CameraSelector.shared.FollowCar(__instance.car);
            _log.ForContext("car", __instance.car).Debug("just click!");
            return false;

        }
        return HandleCarOrTrainBrakeDisplayClick(__instance.car, tweaksAndThings, evt.Activation);
    }

    internal static bool HandleCarOrTrainBrakeDisplayClick(Car car, TweaksAndThingsPlugin tweaksAndThings, PickableActivation activation)
    {
        bool bCtrlAltHeld = GameInput.IsControlDown && GameInput.IsAltDown;
        bool output = true;
        var consist = car.EnumerateCoupled();
        bool handbrakesApplied = consist.Any(c => c.HandbrakeApplied());
        bool airSystemIssues = consist.Any(c => c.EndAirSystemIssue());
        Func<bool> cabooseNear = () => (bool)car.FindMyCabooseSansLoadRequirement();
        bool needsOiling = GameInput.IsShiftDown && consist.All(c => c.IsStopped()) && consist.Any(c => c.NeedsOiling || c.HasHotbox) && (!tweaksAndThings.RequireConsistCabooseForOilerAndHotboxSpotter() || cabooseNear());
        var chargeIt = handbrakesApplied || airSystemIssues || needsOiling;
        //CTRL + ALT + SHIFT : BrakesAngleCocksAndOiling
        //CTRL + ALT : Release Consist Brakes and Check AngleCocks
        //ALT + SHIFT : toggle consist brakes
        //CTRL + SHIFT : Check Consist Angle Cocks
        //ALT : Toggle car brakes and & air up cars
        //CTRL : NOTHING; BASE CAR INSPECTOR
        //SHIFT : FOLLOW

        if (activation == PickableActivation.Primary)
        {
            if (bCtrlAltHeld)
            {
                BrakesAngleCocksAndOiling(car, tweaksAndThings, GameInput.IsShiftDown, consist, handbrakesApplied, airSystemIssues, needsOiling, chargeIt);
                _log.ForContext("car", car).Debug($"ctrlAlt{(GameInput.IsShiftDown ? "shift" : string.Empty)}Held!");
                output = false;
            }
            else if (GameInput.IsAltDown && GameInput.IsShiftDown)
            {
                CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.Handbrake, tweaksAndThings.EndGearHelpersRequirePayment());
                _log.ForContext("car", car).Debug("ctrlShiftHeld!");
                output = false;
            }
            else if (GameInput.IsControlDown && GameInput.IsShiftDown)
            {
                if (airSystemIssues)
                    CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.GladhandAndAnglecock, tweaksAndThings.EndGearHelpersRequirePayment());
                _log.ForContext("car", car).Debug("altShiftHeld!");
                output = false;
            }
            else if (GameInput.IsAltDown)
            {
                car.SetHandbrake(!car.HandbrakeApplied());
                CarInspector_PopulateCarPanel_Patch.CarEndAirUpdate(car);
                if (car.TryGetAdjacentCar(Car.LogicalEnd.A, out Model.Car cA)) CarInspector_PopulateCarPanel_Patch.CarEndAirUpdate(cA);
                if (car.TryGetAdjacentCar(Car.LogicalEnd.B, out Model.Car cB)) CarInspector_PopulateCarPanel_Patch.CarEndAirUpdate(cB);
                _log.ForContext("car", car).Debug("ctrlHeld!");
                TagController.Shared.UpdateTag(car, car.TagCallout, OpsController.Shared);
                car.TagCallout.Update();
                output = false;
            }
            else if (GameInput.IsShiftDown)
            {
                CameraSelector.shared.FollowCar(car);
                output = false;
            }
        }
        else if ((GameInput.IsControlDown || GameInput.IsAltDown) && activation == PickableActivation.Secondary)
        {
            AltClickageMyBrosif(car, activation);
            output = false;
        }
        else if (activation == PickableActivation.Secondary)
        {
            //AltClickageMyBrosif(car, activation);
            CarPickable.HandleShowContextMenu(car);
            output = false;
        }

        return output;
    }

    private static void AltClickageMyBrosif(Car car, PickableActivation activation)
    {
        //if (GameInput.IsControlDown)
        //{

        //}
        //else if (GameInput.IsAltDown)
        //{
        //    logSet(car);
        //    //car.TryGetAdjacentCar(Car.LogicalEnd.A, out var OutCarA);
        //    //car.TryGetAdjacentCar(Car.LogicalEnd.B, out var OutCarB);
        //    output = false;
        //}
        List<Car> cars = TrainController.Shared.SelectedLocomotive.EnumerateCoupled().ToList();
        logSet(car);
        //car.TryGetAdjacentCar(Car.LogicalEnd.A, out var OutCarA);
        //car.TryGetAdjacentCar(Car.LogicalEnd.B, out var OutCarB);
        //bool b4 = false;
        //if ((Object)(object)OutCarA != (Object)null && OutCarA.id == cars[i - 1].id)
        //{
        //    cars[i - 1].ApplyEndGearChange(Car.LogicalEnd.B, Car.EndGearStateKey.Anglecock, 0f);
        //    ccar.ApplyEndGearChange(Car.LogicalEnd.A, Car.EndGearStateKey.Anglecock, 0f);
        //    ccar.HandleCouplerClick(ccar.EndGearA.Coupler);
        //    logger.Information("{indName}: Decoupled {ccarName} from {cars1Name} at NOA{FR}", new object[4]
        //    {
        //            ((Object)base.Industry).name,
        //            ccar.DisplayName,
        //            cars[i - 1].DisplayName,
        //            (ccar.LogicalToEnd(Car.LogicalEnd.A) == Car.End.F) ? "F" : "R"
        //    });
        //    Storage.LastCarEnd = ccar.LogicalToEnd(Car.LogicalEnd.A);
        //    b4 = true;
        //}
        //if ((Object)(object)OutCarB != (Object)null && !b4 && OutCarB.id == cars[i - 1].id)
        //{
        //    cars[i - 1].ApplyEndGearChange(Car.LogicalEnd.A, Car.EndGearStateKey.Anglecock, 0f);
        //    ccar.ApplyEndGearChange(Car.LogicalEnd.B, Car.EndGearStateKey.Anglecock, 0f);
        //    ccar.HandleCouplerClick(ccar.EndGearB.Coupler);
        //    logger.Information("{indName}: Decoupled {ccarName} from {carsm1Name} at NOB{FR}", new object[4]
        //    {
        //            ((Object)base.Industry).name,
        //            ccar.DisplayName,
        //            cars[i - 1].DisplayName,
        //            (ccar.LogicalToEnd(Car.LogicalEnd.A) == Car.End.F) ? "F" : "R"
        //    });
        //    Storage.LastCarEnd = ccar.LogicalToEnd(Car.LogicalEnd.B);
        //    b4 = true;
        //}
        //if (!b4)
        //{
        //    logger.Information("{IndName}: ERROR: NLC {ccarName} can't find LCC {carsm1Name} ({OutCarAName},{OutCarBName})", new object[5]
        //    {
        //            ((Object)base.Industry).name,
        //            ccar.DisplayName,
        //            cars[i - 1].DisplayName,
        //            ((Object)(object)OutCarA != (Object)null) ? OutCarA.DisplayName : "no Car",
        //            ((Object)(object)OutCarB != (Object)null) ? OutCarB.DisplayName : "no Car"
        //    });
        //    return;
        //}
    }

    private static void logSet(Car refCar)
    {
        IntegrationSet set = refCar.set;
        foreach (Car car in set.Cars)
        {
            Car acar;
            string endA = (car.TryGetAdjacentCar(Car.LogicalEnd.A, out acar) ? acar.DisplayName : "none");
            Car bcar;
            string endB = (car.TryGetAdjacentCar(Car.LogicalEnd.B, out bcar) ? bcar.DisplayName : "none");
            _log.Information("[Car: {id}:(EndA:{carA}),(EndB:{carB}),(FisA:{fisA})]", new object[4] { car.DisplayName, endA, endB, car.FrontIsA });
        }
    }


    private static void BrakesAngleCocksAndOiling(Car car, TweaksAndThingsPlugin tweaksAndThings, bool shiftHeld, System.Collections.Generic.IEnumerable<Car> consist, bool handbrakesApplied, bool airSystemIssues, bool needsOiling, bool chargeIt)
    {
        int hbFix = 0;
        if (handbrakesApplied)
            CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.Handbrake, false);
        if (airSystemIssues)
            CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.GladhandAndAnglecock, false);
        if (needsOiling)
            hbFix = CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.Oil, false);
            if (hbFix > 0)
                Multiplayer.Broadcast($"Near {Hyperlink.To(car)}: \"{hbFix.Pluralize("hotbox") + " repaired!"}\"");
        if (chargeIt)
            CarInspector_PopulateCarPanel_Patch.CalculateCostIfEnabled(car, MrocHelperType.Handbrake, tweaksAndThings.EndGearHelpersRequirePayment(), consist);
    }

    public static bool OnPointerDown(
        PickableActivateEvent evt, 
        PickableActivation btn = PickableActivation.Primary
    )
    {
        bool output = false;
        if (evt.Activation == btn)
        {
            clicked++;
            if (clicked == 1) clicktime = Time.time;

            if (clicked > 1 && Time.time - clicktime < clickdelay)
            {
                clicked = 0;
                clicktime = 0;
                output = true;

            }
            else if (clicked > 2 || Time.time - clicktime > 1) clicked = 0;
        }
        return output;
    }
}
