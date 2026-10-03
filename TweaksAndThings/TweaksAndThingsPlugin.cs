// Ignore Spelling: RMROC

using HarmonyLib;
using Game;
using Game.State;
using Model.Ops;
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

    internal static void LogException(string message, Exception exception)
    {
        ModDiagnosticLog.Write("ERROR", message + "\n" + exception, "exception:" + message + exception.Message);
        modEntry?.Logger.LogException(message, exception);
        modEntry?.Logger.Log(exception.ToString());
    }

    internal static void LogDiagnostic(string message)
    {
        ModDiagnosticLog.Write("INFO", message);
        modEntry?.Logger.Log(message);
    }

    private TweaksAndThingsPlugin() { }

    public static bool Load(UnityModManager.ModEntry entry)
    {
        try
        {
            modEntry = entry;
            ModDiagnosticLog.Initialize(entry.Path, text => entry.Logger.Log(text));
            ModDiagnosticLog.Write("SESSION", $"Mod {entry.Info.Version}; directory={entry.Path}; Unity={Application.unityVersion}");
            Instance = new TweaksAndThingsPlugin
            {
                settings = UnityModManager.ModSettings.Load<Settings>(entry) ?? new Settings()
            };
            Instance.NormalizeSettings();
            ThroughTrafficSpawner.Reset();
            NpcServiceStore.Reset();

            harmony = new Harmony(entry.Info.Id);
            entry.OnToggle = OnToggle;
            entry.OnGUI = OnGUI;
            entry.OnSaveGUI = OnSaveGUI;
            entry.OnUpdate = OnUpdate;

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
        if (string.IsNullOrWhiteSpace(settings.ThroughTrafficTrainSymbolPrefix))
            settings.ThroughTrafficTrainSymbolPrefix = ThroughTrafficPolicy.DefaultTrainSymbolPrefix;
        settings.ThroughTrafficOnTimeGraceMinutes = Math.Max(0, Math.Min(60, settings.ThroughTrafficOnTimeGraceMinutes));
        settings.ThroughTrafficDollarsPerPassenger = Math.Max(0, Math.Min(100, settings.ThroughTrafficDollarsPerPassenger));
        if (!Enum.IsDefined(typeof(InterchangeServiceMode), settings.InterchangeService)) settings.InterchangeService = InterchangeServiceMode.Automatic;
        if (!Enum.IsDefined(typeof(NpcSpawnMode), settings.InterchangeSpawnMode)) settings.InterchangeSpawnMode = NpcSpawnMode.NearestMapEdge;
        settings.InterchangeApproaches ??= new System.Collections.Generic.List<InterchangeApproachOverride>();
        settings.HeldDeliveryDeadlineHours = Math.Max(1, Math.Min(72, settings.HeldDeliveryDeadlineHours));
        settings.HeldDeliveryPremiumPercent = Math.Max(0, Math.Min(100, settings.HeldDeliveryPremiumPercent));
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
                NpcServiceHud.Hide();
                NpcSimulationSpeed.Reset();
                NpcTrafficRouting.Reset();
                NpcTrafficWindow.Hide();
                BrysonCtcMirrors.Remove();
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

    internal static void SaveTrafficSettings()
    {
        if (modEntry != null) OnSaveGUI(modEntry);
    }

    private static readonly float[] nextSlowUpdateNotice = new float[3];
    private static void ReportUpdateDuration(UnityModManager.ModEntry entry, int component, string name, long start)
    {
        double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        if (milliseconds < 50 || UnityEngine.Time.realtimeSinceStartup < nextSlowUpdateNotice[component]) return;
        nextSlowUpdateNotice[component] = UnityEngine.Time.realtimeSinceStartup + 30;
        entry.Logger.Log($"Slow mod update: {name} took {milliseconds:F1} ms.");
        ModDiagnosticLog.Write("PERFORMANCE", $"{name} took {milliseconds:F1} ms.");
    }

    private static void OnUpdate(UnityModManager.ModEntry entry, float deltaTime)
    {
        var plugin = Instance;
        if (plugin?.IsEnabled != true || plugin.settings == null) return;
        try
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            NpcSimulationSpeed.Update();
            if (StateManager.IsHost) NpcDailyTrafficPlans.Tick(plugin.settings, Game.TimeWeather.Now.TotalSeconds);
            ThroughTrafficSpawner.Tick(plugin.settings, deltaTime);
            ReportUpdateDuration(entry, 0, "timetable traffic", start);
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            if (StateManager.IsHost) NpcTrainOperations.AdvancePlanning();
            SimulatedInterchangeService.Tick(plugin.settings, deltaTime);
            ReportUpdateDuration(entry, 1, "interchange service", start);
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            NpcTrafficWindow.UpdateToolbar();
            BrysonCtcMirrors.Update();
            NpcServiceHud.Update();
            TimetableHistory.Update();
            NpcTrafficDiagnostics.UpdateFileSnapshot();
            ReportUpdateDuration(entry, 2, "NPC HUD", start);
        }
        catch (Exception exception)
        {
            LogException("Through traffic update failed", exception);
        }
    }

    private static int selectedTab;
    private static readonly string[] Tabs = { "Caboose Mods", "UI", "Webhooks", "Crew Update", "Keybindings", "AI Traffic" };
    private static readonly string[] CrewLoadMethods = { "Tracks", "Daily" };
    private static readonly string[] FuelColumns = { "None", "Engine", "Crew", "Status" };
    private static string crewUpdateMessage = string.Empty;
    private static bool includeCrewUpdateArea = true;

    private static void OnGUI(UnityModManager.ModEntry entry)
    {
        if (GUILayout.Button("Report a bug / feature")) Application.OpenURL("https://github.com/rmroc451/TweaksAndThings/issues");
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
            case 5:
                GUILayout.Label("AI traffic and interchange controls are in their own game window, opened with the leftmost AI button in the top-right toolbar.");
                if (GUILayout.Button("Open AI traffic / interchanges")) NpcTrafficWindow.Show();
                GUILayout.Label("Diagnostic log: " + ModDiagnosticLog.FilePath);
                break;
        }
    }

    private static void DrawThroughTrafficSettings(Settings settings)
    {
        if (GUILayout.Button("Open delinquent pickup list (/npcPickups)")) NpcDelinquentPickupWindow.Show();
        settings.ThroughTrafficEnabled = GUILayout.Toggle(settings.ThroughTrafficEnabled, "Generate AI through traffic from marked timetable trains");
        GUILayout.Label("Interchange service");
        settings.InterchangeService = (InterchangeServiceMode)GUILayout.Toolbar((int)settings.InterchangeService,
            new[] { "Default (auto-magic)", "Simulated delivery / pickup" });
        GUILayout.Label("Simulated service runs with or without CTC. Telegraph messages announce NPC departures and approaches. Active services finish when switching modes.");
        GUILayout.Label("Automatic interchange spawn location");
        settings.InterchangeSpawnMode = (NpcSpawnMode)GUILayout.Toolbar((int)settings.InterchangeSpawnMode,
            new[] { "Nearest map / track edge", "Farthest reachable track edge" });
        GUILayout.Label("Locomotives are randomly selected from available consist-placer definitions and sized for the route. Manual location overrides are optional.");
        GUILayout.Label("NPCs obey track limits and are exempt from caboose speed restrictions. A caboose near the interchange halves transfer time at no cost.");
        GUILayout.Label("Through freight reference car ID or reporting mark / number (uses its coupled freight consist)");
        settings.ThroughFreightReferenceCar = GUILayout.TextField(settings.ThroughFreightReferenceCar ?? string.Empty, 64);
        if (OpsController.Shared != null)
        {
            foreach (var interchange in OpsController.Shared.EnabledInterchanges)
            {
                var approach = settings.InterchangeApproaches.Find(a => a.InterchangeId == interchange.Identifier);
                if (approach == null)
                {
                    approach = new InterchangeApproachOverride { InterchangeId = interchange.Identifier };
                    settings.InterchangeApproaches.Add(approach);
                }
                GUILayout.Label(interchange.DisplayName + " approach overrides (blank = automatic; game track-location strings)");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Spawn / exit", GUILayout.Width(100));
                approach.SpawnLocation = GUILayout.TextField(approach.SpawnLocation ?? string.Empty, 128);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Service point", GUILayout.Width(100));
                approach.ServiceLocation = GUILayout.TextField(approach.ServiceLocation ?? string.Empty, 128);
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.Label("The generator scans first-class timetable rows whose train symbol starts with the prefix. Both endpoints must be interchanges. NPC traffic runs with or without CTC.");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Train symbol prefix", GUILayout.Width(150f));
        settings.ThroughTrafficTrainSymbolPrefix = GUILayout.TextField(settings.ThroughTrafficTrainSymbolPrefix ?? string.Empty, 32);
        GUILayout.EndHorizontal();
        GUILayout.Label("Arrival on-time grace period (minutes)");
        settings.ThroughTrafficOnTimeGraceMinutes = (int)GUILayout.HorizontalSlider(settings.ThroughTrafficOnTimeGraceMinutes, 0, 60);
        GUILayout.Label($"{settings.ThroughTrafficOnTimeGraceMinutes} minutes");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Schedule reward / penalty per passenger", GUILayout.Width(250f));
        settings.ThroughTrafficDollarsPerPassenger = (int)GUILayout.HorizontalSlider(settings.ThroughTrafficDollarsPerPassenger, 0, 100);
        GUILayout.Label($"${settings.ThroughTrafficDollarsPerPassenger}");
        GUILayout.EndHorizontal();
        GUILayout.Label("Passenger services use route demand to size a random load; way freight services use a reference consist. Engines are selected and multiplied to meet the route grade power requirement.");
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
        bool enabled = GUI.enabled;
        if (ModSettingsAuthority.HostControlsLocked) GUILayout.Label("Host only — shared gameplay settings are read-only.");
        GUI.enabled = enabled && !ModSettingsAuthority.HostControlsLocked;
        settings.EndGearHelpersRequirePayment = GUILayout.Toggle(settings.EndGearHelpersRequirePayment, "Caboose Use / Charge crew for end-gear helpers");
        GUILayout.Label("Charges one minute of AI brake crew and caboose crew time per car. A missing sufficiently crewed caboose adds a 1.5x time cost. Crew hours refill at tracks after 30 seconds stopped, or once per day, depending on the refill option.");
        if (settings.EndGearHelpersRequirePayment)
        {
            GUILayout.Label("Crew hours refill method");
            var loadMethod = GUILayout.SelectionGrid((int)settings.LoadCrewHoursMethod, CrewLoadMethods, CrewLoadMethods.Length);
            if (Enum.IsDefined(typeof(CrewHourLoadMethod), loadMethod)) settings.LoadCrewHoursMethod = (CrewHourLoadMethod)loadMethod;
        }

        settings.RequireConsistCabooseForOilerAndHotboxSpotter = GUILayout.Toggle(settings.RequireConsistCabooseForOilerAndHotboxSpotter, "Require a caboose for Auto Oiler and Hotbox Spotter");
        settings.SafetyFirst = GUILayout.Toggle(settings.SafetyFirst, "Safety First: require a caboose for higher Auto Engineer speeds");
        if (settings.SafetyFirst)
            settings.SafetyFirstClientEnforce = GUILayout.Toggle(settings.SafetyFirstClientEnforce, "Enforce Safety First speed limits for remote clients");
        GUI.enabled = enabled;
        settings.CabooseRequiredForLocoTagOilIndication = GUILayout.Toggle(settings.CabooseRequiredForLocoTagOilIndication, "Require a caboose for locomotive consist oil indication");
        settings.CabooseAllowsConsistInfo = GUILayout.Toggle(settings.CabooseAllowsConsistInfo, "Allow caboose consist information");
    }

    private static void DrawUiSettings(Settings settings)
    {
        settings.HandBrakeAndAirTagModifiers = GUILayout.Toggle(settings.HandBrakeAndAirTagModifiers, "Enable tag updates for air, handbrake, oil, and hotbox status");
        bool enabled = GUI.enabled;
        if (ModSettingsAuthority.HostControlsLocked) GUILayout.Label("Host only — repair service settings are read-only.");
        GUI.enabled = enabled && !ModSettingsAuthority.HostControlsLocked;
        settings.AllowRepairsWithoutWaybill = GUILayout.Toggle(settings.AllowRepairsWithoutWaybill, "Allow repair-track service without a waybill (on by default)");
        settings.ServicingFundPenalty = GUILayout.Toggle(settings.ServicingFundPenalty, "Allow repair-track servicing with insufficient funds (20% overdraft fee)");
        GUI.enabled = enabled;
        settings.ShowWaypointSetNotifications = GUILayout.Toggle(settings.ShowWaypointSetNotifications, "Show WP SET notifications");
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
