using System;
using System.Collections.Generic;
using System.Linq;
using Game.State;
using Model;
using Model.Ops;
using RMROC451.TweaksAndThings.Patches;
using TMPro;
using UI;
using UI.Builder;
using UI.Common;
using UI.Tooltips;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RMROC451.TweaksAndThings;

internal sealed class NpcTrafficWindow : MonoBehaviour, IProgrammaticWindow
{
    private static NpcTrafficWindow? instance;
    private static Button? toolbarButton;
    private static float nextToolbarCheck;
    private Window window = null!;
    private UIPanel? panel;
    private readonly UIState<string> selected = new("active");
    public string WindowIdentifier => "TweaksAndThings.AITraffic";
    public Vector2Int DefaultSize => new(850, 550);
    public Window.Position DefaultPosition => Window.Position.Center;
    public Window.Sizing Sizing => Window.Sizing.Resizable(new Vector2Int(600, 350));
    public UIBuilderAssets BuilderAssets { get; set; } = null!;

    private void Awake()
    {
        window = GetComponent<Window>();
        instance = this;
        // Publicizer exposes the backing field under the event's name as well.
        typeof(Window).GetEvent("OnShownWillChange")!.AddEventHandler(window, (Action<bool>)(shown =>
        {
            if (!shown && !window.IsShown) { panel?.Dispose(); panel = null; }
        }));
    }
    private void OnDisable() { panel?.Dispose(); panel = null; }

    internal static void UpdateToolbar()
    {
        if (Time.realtimeSinceStartup < nextToolbarCheck) return;
        nextToolbarCheck = Time.realtimeSinceStartup + 1;
        if (toolbarButton != null) return;
        var area = Object.FindFirstObjectByType<TopRightArea>();
        if (area == null || area.timetableButton == null) return;
        // Use the game's button for sizing, hover behavior and toolbar layout.
        toolbarButton = Object.Instantiate(area.timetableButton, area.timetableButton.transform.parent, false);
        toolbarButton.name = "TweaksAndThings AI Traffic";
        toolbarButton.onClick = new Button.ButtonClickedEvent();
        toolbarButton.onClick.AddListener(() => Toggle());
        toolbarButton.interactable = true;
        var layout = toolbarButton.transform.parent.GetComponent<HorizontalLayoutGroup>();
        if (layout != null && layout.reverseArrangement) toolbarButton.transform.SetAsLastSibling();
        else toolbarButton.transform.SetAsFirstSibling();
        // Preserve the native icon and its raycast surface; disabling it makes the button unclickable.
        if (toolbarButton.targetGraphic != null) toolbarButton.targetGraphic.raycastTarget = true;
        foreach (var tooltip in toolbarButton.GetComponentsInChildren<UITooltipProvider>(true))
            tooltip.TooltipInfo = new TooltipInfo("AI traffic / interchanges", "Active NPC trains, interchange schedules, delinquent pickups and traffic settings.");
        if (toolbarButton.GetComponent<UITooltipProvider>() == null)
            toolbarButton.gameObject.AddComponent<UITooltipProvider>().TooltipInfo = new TooltipInfo("AI traffic / interchanges", "Open traffic management.");
        toolbarButton.gameObject.SetActive(true);
    }

    internal static void Show(string tab = "active")
    {
        if (instance == null)
        {
            var creator = Object.FindFirstObjectByType<ProgrammaticWindowCreator>();
            if (creator == null) { Toast.Present("Load a railroad to open AI traffic."); return; }
            creator.CreateWindow<NpcTrafficWindow>();
        }
        instance!.selected.Value = tab;
        instance.window.Title = "AI traffic / interchanges";
        instance.panel?.Dispose();
        instance.panel = UIPanel.Create(instance.window.contentRectTransform, instance.BuilderAssets, instance.Build);
        instance.window.ShowWindow();
    }

    private static void Toggle()
    {
        if (instance != null && instance.window.IsShown) instance.window.CloseWindow();
        else Show();
    }

    internal static void Hide()
    {
        if (toolbarButton != null) Object.Destroy(toolbarButton.gameObject);
        toolbarButton = null;
        if (instance != null) instance.window.CloseWindow();
    }

    private void Build(UIPanelBuilder builder) => BuildContents(builder, selected);

    internal static void BuildContents(UIPanelBuilder builder, UIState<string> selected)
    {
        builder.AddTabbedPanels(selected, tabs =>
        {
            tabs.AddTab("Active traffic", "active", BuildActive);
            tabs.AddTab("Interchanges", "interchanges", BuildInterchanges);
            tabs.AddTab("Delinquent pickups", "delinquent", TimetableDelinquentPickups_Patch.BuildDelinquents);
            tabs.AddTab("Held deliveries", "held", panel =>
            {
                panel.RebuildOnInterval(3);
                panel.HVScrollView(list => NpcHeldDeliveryTracking.BuildList(list));
            });
            tabs.AddTab("Settings", "settings", BuildSettings);
            tabs.AddTab("Debug", "debug", BuildDiagnostics);
        });
    }

    private static void BuildActive(UIPanelBuilder builder)
    {
        builder.RebuildOnInterval(3);
        builder.HVScrollView(list =>
        {
            if (!NpcServiceStore.Load()) { list.AddLabel("Load a railroad to see traffic."); return; }
            foreach (var service in NpcServiceStore.State.Services.ToList())
            {
                var interchange = OpsController.Shared?.AllInterchanges.FirstOrDefault(i => i.Identifier == service.InterchangeId);
                list.AddSection(interchange?.DisplayName ?? (service.TrainSymbol.Length > 0 ? service.TrainSymbol : service.InterchangeId), section =>
                {
                    section.AddLabel(NpcServiceHud.DisplayPhase(service));
                    if (TrainController.Shared.TryGetCarForId(service.LeadId, out var lead))
                        section.AddButtonCompact("Locate " + lead.DisplayName, () => CameraSelector.shared.ZoomToCar(lead));
                    if (interchange != null)
                    {
                        section.AddLabel($"Setouts {service.SetoutDone}/{service.Inbound.Count}; pickups {service.Outbound.Take(service.PickupDone).Count(p => !p.Missed)}/{service.PickupSnapshot.Count}; missed {service.Outbound.Count(p => p.Missed)}");
                        section.AddLabel("Scheduled service: " + NpcRandomTrafficPolicy.Clock(service.Scheduled));
                    }
                    if (service.WaitingReason.Length > 0) section.AddLabel(service.WaitingReason);
                });
            }
            if (NpcServiceStore.State.Services.Count == 0) list.AddLabel("No active AI traffic.");
        });
    }

    private static void BuildInterchanges(UIPanelBuilder builder)
    {
        builder.RebuildOnInterval(3);
        builder.HVScrollView(list =>
        {
            if (OpsController.Shared == null) { list.AddLabel("Load a railroad to see interchanges."); return; }
            list.AddButtonCompact("Warp to next interchange AI", NpcInterchangeWarp.Request).Disable(!StateManager.IsHost || !NpcInterchangeWarp.Enabled);
            list.AddLabel(NpcInterchangeWarp.Summary, UIPanelBuilder.Frequency.Periodic);
            foreach (var interchange in OpsController.Shared.EnabledInterchanges)
                list.AddSection(interchange.DisplayName, section =>
                {
                    var due = interchange.GetNextServiceTime(Game.TimeWeather.Now, out _);
                    var active = NpcServiceStore.Load() ? NpcServiceStore.State.Services.FirstOrDefault(s => s.InterchangeId == interchange.Identifier) : null;
                    section.AddField("Next service", NpcRandomTrafficPolicy.Clock(active?.Scheduled ?? due.TotalSeconds));
                    section.AddField("Cars awaiting delivery", interchange.Orders.Sum(o => o.CarCount).ToString());
                    if (NpcServiceStore.Load()) {
                        foreach (var pool in NpcServiceStore.State.PoolPower.Where(p => p.InterchangeId == interchange.Identifier)) {
                            var pooledLead = pool.PowerIds.Select(id => TrainController.Shared.CarForId(id)).OfType<BaseLocomotive>().FirstOrDefault();
                            section.AddField("Parked pool power", pool.PowerIds.Count + " vehicles · waiting for next outbound service");
                            if (pooledLead != null) section.AddButtonCompact("Locate pool power", () => CameraSelector.shared.ZoomToCar(pooledLead));
                        }
                        foreach (var outbound in NpcServiceStore.State.Services.Where(s => s.InterchangeId == interchange.Identifier && s.PoolOutbound))
                            section.AddField(outbound.TrainSymbol + " · outbound pool service", NpcServiceHud.DisplayPhase(outbound) + $" · pickup {outbound.PickupDone}/{outbound.Outbound.Count}");
                    }
                    var settings = TweaksAndThingsPlugin.Instance.settings;
                    if (NpcTrainOperations.TryApproach(interchange, settings, out var spawn, out var target, out var seconds))
                    {
                        if (active != null)
                        { spawn = Track.Graph.Shared.ResolveLocationString(active.Spawn); target = Track.Graph.Shared.ResolveLocationString(active.Target); }
                        section.AddField("Approach", NpcTrainOperations.ApproachSummary(interchange.Identifier));
                        section.AddField("Dispatch time", NpcRandomTrafficPolicy.Clock(NpcServicePolicy.DispatchTime(due.TotalSeconds, seconds)));
                        section.AddField("Navigation", section.AddOptionsDropdown(new List<DropdownMenu.RowData>
                        {
                            new("Locate spawn", null), new("Teleport to spawn", null),
                            new("Locate interchange service track", null), new("Teleport to interchange service track", null)
                        }, action =>
                        {
                            if (action == 0) CameraSelector.shared.ZoomToPoint(spawn.GetPosition());
                            else if (action == 1) Teleport(spawn);
                            else if (action == 2) CameraSelector.shared.ZoomToPoint(target.GetPosition());
                            else if (action == 3) Teleport(target);
                        }));
                    }
                    else section.AddField("Approach", NpcTrainOperations.ApproachSummary(interchange.Identifier));
                    section.AddField("Service status", active == null ? NpcTrafficDiagnostics.Status(interchange.Identifier).Split(';').Last().Trim() :
                        NpcServiceHud.DisplayPhase(active) + (active.WaitingReason.Length == 0 ? "" : " — " + active.WaitingReason));
                    NpcHeldDeliveryTracking.BuildList(section, interchangeId: interchange.Identifier);
                });
        });
    }

    private static void BuildSettings(UIPanelBuilder builder)
    {
        builder.AddButtonCompact("Report a bug / feature", () => Application.OpenURL("https://github.com/rmroc451/TweaksAndThings/issues"));
        var settings = TweaksAndThingsPlugin.Instance?.settings;
        if (settings == null) return;
        if (ModSettingsAuthority.HostControlsLocked) builder.AddLabel("Host only — these settings are read-only for multiplayer clients.");
        builder.HVScrollView(list =>
        {
            list.AddFieldToggle("Timetable AI traffic", () => settings.ThroughTrafficEnabled, value => { settings.ThroughTrafficEnabled = value; SaveSettings(); });
            list.AddFieldToggle("Random through freights", () => settings.RandomThroughFreightsEnabled, value => { settings.RandomThroughFreightsEnabled = value; SaveSettings(); });
            list.AddField("Random traffic multiplier", list.AddInputField(settings.RandomThroughFreightMultiplier.ToString(), value =>
            { if (float.TryParse(value, out float multiplier) && !float.IsNaN(multiplier)) { settings.RandomThroughFreightMultiplier = Math.Max(0, Math.Min(10, multiplier)); SaveSettings(); } }));
            list.AddField("Daily through-freight range", () =>
            { var range = NpcRandomThroughFreights.Range(settings); return $"{range.min}–{range.max} trains/day at current industry tiers"; }, UIPanelBuilder.Frequency.Periodic);
            list.AddLabel("Range grows with total active contract tiers. Times and classes are randomized once per game day and saved; multiplier changes affect the next day's plan.");
            list.AddFieldToggle("Random first class", () => settings.RandomThroughFreightFirstClass, value => { settings.RandomThroughFreightFirstClass = value; SaveSettings(); });
            list.AddFieldToggle("Random second class", () => settings.RandomThroughFreightSecondClass, value => { settings.RandomThroughFreightSecondClass = value; SaveSettings(); });
            list.AddFieldToggle("Random third class", () => settings.RandomThroughFreightThirdClass, value => { settings.RandomThroughFreightThirdClass = value; SaveSettings(); });
            list.AddField("Random freight minimum cars", list.AddInputField(settings.RandomThroughFreightMinCars.ToString(), value =>
            { if (int.TryParse(value, out int cars)) { settings.RandomThroughFreightMinCars = Math.Max(1, Math.Min(50, cars)); settings.RandomThroughFreightMaxCars = Math.Max(settings.RandomThroughFreightMinCars, settings.RandomThroughFreightMaxCars); SaveSettings(); } }));
            list.AddField("Random freight maximum cars", list.AddInputField(settings.RandomThroughFreightMaxCars.ToString(), value =>
            { if (int.TryParse(value, out int cars)) { settings.RandomThroughFreightMaxCars = Math.Max(settings.RandomThroughFreightMinCars, Math.Min(50, cars)); SaveSettings(); } }));
            list.AddField("Pulpwood ordering fee (%)", list.AddInputField(settings.PulpwoodOrderingFeePercent.ToString(), value =>
            { if (int.TryParse(value, out int percent)) { settings.PulpwoodOrderingFeePercent = Math.Max(0, Math.Min(100, percent)); SaveSettings(); } }));
            list.AddField("Interchange service", list.AddDropdown(new List<string> { "Default (auto-magic)", "Simulated delivery / pickup" },
                (int)settings.InterchangeService, value => { settings.InterchangeService = (InterchangeServiceMode)value; SaveSettings(); }));
            list.AddLabel("NPC traffic runs with or without CTC. Active services finish after switching modes.");
            list.AddField("Held delivery deadline (hours)", list.AddInputField(settings.HeldDeliveryDeadlineHours.ToString(), value =>
            { if (int.TryParse(value, out int hours)) { settings.HeldDeliveryDeadlineHours = Math.Max(1, Math.Min(72, hours)); SaveSettings(); } }));
            list.AddField("Held delivery premium (%)", list.AddInputField(settings.HeldDeliveryPremiumPercent.ToString(), value =>
            { if (int.TryParse(value, out int percent)) { settings.HeldDeliveryPremiumPercent = Math.Max(0, Math.Min(100, percent)); SaveSettings(); } }));
            list.AddLabel("Held-over cars earn the premium when delivered to their industry by the deadline after their next interchange service.");
            list.AddField("Automatic spawn", list.AddDropdown(new List<string> { "Nearest map / track edge", "Farthest reachable track edge" },
                (int)settings.InterchangeSpawnMode, value => { settings.InterchangeSpawnMode = (NpcSpawnMode)value; SaveSettings(); }));
            list.AddLabel("Power is chosen from the catalog for each route and load. NPCs obey track limits and are exempt from caboose speed restrictions. Nearby cabooses halve interchange transfer time at no charge.");
            list.AddField("Freight reference car", list.AddInputField(settings.ThroughFreightReferenceCar ?? "", value => { settings.ThroughFreightReferenceCar = value; SaveSettings(); }, "Car ID or reporting mark / number", 64));
            list.AddField("Timetable symbol prefix", list.AddInputField(settings.ThroughTrafficTrainSymbolPrefix, value => { settings.ThroughTrafficTrainSymbolPrefix = value; SaveSettings(); }, "NPC", 32));
            list.AddLabel("Marked first-class timetable trains must begin and end at interchanges.");
            list.AddField("On-time grace (minutes)", list.AddInputField(settings.ThroughTrafficOnTimeGraceMinutes.ToString(), value =>
            { if (int.TryParse(value, out int minutes)) { settings.ThroughTrafficOnTimeGraceMinutes = Math.Max(0, Math.Min(60, minutes)); SaveSettings(); } }));
            list.AddField("Reward / penalty per passenger ($)", list.AddInputField(settings.ThroughTrafficDollarsPerPassenger.ToString(), value =>
            { if (int.TryParse(value, out int amount)) { settings.ThroughTrafficDollarsPerPassenger = Math.Max(0, Math.Min(100, amount)); SaveSettings(); } }));
            foreach (var interchange in OpsController.Shared?.EnabledInterchanges ?? Enumerable.Empty<Interchange>())
            {
                var approach = settings.InterchangeApproaches.Find(a => a.InterchangeId == interchange.Identifier);
                if (approach == null) { approach = new InterchangeApproachOverride { InterchangeId = interchange.Identifier }; settings.InterchangeApproaches.Add(approach); }
                list.AddSection(interchange.DisplayName + " overrides (blank = automatic)", section =>
                {
                    section.AddField("Spawn / exit", section.AddInputField(approach.SpawnLocation, value => { approach.SpawnLocation = value; SaveSettings(); }, "Game track-location string", 128));
                    section.AddField("Service point", section.AddInputField(approach.ServiceLocation, value => { approach.ServiceLocation = value; SaveSettings(); }, "Game track-location string", 128));
                });
            }
            if (ModSettingsAuthority.HostControlsLocked)
                foreach (var control in list._container.GetComponentsInChildren<Selectable>(true)) control.interactable = false;
        });
    }

    private static void SaveSettings() => TweaksAndThingsPlugin.SaveTrafficSettings();

    private static void Teleport(Track.Location location)
    {
        var point = Track.Graph.Shared.GetPositionRotation(location);
        CameraSelector.shared.JumpToPoint(point.Position + Vector3.up + point.Rotation * Vector3.right * 3, point.Rotation, CameraSelector.CameraIdentifier.FirstPerson);
    }

    private static void BuildDiagnostics(UIPanelBuilder builder)
    {
        builder.AddLabel("Debug file: " + ModDiagnosticLog.FilePath);
        builder.AddLabel("Share this file and its .previous file to diagnose loading or service blockers.");
        builder.AddButtonCompact("Open log file", () =>
        {
            if (!System.IO.File.Exists(ModDiagnosticLog.FilePath)) { Toast.Present("The debug log has not been created yet."); return; }
            Application.OpenURL(new Uri(ModDiagnosticLog.FilePath).AbsoluteUri);
        });
        builder.AddButtonCompact("Write debug snapshot", () => ModDiagnosticLog.Write("MANUAL SNAPSHOT", NpcTrafficDiagnostics.Snapshot()));
        builder.AddLabel("Latest 160 lines (up to 32 KB); refreshes once per second while this tab is visible.");
        var reader = new ModLogTailReader(ModDiagnosticLog.FilePath);
        TMP_Text text = null!;
        var scroll = builder.HVScrollView(list =>
        {
            var label = list.AddLabel(() => reader.Read(DateTime.UtcNow), UIPanelBuilder.Frequency.Periodic).GetComponent<TMP_Text>();
            text = label;
            label.richText = false;
            label.fontSize = 13;
        });
        scroll.gameObject.AddComponent<NpcDebugAutoScroll>().Label = text;
    }
}
