using HarmonyLib;
using Model;
using Railloader;
using RMROC451.TweaksAndThings.Enums;
using RMROC451.TweaksAndThings.Extensions;
using RollingStock;
using System.Linq;
using UI;
using UI.ContextMenu;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(CarPickable))]
[HarmonyPatch(nameof(CarPickable.HandleShowContextMenu), typeof(Car))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class CarPickable_HandleShowContextMenu_Patch
{
    private static bool Prefix(Car car)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return true;

        bool buttonsHaveCost = tweaksAndThings.EndGearHelpersRequirePayment();
        TrainController trainController = TrainController.Shared;
        ContextMenu shared = ContextMenu.Shared;
        if (ContextMenu.IsShown)
        {
            shared.Hide();
        }
        shared.Clear();

        shared.AddButton(ContextMenuQuadrant.General, (trainController.SelectedCar == car) ? "Deselect" : "Select", SpriteName.Select, delegate
        {
            trainController.SelectedCar = ((trainController.SelectedCar == car) ? null : car);
        });
        if (GameInput.IsShiftDown)
        {


            if (!car.EnumerateCoupled().Any(c => !c.SupportsBleed()))
            {
                shared.AddButton(ContextMenuQuadrant.Brakes, $"Bleed Consist", SpriteName.Bleed, delegate
                {
                    CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.BleedAirSystem, buttonsHaveCost);
                });
            }

            shared.AddButton(ContextMenuQuadrant.Brakes, $"{(car.EnumerateCoupled().Any(c => c.HandbrakeApplied()) ? "Release " : "Set ")} Consist", SpriteName.Handbrake, delegate
            {
                CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.Handbrake, buttonsHaveCost);
            });

            if (car.EnumerateCoupled().Any(c => c.EndAirSystemIssue()))
            {
                shared.AddButton(ContextMenuQuadrant.Unused2, $"Air Up Consist", SpriteName.Select, delegate
            {
                CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.GladhandAndAnglecock, buttonsHaveCost);
            });
            }
        }
        else
        {
            if (car.SupportsBleed())
            {
                shared.AddButton(ContextMenuQuadrant.Brakes, "Bleed", SpriteName.Bleed, car.SetBleed);
            }
            shared.AddButton(ContextMenuQuadrant.Brakes, car.air.handbrakeApplied ? "Release Handbrake" : "Apply Handbrake", SpriteName.Handbrake, delegate
            {
                bool apply = !car.air.handbrakeApplied;
                car.SetHandbrake(apply);
            });
        }

        shared.AddButton(ContextMenuQuadrant.General, $"Follow", SpriteName.Inspect, delegate
        {
            CameraSelector.shared.FollowCar(car);
        });

        shared.Show(car.DisplayName);
        shared.BuildItemAngles();
        shared.StartCoroutine(shared.AnimateButtonsShown());
        return false;
    }
}
