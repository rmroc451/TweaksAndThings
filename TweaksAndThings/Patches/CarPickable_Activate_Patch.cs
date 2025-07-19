using Core;
using HarmonyLib;
using Model;
using Network;
using Railloader;
using RMROC451.TweaksAndThings.Enums;
using RMROC451.TweaksAndThings.Extensions;
using RollingStock;
using Serilog;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using UI;
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
        if (!tweaksAndThings.IsEnabled) return true;
        bool bCtrlAltHeld = GameInput.IsControlDown && GameInput.IsAltDown;

        _log.ForContext("car", __instance.car).Information($"{GameInput.IsShiftDown} {GameInput.IsControlDown} {GameInput.IsAltDown} {bCtrlAltHeld} ");

        //On Double click of a car: follow
        if (OnPointerDown(evt, PickableActivation.Primary))
        {
            CameraSelector.shared.FollowCar(__instance.car);
            _log.ForContext("car", __instance.car).Information("just click!");
            return false;

        } 
        //single click with keys pressed:
        else if (evt.Activation == PickableActivation.Primary)
        {
            bool output = true;
            var consist = __instance.car.EnumerateCoupled();
            bool handbrakesApplied = consist.Any(c => c.HandbrakeApplied());
            bool airSystemIssues = consist.Any(c => c.EndAirSystemIssue());
            bool needsOiling = GameInput.IsShiftDown && consist.All(c => c.IsStopped()) && consist.Any(c => c.NeedsOiling || c.HasHotbox) && (consist.CabooseInConsist() || !tweaksAndThings.RequireConsistCabooseForOilerAndHotboxSpotter());
            var chargeIt = handbrakesApplied || airSystemIssues || needsOiling;
            //CTRL + ALT + SHIFT : BrakesAngleCocksAndOiling
            //CTRL + ALT : Release Consist Brakes and Check AngleCocks
            //CTRL + SHIFT : Toggle Consist Brakes
            //CTRL : Toggle Car Brakes & Airup Car
            //ALT + SHIFT : Check Consist AngleCocks
            //ALT : Open Inspector to Car
            if (bCtrlAltHeld)
            {
                BrakesAngleCocksAndOiling(__instance, tweaksAndThings, GameInput.IsShiftDown, consist, handbrakesApplied, airSystemIssues, needsOiling, chargeIt);
                _log.ForContext("car", __instance.car).Information($"ctrlAlt{(GameInput.IsShiftDown ? "shift" : string.Empty)}Held!");
                output = false;
            }
            else if (GameInput.IsControlDown && GameInput.IsShiftDown)
            {
                CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(__instance.car, MrocHelperType.Handbrake, tweaksAndThings.EndGearHelpersRequirePayment());
                _log.ForContext("car", __instance.car).Information("ctrlShiftHeld!");
                output = false;
            }
            else if (GameInput.IsAltDown && GameInput.IsShiftDown)
            {
                if (airSystemIssues)
                    CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(__instance.car, MrocHelperType.GladhandAndAnglecock, tweaksAndThings.EndGearHelpersRequirePayment());
                _log.ForContext("car", __instance.car).Information("altShiftHeld!");
                output = false;
            }
            else if (GameInput.IsAltDown)
            {
                UI.CarInspector.CarInspector.Show(__instance.car);
                _log.ForContext("car", __instance.car).Information("altHeld!");
                output = false;
            }
            else if (GameInput.IsControlDown)
            {
                __instance.car.SetHandbrake(!__instance.car.HandbrakeApplied());
                CarInspector_PopulateCarPanel_Patch.CarEndAirUpdate(__instance.car);
                _log.ForContext("car", __instance.car).Information("ctrlHeld!");
                output = false;
            }
            return output;


            //else if (ctrlHeld && shiftHeld)
            //{
            //    var selected = UI.CarInspector.CarInspector._instance._selectedTabState.Value;
            //    UI.CarInspector.CarInspector.Show(__instance.car);
            //    UI.CarInspector.CarInspector._instance._selectedTabState.Value = selected;

            //}
        }

        return true;
    }

    private static void BrakesAngleCocksAndOiling(CarPickable __instance, TweaksAndThingsPlugin tweaksAndThings, bool shiftHeld, System.Collections.Generic.IEnumerable<Car> consist, bool handbrakesApplied, bool airSystemIssues, bool needsOiling, bool chargeIt)
    {
        int hbFix = 0;
        if (handbrakesApplied)
            CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(__instance.car, MrocHelperType.Handbrake, false);
        if (airSystemIssues)
            CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(__instance.car, MrocHelperType.GladhandAndAnglecock, false);
        if (needsOiling)
        {
            foreach (var car in consist.Where(c => c.NeedsOiling || c.HasHotbox))
            {
                float num2 = 1f - car.Oiled;
                car.OffsetOiled(num2);
                hbFix += car.HasHotbox ? 1 : 0;
                if (car.HasHotbox) car.AdjustHotboxValue(0f);
            }
            if (hbFix > 0)
                Multiplayer.Broadcast($"Near {Hyperlink.To(__instance.car)}: \"{hbFix.Pluralize("hotbox") + " repaired!"}\"");
        }

        if (chargeIt)
            CarInspector_PopulateCarPanel_Patch.CalculateCostIfEnabled(__instance.car, MrocHelperType.Handbrake, tweaksAndThings.EndGearHelpersRequirePayment(), consist);
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
