// Ignore Spelling: RMROC

using HarmonyLib;
using Game;
using Game.State;
using Model.Ops;
using RMROC451.TweaksAndThings.Commands;
using RMROC451.TweaksAndThings.Enums;
using RMROC451.TweaksAndThings.Patches;
using Railroader.Modding;
using System;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

public sealed class TweaksAndThingsPlugin : IModEntryPoint, IWorldLifecycle
{
    private const string HarmonyId = "RMROC451.TweaksAndThings";
    private static Harmony? harmony;
    private static IModContext? modContext;
    private static string modDirectory = string.Empty;
    private static string settingsPath = string.Empty;
    private static HttpClient? client;
    private static bool initializedAfterMods;
    private static bool patchesApplied;
    private static bool loadedCrewSlotsEnsured;
    private static bool settingsWindowVisible;
    private static Vector2 settingsScroll;
    private static Rect settingsWindowRect = new Rect(80, 60, 760, 680);
    private static HotkeyBinding? bindingCapture;

    public static TweaksAndThingsPlugin? Instance { get; private set; }

    internal HttpClient Client => client ??= new HttpClient();
    internal Settings? settings { get; private set; }
    internal bool IsEnabled { get; private set; }
    public string ModDirectory => modDirectory;
    internal static string ModVersion => typeof(TweaksAndThingsPlugin).Assembly.GetName().Version?.ToString() ?? string.Empty;

    internal static void LogException(string message, Exception exception)
    {
        ModDiagnosticLog.Write("ERROR", message + "\n" + exception, "exception:" + message + exception.Message);
    }

    internal static void LogDiagnostic(string message)
    {
        ModDiagnosticLog.Write("INFO", message);
    }

    public TweaksAndThingsPlugin() { }

    public void OnModLoad(IModContext context)
    {
        try
        {
            modContext = context;
            modDirectory = context.ModDirectory;
            Directory.CreateDirectory(modDirectory);
            settingsPath = Path.Combine(modDirectory, "Settings.json");
            ModDiagnosticLog.Initialize(modDirectory);
            ModLog.Initialize();
            ModDiagnosticLog.Write("SESSION", $"Mod {ModVersion}; directory={modDirectory}; Unity={Application.unityVersion}");
            Instance = new TweaksAndThingsPlugin
            {
                settings = LoadSettings()
            };
            Instance.NormalizeSettings();
            ThroughTrafficSpawner.Reset();
            NpcServiceStore.Reset();

            harmony = new Harmony(HarmonyId);
            Instance.IsEnabled = true;
            initializedAfterMods = false;
            patchesApplied = false;
            loadedCrewSlotsEnsured = false;
            WorldPatchReadiness.WorldLoaded = false;
            var host = new GameObject("RMROC451.TweaksAndThings");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<TweaksAndThingsUpdateHost>();
            context.Log.Info($"Tweaks and Things loaded from {modDirectory}.");
        }
        catch (Exception exception)
        {
            ModDiagnosticLog.Write("ERROR", "Failed to load Tweaks and Things\n" + exception);
            context.Log.Error(exception, "Failed to load Tweaks and Things");
            throw;
        }
    }

    public void OnModUnload()
    {
        try
        {
            try { SaveSettings(); }
            catch (Exception exception) { modContext?.Log.Error(exception, "Could not save Tweaks and Things settings during unload"); }
            settingsWindowVisible = false;
            NpcServiceHud.Hide();
            NpcTrafficWindow.Hide();
            NpcSimulationSpeed.Reset();
            NpcTrafficRouting.Reset();
            BrysonCtcMirrors.Remove();
            DoubleMainlineCtc.UpdateTrainMarker(false);
            DoubleMainlineHudOverlay.Update(false);
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            WorldPatchReadiness.WorldLoaded = false;
            initializedAfterMods = false;
            patchesApplied = false;
            ModJoinCompatibility.Reset();
            if (client != null) { client.Dispose(); client = null; }
            var host = GameObject.Find("RMROC451.TweaksAndThings");
            if (host != null) UnityEngine.Object.Destroy(host);
            Instance = null;
        }
        catch (Exception exception) { modContext?.Log.Error(exception, "Failed to clean up Tweaks and Things"); }
    }

    public void OnWorldLoad()
    {
        WorldPatchReadiness.WorldLoaded = true;
        WorldPatchReadiness.Changed();
        initializedAfterMods = false;
        loadedCrewSlotsEnsured = false;
    }

    public void OnWorldUnload()
    {
        WorldPatchReadiness.WorldLoaded = false;
        initializedAfterMods = false;
        loadedCrewSlotsEnsured = false;
        NpcServiceHud.Hide();
        NpcTrafficWindow.Hide();
        NpcSimulationSpeed.Reset();
        NpcTrafficRouting.Reset();
    }

    private static Settings LoadSettings()
    {
        if (!File.Exists(settingsPath)) return new Settings();
        try { return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(settingsPath)) ?? new Settings(); }
        catch (Exception exception)
        {
            ModDiagnosticLog.Write("ERROR", "Could not read Settings.json; using defaults.\n" + exception);
            return new Settings();
        }
    }

    private static void SaveSettings()
    {
        var plugin = Instance;
        if (plugin?.settings == null || string.IsNullOrEmpty(settingsPath)) return;
        plugin.NormalizeSettings();
        File.WriteAllText(settingsPath, JsonConvert.SerializeObject(plugin.settings, Formatting.Indented));
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

    internal static void SaveTrafficSettings()
    {
        SaveSettings();
    }

    private static readonly float[] nextSlowUpdateNotice = new float[3];
    internal static void NativeLateUpdate()
    {
        if (Instance?.IsEnabled != true || initializedAfterMods || !WorldPatchReadiness.Ready) return;
        try
        {
            if (!patchesApplied)
            {
                harmony!.PatchCategory("RMROC451TweaksAndThings");
                patchesApplied = true;
            }
            loadedCrewSlotsEnsured = EnsureLoadedCabooseSlots();
            initializedAfterMods = true;
            WorldPatchReadiness.Changed();
            modContext?.Log.Info("Gameplay patches initialized after the world graph settled.");
        }
        catch (Exception ex) { LogException("Deferred mod initialization failed", ex); Instance.IsEnabled = false; }
    }
    private static void ReportUpdateDuration(int component, string name, long start)
    {
        double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        if (milliseconds < 50 || UnityEngine.Time.realtimeSinceStartup < nextSlowUpdateNotice[component]) return;
        nextSlowUpdateNotice[component] = UnityEngine.Time.realtimeSinceStartup + 30;
        ModDiagnosticLog.Write("PERFORMANCE", $"{name} took {milliseconds:F1} ms.");
    }

    private static bool EnsureLoadedCabooseSlots()
    {
        var controller = TrainController.Shared;
        if (controller == null) return false;
        foreach (var car in controller.Cars)
            if (car != null) CabooseCrewLoadSlot_Patch.EnsureCrewLoadSlot(car.Definition);
        return true;
    }

    internal static void NativeUpdate(float deltaTime)
    {
        if (bindingCapture != null)
        {
            foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
            {
                if (keyCode == KeyCode.None || !Input.GetKeyDown(keyCode)) continue;
                bindingCapture.keyCode = keyCode;
                bindingCapture.modifiers = 0;
                bindingCapture = null;
                SaveSettings();
                break;
            }
        }
        if (Input.GetKeyDown(KeyCode.F10)) settingsWindowVisible = !settingsWindowVisible;
        var plugin = Instance;
        if (plugin?.IsEnabled != true || !initializedAfterMods || plugin.settings == null) return;
        if (!loadedCrewSlotsEnsured) loadedCrewSlotsEnsured = EnsureLoadedCabooseSlots();
        try
        {
            ModJoinCompatibility.Update();
            if (NewGameStartPicker.Choosing) return;
            Patches.AutoEngineerRunaround.Tick();
            if (plugin.settings.EnableNpcInterchangeTraffic) NpcLoadRecovery.Update();
            AeLocalService.Tick();
            AeLocalNotices.Update();
            NewGameStartOptions.InspectLoadedMap();
            NpcLogOrdering.Tick();
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            DoubleMainlineRuntime.Update();
            DoubleMainlineCtc.UpdateTrainMarker(plugin.settings.AutoFollowTrainCtcMarker);
            DoubleMainlineHudOverlay.Update(plugin.settings.ShowCtcHudOverlayButton);
            if (plugin.settings.EnableNpcInterchangeTraffic) NpcSimulationSpeed.Update();
            if (plugin.settings.EnableNpcInterchangeTraffic)
            {
                if (StateManager.IsHost) NpcDailyTrafficPlans.Tick(plugin.settings, Game.TimeWeather.Now.TotalSeconds);
                ThroughTrafficSpawner.Tick(plugin.settings, deltaTime);
            }
            ReportUpdateDuration(0, "timetable traffic", start);
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            if (plugin.settings.EnableNpcInterchangeTraffic)
            {
                if (StateManager.IsHost) NpcTrainOperations.AdvancePlanning();
                SimulatedInterchangeService.Tick(plugin.settings, deltaTime);
            }
            ReportUpdateDuration(1, "interchange service", start);
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            BrysonCtcMirrors.Update();
            if (plugin.settings.EnableNpcInterchangeTraffic) NpcServiceHud.Update();
            if (plugin.settings.EnableNpcInterchangeTraffic) NpcMaintenance.Update();
            TimetableHistory.Update();
            if (plugin.settings.EnableNpcInterchangeTraffic) NpcTrafficDiagnostics.UpdateFileSnapshot();
            ReportUpdateDuration(2, "NPC HUD", start);
        }
        catch (Exception exception)
        {
            LogException("Through traffic update failed", exception);
        }
    }

    private static int selectedTab;
    private static readonly string[] Tabs = { 
        "Caboose Mods", 
        "UI", 
        "Webhooks", 
        "Crew Update", 
        "Keybindings",
        //"NPC Traffic",
        //"Experimental",
    };
    private static readonly string[] CrewLoadMethods = { "Tracks", "Daily" };
    private static readonly string[] FuelColumns = { "None", "Engine", "Crew", "Status" };
    private static string crewUpdateMessage = string.Empty;
    private static bool includeCrewUpdateArea = true;

    internal static void NativeOnGUI()
    {
        if (!settingsWindowVisible || Instance?.settings == null) return;
        Color previousColor = GUI.color;
        Color previousBackgroundColor = GUI.backgroundColor;
        Color previousContentColor = GUI.contentColor;
        try
        {
            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, 1f);
            GUI.backgroundColor = new Color(previousBackgroundColor.r, previousBackgroundColor.g, previousBackgroundColor.b, 1f);
            GUI.contentColor = new Color(previousContentColor.r, previousContentColor.g, previousContentColor.b, 1f);
            settingsWindowRect = GUI.Window(45102, settingsWindowRect, DrawSettingsWindow, "RMROC451's Tweaks and Things");
        }
        finally
        {
            GUI.color = previousColor;
            GUI.backgroundColor = previousBackgroundColor;
            GUI.contentColor = previousContentColor;
        }
    }

    private static void DrawSettingsWindow(int windowId)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("F10 toggles this window. Settings are saved in Settings.json beside the mod.");
        if (GUILayout.Button("Save", GUILayout.Width(80))) SaveSettings();
        if (GUILayout.Button("Close", GUILayout.Width(80))) settingsWindowVisible = false;
        GUILayout.EndHorizontal();
        settingsScroll = GUILayout.BeginScrollView(settingsScroll);
        DrawSettingsContents();
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    private static void DrawSettingsContents()
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
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Experimental NPC traffic is disabled by default. When disabled, Railroader uses its native simulated interchange traffic.");
                bool npcTrafficEnabled = GUILayout.Toggle(settings.EnableNpcInterchangeTraffic, "Enable NPC interchange traffic, timetable traffic, and NPC clock controls");
                if (npcTrafficEnabled != settings.EnableNpcInterchangeTraffic)
                {
                    settings.EnableNpcInterchangeTraffic = npcTrafficEnabled;
                    SaveSettings();
                    if (!npcTrafficEnabled)
                    {
                        NpcTrafficWindow.Hide(); NpcServiceHud.Hide(); NpcSimulationSpeed.Reset();
                        InterchangeTimewarp_Patch.CancelForDisable();
                    }
                }
                if (settings.EnableNpcInterchangeTraffic && GUILayout.Button("Open AI traffic and interchanges")) NpcTrafficWindow.Show("settings");
                GUILayout.EndVertical();
                break;
            case 6:
                GUILayout.Space(8f);

                GUILayout.Label("Experimental Auto Engineer mode");
                GUILayout.Label("AE Local is an experimental switching mode. It is disabled by default.");
                bool aeLocalEnabled = GUILayout.Toggle(
                    settings.EnableExperimentalAeLocalMode,
                    "Enable AE Local mode in locomotive controls");
                if (aeLocalEnabled != settings.EnableExperimentalAeLocalMode)
                {
                    settings.EnableExperimentalAeLocalMode = aeLocalEnabled;
                    SaveSettings();
                }
                GUILayout.Label("This setting controls whether AE Local appears as a mode option. Reopen locomotive controls after changing it.");

                GUILayout.Space(8f);
                GUILayout.Label("EXTREMELY WIP — experimental second-mainline progressions");
                GUILayout.Label("This feature can create broken, colliding, or invisible track and unfinished CTC signaling. It is disabled by default.");
                settings.EnableExperimentalSecondMainlineProgressions = GUILayout.Toggle(
                    settings.EnableExperimentalSecondMainlineProgressions,
                    "Enable second-mainline progressions (EXTREMELY WIP)");
                GUILayout.Label("Changing this setting takes effect after you save settings and reload the railroad.");
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
        DrawBinding("Alt action modifier", settings.ClickAltBinding);
        DrawBinding("Control action modifier", settings.ClickControlBinding);
        DrawBinding("Shift action modifier", settings.ClickShiftBinding);
        GUILayout.Label("Defaults are Left Alt, Left Control, and Left Shift. The existing click combinations still work by holding multiple action modifiers together.");
    }

    private static void DrawBinding(string label, HotkeyBinding binding)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(180f));
        string button = ReferenceEquals(bindingCapture, binding) ? "Press a key..." : binding.keyCode.ToString();
        if (GUILayout.Button(button, GUILayout.Width(180f))) bindingCapture = binding;
        GUILayout.EndHorizontal();
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
        settings.ShowTimetableStringGraph = GUILayout.Toggle(settings.ShowTimetableStringGraph, "Show timetable string graph in the timetable window");
        settings.UseMenuContextStyle = GUILayout.Toggle(settings.UseMenuContextStyle, "Use menu-style car context menu (uncheck for cog style)");
        settings.HandBrakeAndAirTagModifiers = GUILayout.Toggle(settings.HandBrakeAndAirTagModifiers, "Enable tag updates for air, handbrake, oil, and hotbox status");
        bool enabled = GUI.enabled;
        if (ModSettingsAuthority.HostControlsLocked) GUILayout.Label("Host only — repair service settings are read-only.");
        GUI.enabled = enabled && !ModSettingsAuthority.HostControlsLocked;
        settings.AllowRepairsWithoutWaybill = GUILayout.Toggle(settings.AllowRepairsWithoutWaybill, "Allow repair-track service without a waybill (on by default)");
        settings.ServicingFundPenalty = GUILayout.Toggle(settings.ServicingFundPenalty, "Allow repair and interchange servicing with insufficient funds (20% overdraft fee)");
        GUI.enabled = enabled;
        settings.ShowWaypointSetNotifications = GUILayout.Toggle(settings.ShowWaypointSetNotifications, "Show WP SET notifications");
        settings.TrainBrakeDisplayShowsColorsInCalloutMode = GUILayout.Toggle(settings.TrainBrakeDisplayShowsColorsInCalloutMode, "Show destination colors in train brake display while tags are visible");
        settings.DisableWaypointControls = GUILayout.Toggle(settings.DisableWaypointControls, "Disable waypoint controls");
        settings.AutoFollowTrainCtcMarker = GUILayout.Toggle(
            settings.AutoFollowTrainCtcMarker,
            "Auto-follow train marker on CTC panel");
        settings.ShowCtcHudOverlayButton = GUILayout.Toggle(
            settings.ShowCtcHudOverlayButton,
            "Show CTC panel HUD overlay button");

        GUILayout.Space(6f);
        GUILayout.Label("Fuel display in engine roster");
        GUILayout.Label("Engine roster column widths (pixels; narrower panels shrink columns to fit)");
        GUILayout.Label("Locomotive: " + settings.EngineRosterNameWidth);
        settings.EngineRosterNameWidth = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.EngineRosterNameWidth, 60, 600));
        GUILayout.Label("Crew / mode: " + settings.EngineRosterCrewWidth);
        settings.EngineRosterCrewWidth = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.EngineRosterCrewWidth, 60, 600));
        GUILayout.Label("Status: " + settings.EngineRosterStatusWidth);
        settings.EngineRosterStatusWidth = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.EngineRosterStatusWidth, 60, 600));
        if (GUILayout.Button("Reset roster column widths"))
        { settings.EngineRosterNameWidth = 150; settings.EngineRosterCrewWidth = 100; settings.EngineRosterStatusWidth = 280; }
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

internal sealed class TweaksAndThingsUpdateHost : MonoBehaviour
{
    private void Update() => TweaksAndThingsPlugin.NativeUpdate(Time.deltaTime);
    private void LateUpdate()
    {
        TweaksAndThingsPlugin.NativeLateUpdate();
        TrainBrakeDisplayRefresh.Tick();
    }
    private void OnGUI() => TweaksAndThingsPlugin.NativeOnGUI();
}
