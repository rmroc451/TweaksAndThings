// Ignore Spelling: RMROC

using HarmonyLib;
using Game;
using Game.State;
using RMROC451.TweaksAndThings.Commands;
using RMROC451.TweaksAndThings.Enums;
using System;
using System.Net.Http;
using UnityEngine;
using UnityModManagerNet;

namespace RMROC451.TweaksAndThings;

public sealed class TweaksAndThingsPlugin
{
    private static Harmony? harmony;
    private static UnityModManager.ModEntry? modEntry;
    private static HttpClient? client;

    public static TweaksAndThingsPlugin? Instance { get; private set; }

    internal HttpClient Client => client ??= new HttpClient();
    internal Settings? settings { get; private set; }
    internal bool IsEnabled { get; private set; }
    public string ModDirectory => modEntry?.Path ?? string.Empty;

    internal static void LogException(string message, Exception exception) =>
        modEntry?.Logger.LogException(message, exception);

    private TweaksAndThingsPlugin() { }

    public static bool Load(UnityModManager.ModEntry entry)
    {
        try
        {
            modEntry = entry;
            Instance = new TweaksAndThingsPlugin
            {
                settings = UnityModManager.ModSettings.Load<Settings>(entry) ?? new Settings()
            };
            Instance.NormalizeSettings();

            harmony = new Harmony(entry.Info.Id);
            entry.OnToggle = OnToggle;
            entry.OnGUI = OnGUI;
            entry.OnSaveGUI = OnSaveGUI;

            if (entry.Enabled)
            {
                harmony.PatchCategory(entry.Info.Id.Replace(".", string.Empty));
                Instance.IsEnabled = true;
            }

            entry.Logger.Log("Tweaks and Things loaded for UnityModManager.");
            return true;
        }
        catch (Exception exception)
        {
            entry.Logger.LogException("Failed to load Tweaks and Things", exception);
            return false;
        }
    }

    private void NormalizeSettings()
    {
        settings ??= new Settings();
        settings.WebhookSettingsList = SettingsExtensions.SanitizeEmptySettings(settings.WebhookSettingsList);
        settings.EngineRosterFuelColumnSettings ??= new RosterFuelColumnSettings();
    }

    private static bool OnToggle(UnityModManager.ModEntry entry, bool value)
    {
        try
        {
            var plugin = Instance;
            if (plugin == null || harmony == null) return false;
            if (plugin.IsEnabled == value) return true;
            if (value)
            {
                harmony.PatchCategory(entry.Info.Id.Replace(".", string.Empty));
            }
            else
            {
                harmony.UnpatchAll(entry.Info.Id);
            }
            plugin.IsEnabled = value;
            return true;
        }
        catch (Exception exception)
        {
            entry.Logger.LogException(value ? "Failed to enable Tweaks and Things" : "Failed to disable Tweaks and Things", exception);
            return false;
        }
    }

    private static void OnSaveGUI(UnityModManager.ModEntry entry)
    {
        var plugin = Instance;
        if (plugin?.settings == null) return;
        plugin.NormalizeSettings();
        plugin.settings.Save(entry);
    }

    private static int selectedTab;
    private static readonly string[] Tabs = { "Caboose Mods", "UI", "Webhooks", "Crew Update", "Keybindings" };
    private static readonly string[] CrewLoadMethods = { "Tracks", "Daily" };
    private static readonly string[] FuelColumns = { "None", "Engine", "Crew", "Status" };
    private static string crewUpdateMessage = string.Empty;
    private static bool includeCrewUpdateArea = true;

    private static void OnGUI(UnityModManager.ModEntry entry)
    {
        var settings = Instance?.settings;
        if (settings == null) return;
        Instance!.NormalizeSettings();

        GUILayout.Label("Adjustments to the base game");
        GUILayout.Label("Repair tracks service cars without a waybill by default. Cars without a work order show 'No Work Order Assigned'.");
        GUILayout.Label("Car icons in the engine controls support the same modifier-click actions as cars in the world.");
        GUILayout.Space(8f);
        selectedTab = GUILayout.Toolbar(selectedTab, Tabs);
        GUILayout.Space(8f);

        switch (selectedTab)
        {
            case 0: DrawCabooseSettings(settings); break;
            case 1: DrawUiSettings(settings); break;
            case 2: DrawWebhookSettings(settings); break;
            case 3: DrawCrewUpdate(); break;
            case 4: DrawKeybindings(settings); break;
        }
    }

    private static void DrawCrewUpdate()
    {
        GUILayout.Label("Compose and send the same formatted locomotive status update provided by /cu.");
        crewUpdateMessage = GUILayout.TextField(crewUpdateMessage, 512);
        includeCrewUpdateArea = GUILayout.Toggle(includeCrewUpdateArea, "Include the current area in the message");
        if (string.IsNullOrWhiteSpace(crewUpdateMessage))
            GUILayout.Label("Enter a message before sending.");
        else if (GUILayout.Button("Send crew update"))
        {
            var command = new EchoCommand();
            command.Execute(new[] { "/cu", ".", includeCrewUpdateArea ? "+" : "-", crewUpdateMessage });
        }
    }

    private static void DrawKeybindings(Settings settings)
    {
        GUILayout.Label("These bindings replace the hard-coded click modifiers. Hold the binding while clicking a car or map location.");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Alt action modifier", GUILayout.Width(160f));
        UnityModManager.UI.DrawKeybinding(ref settings.ClickAltBinding);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Control action modifier", GUILayout.Width(160f));
        UnityModManager.UI.DrawKeybinding(ref settings.ClickControlBinding);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Shift action modifier", GUILayout.Width(160f));
        UnityModManager.UI.DrawKeybinding(ref settings.ClickShiftBinding);
        GUILayout.EndHorizontal();
        GUILayout.Label("Defaults are Left Alt, Left Control, and Left Shift. The existing click combinations still work by holding multiple action modifiers together.");
    }

    private static void DrawCabooseSettings(Settings settings)
    {
        settings.EndGearHelpersRequirePayment = GUILayout.Toggle(settings.EndGearHelpersRequirePayment, "Caboose Use / Charge crew for end-gear helpers");
        GUILayout.Label("Charges one minute of AI brake crew and caboose crew time per car. A missing sufficiently crewed caboose adds a 1.5x time cost. Crew hours refill at tracks after 30 seconds stopped, or once per day, depending on the refill option.");
        if (settings.EndGearHelpersRequirePayment)
        {
            GUILayout.Label("Crew hours refill method");
            var loadMethod = GUILayout.SelectionGrid((int)settings.LoadCrewHoursMethod, CrewLoadMethods, CrewLoadMethods.Length);
            if (Enum.IsDefined(typeof(CrewHourLoadMethod), loadMethod)) settings.LoadCrewHoursMethod = (CrewHourLoadMethod)loadMethod;
        }

        settings.RequireConsistCabooseForOilerAndHotboxSpotter = GUILayout.Toggle(settings.RequireConsistCabooseForOilerAndHotboxSpotter, "Require a caboose for Auto Oiler and Hotbox Spotter");
        settings.CabooseRequiredForLocoTagOilIndication = GUILayout.Toggle(settings.CabooseRequiredForLocoTagOilIndication, "Require a caboose for locomotive consist oil indication");
        settings.SafetyFirst = GUILayout.Toggle(settings.SafetyFirst, "Safety First: require a caboose for higher Auto Engineer speeds");
        if (settings.SafetyFirst)
            settings.SafetyFirstClientEnforce = GUILayout.Toggle(settings.SafetyFirstClientEnforce, "Enforce Safety First speed limits for remote clients");
        settings.CabooseAllowsConsistInfo = GUILayout.Toggle(settings.CabooseAllowsConsistInfo, "Allow caboose consist information");
    }

    private static void DrawUiSettings(Settings settings)
    {
        settings.HandBrakeAndAirTagModifiers = GUILayout.Toggle(settings.HandBrakeAndAirTagModifiers, "Enable tag updates for air, handbrake, oil, and hotbox status");
        settings.AllowRepairsWithoutWaybill = GUILayout.Toggle(settings.AllowRepairsWithoutWaybill, "Allow repair-track service without a waybill (on by default)");
        settings.ServicingFundPenalty = GUILayout.Toggle(settings.ServicingFundPenalty, "Allow repair-track servicing with insufficient funds (20% overdraft fee)");
        settings.TrainBrakeDisplayShowsColorsInCalloutMode = GUILayout.Toggle(settings.TrainBrakeDisplayShowsColorsInCalloutMode, "Show train brake colors in callout mode");
        settings.DisableWaypointControls = GUILayout.Toggle(settings.DisableWaypointControls, "Disable waypoint controls");

        GUILayout.Space(6f);
        GUILayout.Label("Fuel display in engine roster");
        var current = (int)(settings.EngineRosterFuelColumnSettings?.EngineRosterFuelStatusColumn ?? EngineRosterFuelDisplayColumn.None);
        var selected = GUILayout.SelectionGrid(current, FuelColumns, FuelColumns.Length);
        if (Enum.IsDefined(typeof(EngineRosterFuelDisplayColumn), selected))
            settings.EngineRosterFuelColumnSettings!.EngineRosterFuelStatusColumn = (EngineRosterFuelDisplayColumn)selected;
        settings.EngineRosterFuelColumnSettings!.EngineRosterShowsFuelStatusAlways = GUILayout.Toggle(
            settings.EngineRosterFuelColumnSettings.EngineRosterShowsFuelStatusAlways,
            "Always show fuel status (otherwise hold Alt to display)");
    }

    private static void DrawWebhookSettings(Settings settings)
    {
        GUILayout.Label("Webhook messages are sent for the currently loaded railroad reporting mark.");
        var rows = settings.WebhookSettingsList!;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            GUILayout.Label($"Webhook {i + 1}");
            row.WebhookEnabled = GUILayout.Toggle(row.WebhookEnabled, "Enabled");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Reporting mark", GUILayout.Width(110f));
            row.RailroadMark = GUILayout.TextField(row.RailroadMark ?? string.Empty, GameStorage.ReportingMarkMaxLength);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Webhook URL", GUILayout.Width(110f));
            row.WebhookUrl = GUILayout.TextField(row.WebhookUrl ?? string.Empty);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }
        settings.WebhookSettingsList = SettingsExtensions.SanitizeEmptySettings(rows);
    }
}
