using Game.State;
using HarmonyLib;
using Model;
using Railloader;
using RMROC451.TweaksAndThings.Enums;
using RMROC451.TweaksAndThings.Extensions;
using RollingStock;
using System;
using System.Linq;
using UI;
using UI.ContextMenu;
using UnityEngine;
using ContextMenu = UI.ContextMenu.ContextMenu;

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

        if (!car.EnumerateCoupled().Any(c => !c.SupportsBleed()))
        {
            Sprite bleedConsist = MapWindow_OnClick_Patch.LoadTexture("BleedConsist.png", "BleedConsist");
            shared.AddButton(ContextMenuQuadrant.Brakes, $"Bleed Consist", bleedConsist, delegate
            {
                CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.BleedAirSystem, buttonsHaveCost);
            });
        }
        if (car.SupportsBleed())
        {
            Sprite bleedCar = MapWindow_OnClick_Patch.LoadTexture("BleedCar.png", "BleedCar");
            shared.AddButton(ContextMenuQuadrant.Brakes, "Bleed", bleedCar, car.SetBleed);
        }

        string text = car.EnumerateCoupled().Any(c => c.HandbrakeApplied()) ? "Release " : "Set ";
        Sprite consistBrakes = MapWindow_OnClick_Patch.LoadTexture($"Consist{text.Trim()}Brake.png", $"{text.Trim()}Consist");
        shared.AddButton(ContextMenuQuadrant.Brakes, $"{text}Consist", consistBrakes, delegate
        {
            CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.Handbrake, buttonsHaveCost);
        });



        string textCar = car.HandbrakeApplied() ? "Release " : "Set ";
        Sprite carBrakes = MapWindow_OnClick_Patch.LoadTexture($"{text.Trim()}Brake.png", $"{text.Trim()}Consist");
        shared.AddButton(ContextMenuQuadrant.Brakes, $"{textCar}Handbrake", carBrakes, delegate
        {
            bool apply = !car.air.handbrakeApplied;
            car.SetHandbrake(apply);
        });


        if (car.EnumerateCoupled().Any(c => c.EndAirSystemIssue()))
        {
            Sprite connectAir = MapWindow_OnClick_Patch.LoadTexture($"ConnectAir.png", "ConnectAir");
            shared.AddButton(ContextMenuQuadrant.General, $"Air Up Consist", connectAir, delegate
            {
                CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.GladhandAndAnglecock, buttonsHaveCost);
            });
        }

        if (StateManager.IsHost && car.EnumerateCoupled().Any(c => c.NeedsOiling || c.HasHotbox))
        {
            Sprite oilCan = MapWindow_OnClick_Patch.LoadTexture("OilCan.png", "OilCan");
            shared.AddButton(ContextMenuQuadrant.General, $"Oil Consist", oilCan, delegate
            {
                CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.Oil, buttonsHaveCost);
            });
        }

        Sprite follow = MapWindow_OnClick_Patch.LoadTexture($"Follow.png", "ConnectAir");
        shared.AddButton(ContextMenuQuadrant.General, $"Follow", follow, delegate
        {
            CameraSelector.shared.FollowCar(car);
        });

        string secondaryLine = car.Waybill.HasValue ? $"{Environment.NewLine}{car.Waybill.Value.Destination.DisplayName}" : string.Empty;
        secondaryLine = secondaryLine.Length > 10 + Environment.NewLine.Length ? $"{secondaryLine.Substring(0, 7 + Environment.NewLine.Length)}..." : secondaryLine;
        shared.Show($"{car.EnumerateCoupled().Count()} Cars{Environment.NewLine}{car.DisplayName}{secondaryLine}");
        shared.BuildItemAngles();
        shared.StartCoroutine(shared.AnimateButtonsShown());
        return false;
    }
}
