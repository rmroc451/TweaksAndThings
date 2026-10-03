using System.Collections.Generic;
using System.Linq;
using Game.State;
using Model;
using Model.Ops;
using TMPro;
using UI;
using UI.Builder;
using UI.Common;
using RMROC451.TweaksAndThings.Extensions;
using UI.SwitchList;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal sealed class WaybillSummaryWindow : MonoBehaviour, IProgrammaticWindow
{
    private static WaybillSummaryWindow? instance;
    private Window window = null!;
    private UIPanel? panel;
    private string? carId;
    private readonly UIState<string> tab = new("consist");
    public string WindowIdentifier => "TweaksAndThings.WaybillSummary";
    public Vector2Int DefaultSize => new(760, 520);
    public Window.Position DefaultPosition => Window.Position.Center;
    public Window.Sizing Sizing => Window.Sizing.Resizable(new Vector2Int(550, 300));
    public UIBuilderAssets BuilderAssets { get; set; } = null!;

    private void Awake()
    {
        window = GetComponent<Window>(); instance = this;
        typeof(Window).GetEvent("OnShownWillChange")!.AddEventHandler(window, (System.Action<bool>)(shown =>
        { if (!shown && !window.IsShown) { panel?.Dispose(); panel = null; } }));
    }
    private void OnDisable() { panel?.Dispose(); panel = null; }

    internal static void Show(Car? caboose = null)
    {
        if (instance == null)
        {
            var creator = Object.FindFirstObjectByType<ProgrammaticWindowCreator>();
            if (creator == null) { Toast.Present("Load a railroad to see the waybill summary."); return; }
            creator.CreateWindow<WaybillSummaryWindow>();
        }
        instance!.carId = caboose?.id;
        instance.tab.Value = caboose == null ? "switch" : "consist";
        instance.window.Title = "Waybill / switch list summary";
        instance.panel?.Dispose();
        instance.panel = UIPanel.Create(instance.window.contentRectTransform, instance.BuilderAssets, instance.Build);
        instance.window.ShowWindow();
    }

    private void Build(UIPanelBuilder builder)
    {
        builder.AddTabbedPanels(tab, tabs =>
        {
            tabs.AddTab("Caboose consist", "consist", b => BuildSummary(b, false));
            tabs.AddTab("Current switch list", "switch", b => BuildSummary(b, true));
        });
    }

    private void BuildSummary(UIPanelBuilder builder, bool switchList)
    {
        builder.RebuildOnInterval(3);
        builder.HVScrollView(content =>
        {
            if (OpsController.Shared == null || TrainController.Shared == null) { content.AddLabel("Load a railroad."); return; }
            var entries = new OpsCarList();
            if (switchList)
            {
                var crew = StateManager.Shared?.PlayersManager?.MyTrainCrew;
                if (crew == null) { content.AddLabel("Join a train crew to view its switch list."); return; }
                // Read the native list, including repair bills and cars without waybills.
                if (StateManager.IsHost)
                    entries.Rebuild(OpsController.Shared.SwitchListController._switchLists.TryGetValue(crew.Id, out var list)
                        ? list.Where(c => c != null).Select(c => c.Id).ToList() : new List<string>());
                else entries.Rebuild(SwitchListPanel.Shared._switchList.Entries.Select(e => e.CarId).ToList());
            }
            else
            {
                if (carId == null || !TrainController.Shared.TryGetCarForId(carId, out var caboose))
                { content.AddLabel("Open this summary from a caboose to select a consist."); return; }
                content.AddLabel(caboose.DisplayName + " · " + caboose.VelocityMphAbs.ToString("N0") + " mph");
                entries.Rebuild(caboose.EnumerateCoupled().Where(c => !c.MotivePower() && c.Archetype != Model.Definition.CarArchetype.Tender).Select(c => c.id));
            }
            var cars = Resolve(entries.Entries);
            content.AddLabel("Total: " + Metrics(cars) + $" · {entries.Entries.Count(e => e.Completed)} completed");
            if (entries.Entries.Count == 0) { content.AddLabel("No cars in this list."); return; }
            content.AddSection("By destination", section => Groups(section, entries.Entries, false));
            content.AddSection("By current location", section => Groups(section, entries.Entries, true));
        });
    }

    private static List<Car> Resolve(IEnumerable<OpsCarList.Entry> entries) => entries.Select(e => TrainController.Shared.CarForId(e.CarId)).Where(c => c != null).ToList();

    private static string Metrics(List<Car> cars)
    {
        int loaded = cars.Count(c => Enumerable.Range(0, c.Definition.LoadSlots.Count).Any(i => c.GetLoadInfo(i)?.Quantity > 0));
        return $"{cars.Count} cars · {loaded} loaded / {cars.Count - loaded} empty · {LocomotiveControlsHoverArea.CalculateTonnage(cars):N0}T · {Mathf.CeilToInt(LocomotiveControlsHoverArea.CalculateLengthInMeters(cars) * 3.28084f):N0}ft";
    }

    private static void Groups(UIPanelBuilder builder, List<OpsCarList.Entry> entries, bool current)
    {
        foreach (var group in entries.GroupBy(e =>
        {
            var location = current ? e.Current : e.Destination;
            return (location.Subtitle ?? "Unassigned") + " / " + location.Title;
        }).OrderBy(g => g.Key))
        {
            var first = group.First();
            var location = current ? first.Current : first.Destination;
            builder.AddSection(group.Key, section =>
            {
                section.AddLabel(Metrics(Resolve(group)));
                section.HStack(row =>
                {
                    row.AddButtonCompact("Locate", () => CameraSelector.shared.ZoomToPoint(location.Position));
                    row.AddLabel(string.Join(", ", group.Select(e => e.CarSortName))).FlexibleWidth();
                });
            });
        }
    }
}
