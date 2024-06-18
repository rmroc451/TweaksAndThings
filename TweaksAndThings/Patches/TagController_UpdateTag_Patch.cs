using HarmonyLib;
using Model;
using Model.OpsNew;
using Railloader;
using UI;
using UI.Tags;
using UnityEngine;

namespace TweaksAndThings.Patches;

[HarmonyPatch(typeof(TagController))]
[HarmonyPatch(nameof(TagController.UpdateTag), typeof(Car), typeof(TagCallout), typeof(OpsController))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
public class TagController_UpdateTag_Patch
{
    private const string tagTitleAndIconDelimeter = "\n<width=100%><align=\"right\">";
    private const string tagTitleFormat = "<align=left><margin-right={0}.5em>{1}</margin><line-height=0>";

    private static void Postfix(Car car, TagCallout tagCallout)
    {
        TagController tagController = UnityEngine.Object.FindObjectOfType<TagController>();
        TweaksAndThings tweaksAndThings = SingletonPluginBase<TweaksAndThings>.Shared;

        if (!tweaksAndThings.IsEnabled || !tweaksAndThings.settings.HandBrakeAndAirTagModifiers)
        {
            return;
        }

        ProceedWithPostFix(car, tagCallout, tagController);

        return;
    }

    private static void ProceedWithPostFix(Car car, TagCallout tagCallout, TagController tagController)
    {
        bool isAltDownWithCarIssue = GameInput.IsAltDown && car.CarOrEndGearIssue();
        tagCallout.callout.Title = string.Format(tagTitleFormat, "{0}", car.DisplayName);
        tagCallout.gameObject.SetActive(
            tagCallout.gameObject.activeSelf &&
            (!GameInput.IsAltDown || isAltDownWithCarIssue)
        );

        if (tagCallout.gameObject.activeSelf && isAltDownWithCarIssue)
        {
            tagController.ApplyImageColor(tagCallout, Color.black);
        }

        tagCallout.callout.Title =
            (car.CarAndEndGearIssue(), car.EndAirSystemIssue(), car.HandbrakeApplied()) switch
            {
                (true, _, _) => $"{tagCallout.callout.Title}{tagTitleAndIconDelimeter}{TextSprites.CycleWaybills}{TextSprites.HandbrakeWheel}".Replace("{0}", "2"),
                (_, true, _) => $"{tagCallout.callout.Title}{tagTitleAndIconDelimeter}{TextSprites.CycleWaybills}".Replace("{0}", "1"),
                (_, _, true) => $"{tagCallout.callout.Title}{tagTitleAndIconDelimeter}{TextSprites.HandbrakeWheel}".Replace("{0}", "1"),
                _ => car.DisplayName
            };
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
