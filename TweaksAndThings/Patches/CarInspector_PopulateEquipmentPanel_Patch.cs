using Game.Messages;
using Game.Notices;
using Game.State;
using HarmonyLib;
using Model;
using Model.Ops;
using Network;
using Railloader;
using System;
using System.Linq;
using UI.Builder;
using UI.CarInspector;
using Core;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(CarInspector))]
[HarmonyPatch(nameof(CarInspector.PopulateEquipmentPanel))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class CarInspector_PopulateEquipmentPanel_Patch
{
    [HarmonyPrefix]
    private static void Prefix(CarInspector __instance, UIPanelBuilder builder)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return;

        builder.HStack(hstack =>
        {
            hstack.AddButtonCompact("Copy Repair Dest", delegate
            {
                Car selectedCar = __instance._car;

                bool hasDestination = selectedCar.TryGetOverrideDestination(
                    OverrideDestination.Repair,
                    OpsController.Shared,
                    out (OpsCarPosition, string)? destination);

                int updatedCount = selectedCar
                    .EnumerateCoupled()
                    .Where(car => car.id != selectedCar.id)
                    .Select(car =>
                    {
                        if (hasDestination)
                        {
                            car.SetOverrideDestination(
                                OverrideDestination.Repair,
                                destination);
                        }
                        else
                        {
                            car.SetOverrideDestination(
                                OverrideDestination.Repair,
                                null);
                        }

                        return 1;
                    })
                    .Sum();

                Multiplayer.SendError(
                    StateManager.Shared.PlayersManager.LocalPlayer,
                    hasDestination
                        ? $"Copied repair destination to {updatedCount} connected {"car".Pluralize(updatedCount == 1 ? 1 : 0)}."
                        : $"Cleared repair destination from {updatedCount} connected {"car".Pluralize(updatedCount == 1 ? 1 : 0)}.",
                    default);

                hstack.Rebuild();
            })
            .Tooltip(
                "Copy Repair Destination",
                "Copies or clears this car's repair destination across all connected cars in the consist.");
        });
    }
}