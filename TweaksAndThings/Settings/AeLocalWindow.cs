using System;
using System.Collections.Generic;
using System.Linq;
using Game.State;
using Model;
using Model.AI;
using Model.Ops;
using Track;
using UI;
using UI.Builder;
using UI.Common;
using UI.EngineControls;
using UI.Waypoints;
using UI.Windows;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal sealed class AeLocalWindow
{
    private static readonly AeLocalWindow instance = new();
    private static CustomWindow? customWindow;
    private static bool registered;
    private AeLocalJob? job;
    internal static void Show(AeLocalJob job)
    {
        var manager = WindowManager.Shared;
        if (manager == null) return;
        if (!registered || !manager.HasDefinition("TweaksAndThings.AeLocal"))
        {
            manager.RegisterCustomWindow(new CustomWindowDefinition
            {
                Identifier = "TweaksAndThings.AeLocal",
                Title = "AE Local",
                Config = WindowConfig.Resizable(new Vector2Int(900, 700), new Vector2Int(650, 450)),
                BuilderAction = instance.Build
            });
            registered = true;
        }
        instance.job = job;
        customWindow = manager.ShowCustomWindow("TweaksAndThings.AeLocal");
        customWindow.Window.SetResizable(new Vector2(650, 450), new Vector2(int.MaxValue, int.MaxValue));
        customWindow.Window.Title = "AE Local · " + (AeLocalService.CarFor(job.LeadId)?.DisplayName ?? "Job");
        customWindow.SetNeedsRebuild();
    }
    internal static void Refresh()
    {
        if (instance.job == null) return;
        customWindow ??= WindowManager.Shared?.GetCustomWindow("TweaksAndThings.AeLocal");
        if (customWindow == null) return;
        customWindow.Window.Title = "AE Local · " + (AeLocalService.CarFor(instance.job.LeadId)?.DisplayName ?? "Job");
        customWindow.SetNeedsRebuild();
    }
    private void Build(UIPanelBuilder builder)
    {
        ResponsiveModText.Install(builder._container);
        if (job == null) return;
        var current = job;
        builder.AddField("Progress", () => AeLocalService.Summary(current), UIPanelBuilder.Frequency.Periodic);
        builder.AddField("Current work", () => current.Status, UIPanelBuilder.Frequency.Periodic);
        builder.AddField("Plan", () => current.PlanDetail, UIPanelBuilder.Frequency.Periodic);
        builder.AddField("Engineer", () => current.NativeStatus, UIPanelBuilder.Frequency.Periodic);
        builder.AddField("Deferred cars", () => current.YardRemainder.Count + " · remain assigned until worked", UIPanelBuilder.Frequency.Periodic);
        builder.AddField("Local crew", () => current.CrewId.Length == 0 ? "Uses locomotive crew; AEL crew created at start if needed" :
            StateManager.Shared.PlayersManager.TrainCrewForId(current.CrewId, out var crew) ? crew.Name + " · switch list follows working order" : "Crew unavailable",
            UIPanelBuilder.Frequency.Periodic);
        builder.AddField("Maximum speed", () => current.MaxSpeedMph + " mph · track, signal and safety limits still apply", UIPanelBuilder.Frequency.Periodic);
        builder.AddLabel("Physical switching only. A blocked move holds for dispatcher/player assistance. Normal fuel, wear and safety rules apply.");
        if (!StateManager.IsHost) { builder.AddLabel("The host manages this local job."); return; }
        var speeds = Enumerable.Range(0, 8).Select(i => i * 5).ToList();
        builder.AddField("Job speed limit", builder.AddDropdown(speeds.Select(s => s + " mph").ToList(),
            Math.Max(0, Math.Min(7, current.MaxSpeedMph / 5)), index =>
            { if (AeLocalService.CarFor(current.LeadId) is BaseLocomotive lead) AeLocalService.SetMaxSpeed(lead, speeds[index]); }));
        builder.AddLabel("The job limit remains editable during setup and switching. Coupling approaches use at most 3 mph; setup and paused jobs stay held.");
        builder.HVScrollView(list =>
        {
            if (current.Phase == "Setup")
            {
                list.AddLabel("Select industries in surrounding areas. Checked industries supply pickup visits. Coupled and nearby pending waybills can be delivered to other destinations too.");
                var origin = OpsController.Shared.Areas.FirstOrDefault(a => a.identifier == current.HomeArea);
                var industries = AeLocalService.Industries().OrderBy(i => i.Components.Select(c => Vector3.SqrMagnitude(
                    c.CenterPoint - (origin?.Position ?? Vector3.zero))).DefaultIfEmpty(float.MaxValue).Min()).ToList();
                foreach (var area in industries.GroupBy(i => OpsController.Shared.ClosestAreaForGamePosition(i.Components.First().CenterPoint)))
                {
                    list.AddLabel(area.Key?.name ?? "Other area");
                    foreach (var industry in area)
                    {
                        string id = industry.identifier;
                        list.AddFieldToggle(industry.name, () => current.IndustryIds.Contains(id), value =>
                        { if (value && !current.IndustryIds.Contains(id)) current.IndustryIds.Add(id); else if (!value) current.IndustryIds.Remove(id); NpcServiceStore.Save(); });
                    }
                }
                list.AddFieldToggle("Grab additional nearby setouts/pickups if power allows", () => current.IncludeNearby,
                    value => { current.IncludeNearby = value; NpcServiceStore.Save(); });
                list.AddLabel("Extra cars are snapshotted at start. Oversized pickup strings are collected in smaller batches where power and storage allow.");
                list.AddButton("Set yard return track…", () => Pick(current, "yard"));
                list.AddField("Yard return target", () => current.YardPoint.Length == 0 ? "Required · select a track in the originating yard area" : current.YardPoint, UIPanelBuilder.Frequency.Periodic);
                list.AddButton("Set final engine parking waypoint…", () => Pick(current, "parking"));
                list.AddField("Final parking target", () => current.ParkingPoint.Length == 0 ? "Required" : current.ParkingPoint, UIPanelBuilder.Frequency.Periodic);
                list.AddButton("Start AE Local", () => { AeLocalService.Start(current); Refresh(); });
            }
            else
            {
                foreach (var task in current.Tasks)
                    list.AddLabel((task.Skipped ? "Skipped · " : task.Done ? "✓ " : task.Collected ? "Collected · " : "Pending · ") +
                        (AeLocalService.CarFor(task.CarId)?.DisplayName ?? task.CarId) + " → " +
                        AeLocalService.DestinationLabel(task));
                list.AddButton(current.Paused ? "Retry blocked work" : "Pause job", () =>
                {
                    current.Paused = !current.Paused;
                    if (AeLocalService.CarFor(current.LeadId) is BaseLocomotive lead)
                    { if (current.Paused) NpcTrainOperations.Hold(lead); else current.RestoreOrders = true; }
                    NpcServiceStore.Save(); Refresh();
                });
            }
            list.AddButton(current.Phase == "Complete" ? "Dismiss completed job" : "Cancel AE Local and release equipment", () =>
            { AeLocalService.Cancel(current); customWindow?.Window.CloseWindow(); });
        });
    }
    private void Pick(AeLocalJob job, string kind)
    {
        if (AeLocalService.CarFor(job.LeadId) is not BaseLocomotive lead) return;
        job.PickingTarget = kind;
        NpcTrainOperations.Hold(lead);
        NpcServiceStore.Save();
        customWindow?.Window.CloseWindow();
        NpcTrainOperations.Trusted(() => AutoEngineerDestinationPicker.Shared.StartPickingLocation(lead,
            new AutoEngineerOrdersHelper(lead, new AutoEngineerPersistence(lead.KeyValueObject))));
        Toast.Present(kind == "yard" ? "Select a named yard track in the local's originating area." : "Select where the engine unit should park after the job.");
    }
    internal static bool Configure(LocomotiveControlsUIAdapter adapter)
    {
        var job = AeLocalService.Job(LocomotiveControlsUIAdapter.Locomotive);
        if (job == null || job.Phase == "Complete") return false;
        adapter.modeDropdown.gameObject.SetActive(false);
        foreach (var set in adapter._controlSets) set.gameObject.SetActive(false);
        adapter.optionsDropdown.Configure(new List<DropdownMenu.RowData> { new("AE Local job / status", null), new("Cancel AE Local", null) },
            action => { if (action == 0) Show(job); else AeLocalService.Cancel(job); });
        return true;
    }

    internal static void Inspector(Car car, UIPanelBuilder builder)
    {
        var job = AeLocalService.Job(car);
        if (job == null)
        {
            builder.AddLabel(car.DisplayName + " — AE Local equipment");
            builder.AddLabel("This equipment is assigned to an automated AE Local job.");
            return;
        }

        builder.AddLabel(car.DisplayName + " — AE Local");
        builder.AddOptionsDropdown(
            new List<DropdownMenu.RowData> { new("AE Local job / status", null), new("Cancel AE Local", null) },
            action => { if (action == 0) Show(job); else AeLocalService.Cancel(job); });
        builder.AddField("Status", () => AeLocalService.Summary(job), UIPanelBuilder.Frequency.Periodic);
        builder.AddLabel("This equipment is assigned to an automated local job and its locomotive controls are locked.");
    }
}
