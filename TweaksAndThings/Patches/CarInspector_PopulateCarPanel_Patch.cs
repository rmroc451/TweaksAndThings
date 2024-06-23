using Game.Messages;
using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Model;
using Model.OpsNew;
using Railloader;
using RMROC451.TweaksAndThings.Enums;
using RMROC451.TweaksAndThings.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using UI.Builder;
using UI.CarInspector;
using UI.Tags;
using static Model.Car;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(CarInspector))]
[HarmonyPatch(nameof(CarInspector.PopulateCarPanel), typeof(UIPanelBuilder))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
public class CarInspector_PopulateCarPanel_Patch
{
    private static IEnumerable<LogicalEnd> ends = Enum.GetValues(typeof(LogicalEnd)).Cast<LogicalEnd>();

    /// <summary>
    /// If a caboose inspector is opened, it will auto set Anglecocks, gladhands and hand brakes
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="builder"></param>
    /// <returns></returns>
	private static bool Prefix(CarInspector __instance, UIPanelBuilder builder)
    {

        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
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
                hstack.AddButtonCompact("Connect Air", delegate
                {
                    MrocConsistHelper(__instance._car, MrocHelperType.GladhandAndAnglecock);
                    hstack.Rebuild();
                }).Tooltip("Connect Consist Air", "Iterates over each car in this consist and connects gladhands and opens anglecocks.");
            }

            hstack.AddButtonCompact("Bleed Consist", delegate
            {
                MrocConsistHelper(__instance._car, MrocHelperType.BleedAirSystem);
                hstack.Rebuild();
            }).Tooltip("Bleed Air Lines", "Iterates over each car in this consist and bleeds the air out of the lines.");
        });

        return true;
    }

    private static UIPanelBuilder AddCarConsistRebuildObservers(UIPanelBuilder builder, IEnumerable<Model.Car> consist)
    {
        TagController tagController = UnityEngine.Object.FindFirstObjectByType<TagController>();
        foreach (Model.Car car in consist)
        {
            builder = AddObserver(builder, car, PropertyChange.KeyForControl(PropertyChange.Control.Handbrake), tagController);
            foreach (LogicalEnd logicalEnd in ends)
            {
                builder = AddObserver(builder, car, KeyValueKeyFor(EndGearStateKey.IsCoupled, car.LogicalToEnd(logicalEnd)), tagController);
                builder = AddObserver(builder, car, KeyValueKeyFor(EndGearStateKey.IsAirConnected, car.LogicalToEnd(logicalEnd)), tagController);
                builder = AddObserver(builder, car, KeyValueKeyFor(EndGearStateKey.Anglecock, car.LogicalToEnd(logicalEnd)), tagController);
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
                    tagController.UpdateTag(car, car.TagCallout, OpsController.Shared);
                    builder.Rebuild();
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

    public static void MrocConsistHelper(Model.Car car, MrocHelperType mrocHelperType)
    {
        IEnumerable<Model.Car> consist = car.EnumerateCoupled(LogicalEnd.A);

        Log.Information($"{car} => {mrocHelperType} => {string.Join("/", consist.Select(c => c.ToString()))}");
        TrainController tc = UnityEngine.Object.FindObjectOfType<TrainController>();
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
                    Log.Information($"{car} => {mrocHelperType} => {string.Join("/", consist.Select(c => c.ToString()))}");
                    //when ApplyHandbrakesAsNeeded is called, and the consist contains an engine, it stops applying brakes.
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

            case MrocHelperType.BleedAirSystem:
                consist = consist.Where(c => c.NotMotivePower());
                Log.Information($"{car} => {mrocHelperType} => {string.Join("/", consist.Select(c => c.ToString()))}");
                foreach (Model.Car bleed in consist)
                {
                    StateManager.ApplyLocal(new PropertyChange(bleed.id, PropertyChange.Control.Bleed, 1));
                }
                break;
        }
    }
}
