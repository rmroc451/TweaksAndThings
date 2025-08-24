using Game.State;
using HarmonyLib;
using Model;
using Model.Ops;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using System;
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

        if (!tweaksAndThings.IsEnabled() || !tweaksAndThings.settings.HandBrakeAndAirTagModifiers)
        {
            return;
        }

        ProceedWithPostFix(car, tagCallout, tweaksAndThings.CabooseRequiredForLocoOilIndicator());

        return;
    }

    private static void ProceedWithPostFix(Car car, TagCallout tagCallout, bool cabooseRequired)
    {
        tagCallout.callout.Title = string.Format(tagTitleFormat, "{0}", Hyperlink.To(car));
        List<string> tags = new();
        string oilSpriteName = string.Empty;// "OilCan";

        if (OpsController_AnnounceCoalescedPayments_Patch.CrewCarStatus(car).spotted) tags.Add("+");
        //if (car.EnableOiling) tags.Add(car.HasHotbox ? TextSprites.Hotbox : $"<cspace=-1em>{TextSprites.Warning}{car.Oiled.TriColorPiePercent(1)}</cspace>");
        if (car.EnableOiling) tags.Add(car.HasHotbox ? TextSprites.Hotbox : car.Oiled.TriColorPiePercent(1, oilSpriteName));
        IEnumerable<Car> consist = car.EnumerateCoupled().Where(c => c.EnableOiling);
        Func<bool> cabooseRequirementFulfilled = () => (!cabooseRequired || consist.ConsistNoFreight() || car.FindMyCaboose(0.0f, false)); 
        if (StateManager.Shared.Storage.OilFeature
            && car.IsLocomotive 
            && !car.NeedsOiling 
            && (consist.Any(c => c.NeedsOiling) || consist.Any(c => c.HasHotbox)
            && cabooseRequirementFulfilled())
        ) 
            tags.Add(consist.Any(c => c.HasHotbox) ? TextSprites.Hotbox : consist.OrderBy(c => c.Oiled).FirstOrDefault().Oiled.TriColorPiePercent(1, oilSpriteName));
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
