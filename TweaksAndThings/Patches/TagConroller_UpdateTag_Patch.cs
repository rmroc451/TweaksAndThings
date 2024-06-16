using HarmonyLib;
using JetBrains.Annotations;
using Model;
using Model.AI;
using Model.OpsNew;
using Railloader;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Track;
using TweaksAndThings;
using UI;
using UI.Builder;
using UI.CarInspector;
using UI.Tags;
using UnityEngine;
using static Model.Car;
using tat = TweaksAndThings.TweaksAndThings;

namespace TweaksAndThings.Patches;

[HarmonyPatch(typeof(TagController))]
[HarmonyPatch(nameof(TagController.UpdateTag), typeof(Car), typeof(TagCallout), typeof(OpsController))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
public class TagConroller_UpdateTag_Patch
{
    private static void Postfix(Car car, TagCallout tagCallout)
    {
        TagController tagController = UnityEngine.Object.FindObjectOfType<TagController>();
        TweaksAndThings tweaksAndThings = SingletonPluginBase<TweaksAndThings>.Shared;
        tagCallout.callout.Title = $"<align=left>{car.DisplayName}<line-height=0>";

        if (!tweaksAndThings.IsEnabled || !tweaksAndThings.settings.HandBrakeAndAirTagModifiers)
        {
            return;
        }

        tagCallout.gameObject.SetActive(
            tagCallout.gameObject.activeSelf && 
            (!GameInput.IsShiftDown || (GameInput.IsShiftDown && car.CarOrEndGearIssue()))
        );
        if (tagCallout.gameObject.activeSelf && GameInput.IsShiftDown && car.CarOrEndGearIssue()) {

            tagController.ApplyImageColor(tagCallout, Color.black);
        }

        if (car.CarAndEndGearIssue())
        {
            tagCallout.callout.Title =
                    $"{tagCallout.callout.Title}\n<align=\"right\">{TextSprites.CycleWaybills}{TextSprites.HandbrakeWheel}";
        }
        else if (car.EndAirSystemIssue())
                tagCallout.callout.Title =
                    $"{tagCallout.callout.Title}\n<align=\"right\">{TextSprites.CycleWaybills}";
        else if (car.HandbrakeApplied())
                tagCallout.callout.Title =
                    $"{tagCallout.callout.Title}\n<align=\"right\">{TextSprites.HandbrakeWheel}";
            

        return;
    }
}

public static class ModelCarExtensions
{
    public static bool EndAirSystemIssue(this Model.Car car)
    {
        bool AEndAirSystemIssue = car[Car.LogicalEnd.A].IsCoupled && !car[Car.LogicalEnd.A].IsAirConnectedAndOpen;
        bool BEndAirSystemIssue = car[Car.LogicalEnd.B].IsCoupled && !car[Car.LogicalEnd.B].IsAirConnectedAndOpen;
        bool EndAirSystemIssue = AEndAirSystemIssue || BEndAirSystemIssue;
        return EndAirSystemIssue;
    }

    public static bool HandbrakeApplied(this Model.Car car) =>
        car.air.handbrakeApplied;

    public static bool CarOrEndGearIssue(this Model.Car car) =>
        car.EndAirSystemIssue() || car.HandbrakeApplied();
    public static bool CarAndEndGearIssue(this Model.Car car) =>
        car.EndAirSystemIssue() && car.HandbrakeApplied();
}
