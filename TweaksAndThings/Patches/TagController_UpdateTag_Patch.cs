using Game.State;
using HarmonyLib;
using KeyValue.Runtime;
using Model;
using Model.Definition.Data;
using Model.Ops;
using Model.Ops.Definition;
using RMROC451.TweaksAndThings.Extensions;
using RollingStock;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UI.Tags;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

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
        TweaksAndThingsPlugin tweaksAndThings = TweaksAndThingsPlugin.Instance!;

        if (car == null || tagCallout == null || tagCallout.callout == null ||
            tweaksAndThings?.settings == null || !tweaksAndThings.IsEnabled() || !tweaksAndThings.settings.HandBrakeAndAirTagModifiers)
        {
            return;
        }

        string previousTitle = tagCallout.callout.Title;
        string previousText = tagCallout.callout.Text;
        ProceedWithPostFix(car, tagCallout, tweaksAndThings.CabooseRequiredForLocoOilIndicator());
        if (previousTitle != tagCallout.callout.Title || previousText != tagCallout.callout.Text)
            tagCallout.callout.Layout();

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
        if (StateManager.Shared?.Storage?.OilFeature == true
            && car.IsLocomotive 
            && !car.NeedsOiling
        ) 
        {
            var consist = car.EnumerateCoupled().Where(c => c.EnableOiling).ToList();
            bool hotbox = consist.Any(c => c.HasHotbox);
            if ((hotbox || consist.Any(c => c.NeedsOiling)) &&
                (!cabooseRequired || consist.ConsistNoFreight() || car.FindMyCabooseSansLoadRequirement() != null))
                tags.Add(hotbox ? TextSprites.Hotbox : consist.OrderBy(c => c.Oiled).First().Oiled.TriColorPiePercent(1, oilSpriteName));
        }
        if (car.EndAirSystemIssue()) tags.Add(TextSprites.CycleWaybills);
        if (car.HandbrakeApplied()) tags.Add(TextSprites.HandbrakeWheel);

        if (car.IsPassengerCar())
        {
            PassengerMarker? passengerMarker = car.GetPassengerMarker();
            if (passengerMarker.HasValue)
            {
                IEnumerable<string> loadInfo = car.PassengerCountString(passengerMarker).Split('/');
                //string item4 = CarPickable.PassengerString(car, passengerMarker.Value);
                string val = TextSprites.PiePercent(float.Parse(loadInfo.First()), float.Parse(loadInfo.Last())) + $" {car.PassengerCountString(passengerMarker)} Passengers";
                string text = tagCallout.callout.Text ?? string.Empty;
                tagCallout.callout.Text = text.Contains("Empty") ? text.Replace("Empty", val) : text + $"\n{val}";
            }
        }

        tagCallout.callout.Title =
            tags.Any() switch
            {
                true => $"{tagCallout.callout.Title}{tagTitleAndIconDelimeter}{string.Join("", tags)}".Replace("{0}", tags.Count().ToString()),
                _ => car.DisplayName
            };

        // UpdateTag restores the native body each time; prepend fresh details so
        // destination and passenger information survive without accumulating lines.
        var details = new List<string>();
        var slots = car.Definition.LoadSlots;
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var info = car.GetLoadInfo(i);
                if (!info.HasValue || info.Value.Quantity <= 0) continue;
                var load = CarPrototypeLibrary.instance.LoadForId(info.Value.LoadId);
                float capacity = slots[i].MaximumCapacity;
                string amounts = load == null
                    ? $"{info.Value.Quantity:N1} / {capacity:N1} {info.Value.LoadId}"
                    : FormatLoadAmounts(info.Value.Quantity, capacity, load);
                details.Add($"{TextSprites.PiePercent(info.Value.Quantity, capacity)} {amounts}");
            }
        }
        details.Add($"{car.VelocityMphAbs:N1} mph");
        string nativeText = tagCallout.callout.Text;
        if (!string.IsNullOrEmpty(nativeText)) details.Add(nativeText);
        tagCallout.callout.Text = string.Join("\n", details);
    }

    private static string FormatLoadAmounts(float quantity, float capacity, Load load)
    {
        // Use the same unit for both quantities so partially filled heavy loads
        // do not mix pounds and tons in one fraction.
        switch (load.units)
        {
            case LoadUnits.Pounds:
                return capacity >= 200
                    ? $"{quantity / 2000f:N1} / {capacity / 2000f:N1} T {load.description}"
                    : $"{quantity:N1} / {capacity:N1} lb {load.description}";
            case LoadUnits.Gallons:
                return $"{quantity:N0} / {capacity:N0} gal {load.description}";
            default:
                string unit = load.id == "crew-hours" ? " hours" : string.Empty;
                return $"{quantity:N1} / {capacity:N1}{unit} {load.description}";
        }
    }
}
