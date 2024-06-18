using Game.Messages;
using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Railloader;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TweaksAndThings.Enums;
using UI.Builder;
using UI.CarInspector;
using static Model.Car;

namespace TweaksAndThings.Patches;

[HarmonyPatch(typeof(CarInspector))]
[HarmonyPatch(nameof(CarInspector.PopulateCarPanel), typeof(UIPanelBuilder))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
public class CarInspector_PopulateCarPanel_Patch
{
    private static IEnumerable<LogicalEnd> ends = Enum.GetValues(typeof(LogicalEnd)).Cast<LogicalEnd>();

    /// <summary>
    /// If a caboose inspector is opened, it will auto set Anglecocks, gladhands and handbrakes
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="builder"></param>
    /// <returns></returns>
	private static bool Prefix(CarInspector __instance, UIPanelBuilder builder)
    {

        TweaksAndThings tweaksAndThings = SingletonPluginBase<TweaksAndThings>.Shared;
        if (!tweaksAndThings.IsEnabled) return true;

        var consist = __instance._car.EnumerateCoupled(LogicalEnd.A);
        builder = AddCarConsistRebuildObservers(builder, consist);

        builder.HStack(delegate (UIPanelBuilder hstack)
        {
            var buttonName = $"{(consist.Any(c => c.HandbrakeApplied()) ? "Release " : "Set ")} {TextSprites.HandbrakeWheel}";
            hstack.AddButtonCompact(buttonName, delegate
            {
                MrocConsistHelper(__instance._car, MrocHelperType.Handbrake);
                hstack.Rebuild();
            }).Tooltip(buttonName, $"Iterates over cars in this consist and {(consist.Any(c => c.HandbrakeApplied()) ? "releases" : "sets")} {TextSprites.HandbrakeWheel}.");

            if (consist.Any(c => c.EndAirSystemIssue()))
            {
                hstack.AddButtonCompact("Connect Air Lines", delegate
                {
                    MrocConsistHelper(__instance._car, MrocHelperType.GladhandAndAnglecock);
                    hstack.Rebuild();
                }).Tooltip("Connect Consist Air Lines", "Iterates over each car in this consist and connects gladhands and opens anglecocks.");
            }
        });

        return true;
    }

    private static UIPanelBuilder AddCarConsistRebuildObservers(UIPanelBuilder builder, IEnumerable<Model.Car> consist)
    {
        foreach (Model.Car car in consist)
        {
            builder = AddObserver(builder, car, PropertyChange.KeyForControl(PropertyChange.Control.Handbrake));
            foreach (LogicalEnd logicalEnd in ends)
            {
                builder = AddObserver(builder, car, KeyValueKeyFor(EndGearStateKey.IsCoupled, car.LogicalToEnd(logicalEnd)));
                builder = AddObserver(builder, car, KeyValueKeyFor(EndGearStateKey.IsAirConnected, car.LogicalToEnd(logicalEnd)));
                builder = AddObserver(builder, car, KeyValueKeyFor(EndGearStateKey.Anglecock, car.LogicalToEnd(logicalEnd)));
            }
        }

        return builder;
    }

    private static UIPanelBuilder AddObserver(UIPanelBuilder builder, Model.Car car, string key)
    {
        builder.AddObserver(
            car.KeyValueObject.Observe(
                key,
                delegate (Value value)
                {
                    builder.Rebuild();
                },
                false
            )
        );

        return builder;
    }

    public static void MrocConsistHelper(Model.Car car, MrocHelperType mrocHelperType)
    {
        IEnumerable<Model.Car> consist = car.EnumerateCoupled(LogicalEnd.A);

        Log.Information($"{car} => {mrocHelperType} => {string.Join("/", consist.Select(c => c.ToString()))}");
        switch (mrocHelperType)
        {
            case MrocHelperType.Handbrake:
                if (consist.Any(c => c.HandbrakeApplied()))
                {
                    consist.Do(c => c.SetHandbrake(false));
                } else
                {
                    TrainController tc = UnityEngine.Object.FindObjectOfType<TrainController>();
                    tc.ApplyHandbrakesAsNeeded(consist.ToList(), PlaceTrainHandbrakes.Automatic);
                }
                break;

            case MrocHelperType.GladhandAndAnglecock:
                consist.Do(c =>
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

                        if (c.TryGetAdjacentCar(end, out Model.Car c2))
                        {
                            StateManager.ApplyLocal(new SetGladhandsConnected(c.id, c2.id, true));
                        }

                    })
                );
                break;
        }
    }
}
