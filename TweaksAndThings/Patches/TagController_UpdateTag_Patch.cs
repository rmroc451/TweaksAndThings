using HarmonyLib;
using Model;
using Model.OpsNew;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using UI.Tags;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(TagController))]
[HarmonyPatch(nameof(TagController.UpdateTag), typeof(Car), typeof(TagCallout), typeof(OpsController))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class TagController_UpdateTag_Patch
{
    private const string tagTitleAndIconDelimeter = "\n<width=100%><align=\"right\">";
    private const string tagTitleFormat = "<align=left><margin-right={0}.5em>{1}</margin><line-height=0>";

    private static void Postfix(Car car, TagCallout tagCallout)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;

        if (!tweaksAndThings.IsEnabled || !tweaksAndThings.settings.HandBrakeAndAirTagModifiers)
        {
            return;
        }

        ProceedWithPostFix(car, tagCallout);

        return;
    }

    private static void ProceedWithPostFix(Car car, TagCallout tagCallout)
    {
        tagCallout.callout.Title = string.Format(tagTitleFormat, "{0}", car.DisplayName);

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
