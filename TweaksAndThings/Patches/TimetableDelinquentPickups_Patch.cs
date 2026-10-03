using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Model;
using Model.Ops;
using UI;
using UI.Builder;
using UI.Timetable;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(TimetableWindow), "Build")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class TimetableDelinquentPickups_Patch
{
    private sealed class Tabs
    {
        internal readonly UIState<string> Selected = new("timetable");
        internal readonly UIState<string> TrafficSelected = new("active");
        internal readonly TimetableStringChart.View Chart = new();
    }
    private static readonly ConditionalWeakTable<TimetableWindow, Tabs> views = new();
    private static bool Prefix(TimetableWindow __instance, UIPanelBuilder builder)
    {
        var view = views.GetValue(__instance, _ => new Tabs());
        if (__instance._firstBuild)
        {
            __instance._firstBuild = false;
            __instance._window.SetContentWidth(1000);
        }
        builder.AddTabbedPanels(view.Selected, tabs =>
        {
            tabs.AddTab("Timetable", "timetable", panel => TimetableStringChart.Build(panel, view.Chart));
            tabs.AddTab("Delinquent pickups", "delinquent", BuildDelinquents);
            tabs.AddTab("AI traffic", "traffic", panel =>
            {
                __instance._window.SetContentWidth(System.Math.Max(850, (int)__instance._window.GetContentSize().x));
                NpcTrafficWindow.BuildContents(panel, view.TrafficSelected);
            });
        });
        return false;
    }

    internal static void BuildDelinquents(UIPanelBuilder builder)
    {
        builder.RebuildOnInterval(3);
        builder.AddLabel("All railroad cars — independent of train crew");
        builder.HVScrollView(list =>
        {
            if (!NpcServiceStore.Load() || OpsController.Shared == null) { list.AddLabel("Load a railroad to see delinquent pickups."); return; }
            var state = NpcServiceStore.State;
            foreach (var expected in state.DelinquentPickups.Values.OrderBy(p => p.InterchangeId).ThenBy(p => p.CarName).ToList())
            {
                var interchange = OpsController.Shared.AllInterchanges.FirstOrDefault(i => i.Identifier == expected.InterchangeId);
                list.HStack(row =>
                {
                    if (TrainController.Shared.TryGetCarForId(expected.CarId, out var car))
                    {
                        row.AddButtonCompact(expected.CarName, () => CameraSelector.shared.ZoomToCar(car)).Width(130).Height(24).Tooltip("Locate car", "Move the camera to this delinquent car.");
                        row.AddButtonCompact("+ List", () => SwitchListAccess.TryAddCars(new[] { car }, out _)).Width(60).Height(24).Tooltip("Add to switch list", "Add this car to your current crew's working switch list.");
                    }
                    else row.AddLabel(expected.CarName + " (unavailable)").Width(190);
                    row.AddButtonCompact(interchange?.DisplayName ?? expected.InterchangeId, () =>
                    { if (interchange != null) CameraSelector.shared.ZoomToPoint(interchange.CenterPoint); }).Width(155).Height(24)
                        .Tooltip("Return destination", "Pickup spans: " + string.Join(", ", expected.SpanIds));
                    row.AddLabel(state.ForfeitedPickupCars.Contains(expected.CarId) ? "Payout forfeited" : "First miss; payout at risk").Width(155);
                    row.AddLabel(() => NpcPickupTracking.NextAttemptText(expected), UIPanelBuilder.Frequency.Periodic).Width(190);
                });
            }
            if (state.DelinquentPickups.Count == 0) list.AddLabel("No delinquent pickups.");
        });
    }
}
