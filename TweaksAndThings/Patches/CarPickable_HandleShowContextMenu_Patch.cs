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
    private static void Postfix(Car car)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled) return;

        bool buttonsHaveCost = tweaksAndThings.EndGearHelpersRequirePayment();
        ContextMenu shared = ContextMenu.Shared;
        shared.AddButton(ContextMenuQuadrant.Unused1, $"{(car.EnumerateCoupled().Any(c => c.HandbrakeApplied()) ? "Release " : "Set ")} Consist", SpriteName.Handbrake, delegate
        {
            CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.Handbrake, buttonsHaveCost);
        });

        if (car.EnumerateCoupled().Any(c => c.EndAirSystemIssue()))
        {
            shared.AddButton(ContextMenuQuadrant.Unused1, $"Air Up Consist", SpriteName.Select, delegate
            {
                CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.GladhandAndAnglecock, buttonsHaveCost);
            });
        }

        if (!car.EnumerateCoupled().Any(c => !c.SupportsBleed()))
        {
            shared.AddButton(ContextMenuQuadrant.Unused2, $"Bleed Consist", SpriteName.Bleed, delegate
            {
                CarInspector_PopulateCarPanel_Patch.MrocConsistHelper(car, MrocHelperType.BleedAirSystem, buttonsHaveCost);
            });
        }

        shared.AddButton(ContextMenuQuadrant.Unused2, $"Follow", SpriteName.Inspect, delegate
        {
            CameraSelector.shared.FollowCar(car);
        });
        shared.BuildItemAngles();
        shared.StartCoroutine(shared.AnimateButtonsShown());
    }
}
