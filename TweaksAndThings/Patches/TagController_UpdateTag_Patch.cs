using HarmonyLib;
using Model;
using Model.OpsNew;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using System.Collections.Generic;
using System.Linq;
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
        List<string> tags = [];

        if (OpsController_AnnounceCoalescedPayments_Patch.CrewCarStatus(car).spotted) tags.Add("+");
        if (car.HasHotbox) tags.Add(TextSprites.Hotbox);
        if (car.EndAirSystemIssue()) tags.Add(TextSprites.CycleWaybills);
        if (car.HandbrakeApplied()) tags.Add(TextSprites.HandbrakeWheel);

        tagCallout.callout.Title =
            tags.Any() switch
            {
                true => $"{tagCallout.callout.Title}{tagTitleAndIconDelimeter}{string.Join("", tags)}".Replace("{0}", tags.Count().ToString()),
                _ => car.DisplayName
            };
    }
}
