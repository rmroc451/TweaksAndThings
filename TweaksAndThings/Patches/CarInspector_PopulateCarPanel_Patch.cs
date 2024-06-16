using GalaSoft.MvvmLight.Messaging;
using Game;
using Game.Events;
using Game.Messages;
using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Network;
using Network.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Railloader;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using TweaksAndThings.Enums;
using UI.Builder;
using UI.CarInspector;
using WorldStreamer2;
using static Model.Car;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

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

        builder.HStack(delegate (UIPanelBuilder hstack)
        {
            hstack.AddButtonCompact("Handbrakes", delegate
            {
                MrocConsistHelper(__instance._car, MrocHelperType.Handbrake);
            }).Tooltip("Release Consist Handbrakes", "Iterates over each car in this consist and releases handbrakes.");
            if (StateManager.IsHost || true)
            {
                hstack.AddButtonCompact("Air Lines", delegate
                {
                    MrocConsistHelper(__instance._car, MrocHelperType.GladhandAndAnglecock);
                }).Tooltip("Connect Consist Air Lines", "Iterates over each car in this consist and connects gladhands and opens anglecocks.");
            }
        });

        return true;
    }

    public static void MrocConsistHelper(Model.Car car, MrocHelperType mrocHelperType)
    {
        IEnumerable<Model.Car> consist = car.EnumerateCoupled(LogicalEnd.A);

        Log.Information($"{car} => {mrocHelperType} => {string.Join("/", consist.Select(c => c.ToString()))}");
        switch (mrocHelperType)
        {
            case MrocHelperType.Handbrake:
                consist.Do(c => c.SetHandbrake(false));
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
