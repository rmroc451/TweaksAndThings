// Ignore Spelling: RMROC

using GalaSoft.MvvmLight.Messaging;
using Game.Messages;
using Game.State;
using HarmonyLib;
using Railloader;
using RMROC451.TweaksAndThings.Commands;
using RMROC451.TweaksAndThings.Enums;
using Serilog;
using System;
using System.Linq;
using System.Net.Http;
using UI.Builder;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace RMROC451.TweaksAndThings;

public class TweaksAndThingsPlugin : SingletonPluginBase<TweaksAndThingsPlugin>, IUpdateHandler, IModTabHandler
{
    private HttpClient client;
    internal HttpClient Client
    {
        get
        {
            if (client == null)
                client = new HttpClient();

            return client;
        }
    }
    internal Settings? settings { get; private set; } = null;
    readonly ILogger logger = Log.ForContext<TweaksAndThingsPlugin>();
    IModdingContext moddingContext { get; set; }
    IModDefinition modDefinition { get; set; }

    public string ModDirectory => modDefinition.Directory;

    static TweaksAndThingsPlugin()
    {
        Log.Information("Hello! Static Constructor was called!");
    }

    public TweaksAndThingsPlugin(IModdingContext moddingContext, IModDefinition self)
    {
        this.modDefinition = self;

        this.moddingContext = moddingContext;

        logger.Information("Hello! Constructor was called for {modId}/{modVersion}!", self.Id, self.Version);

        moddingContext.RegisterConsoleCommand(new EchoCommand());

        settings = moddingContext.LoadSettingsData<Settings>(self.Id) ?? new();
    }

    public override void OnEnable()
    {
        logger.Information("OnEnable() was called!");
        var harmony = new Harmony(modDefinition.Id);
        harmony.PatchCategory(modDefinition.Id.Replace(".", string.Empty));
    }

    public override void OnDisable()
    {
        var harmony = new Harmony(modDefinition.Id);
        harmony.UnpatchAll(modDefinition.Id);
        Messenger.Default.Unregister(this);
    }

    public void Update()
    {
        logger.Verbose("UPDATE()");
    }

    public void ModTabDidOpen(UIPanelBuilder builder)
    {
        logger.Information("Daytime!");

        if (settings == null) settings = new();
        if (!settings?.WebhookSettingsList?.Any() ?? true) settings.WebhookSettingsList = new[] { new WebhookSettings() }.ToList();
        if (settings?.EngineRosterFuelColumnSettings == null) settings.EngineRosterFuelColumnSettings = new();


        settings.WebhookSettingsList =
            settings?.WebhookSettingsList.SanitizeEmptySettings();

        builder.AddSection("Adjustments To Base Game", (UIPanelBuilder builder) => {
            builder.AddLabel("Repair tracks now require cars to be waybilled, or they will not be serviced/overhauled.\nThey will report on the company window's location section as 'No Work Order Assigned'.");
        });
        builder.Spacer(spacing * spacing);
        builder.AddTabbedPanels(settings._selectedTabState, delegate (UITabbedPanelBuilder tabBuilder)
        {
            tabBuilder.AddTab("Caboose Mods", "cabooseUpdates", CabooseMods);
            tabBuilder.AddTab("UI", "rosterUi", UiUpdates);
            tabBuilder.AddTab("Webhooks", "webhooks", WebhooksListUISection);
        });
    }

    private static string cabooseUse => "Caboose Use";
    private static string autoAiRequirment => "AutoAI Requirement";
    private static string locoConsistOilIndication => "Consist Oil Indication";
    private static float spacing => 2f;

    private void CabooseMods(UIPanelBuilder builder)
    {
        //UI.GameInput
        //builder.AddField("Meow", 
        //    builder.AddInputBindingControl(

        //        )
        //)

        //        InputAction a = new InputAction("connectCarsAndGladhands", InputActionType.Button)

        //var connectCarsAndGladhands = new InputAction("connectCarsAndGladhands");
        //connectCarsAndGladhands.AddCompositeBinding("connectCarsAndGladhandsComposite")
        //    .With("modifier", "<Keyboard>/leftCtrl")
        //    .With("modifier", "<Keyboard>/leftAlt");

        //builder.AddField("meow", builder.AddInputBindingControl(connectCarsAndGladhands, conflict: true, ()=>))
        #region EndGearHelperCost
        builder.AddFieldToggle(
            cabooseUse,
            () => this.EndGearHelpersRequirePayment(),
                delegate (bool enabled)
                {
                    if (settings == null) settings = new();
                    settings.EndGearHelpersRequirePayment = enabled;
                    builder.Rebuild();
                }
        ).Tooltip("Enable End Gear Helper Cost", @$"Will cost 1 minute of AI Brake Crew & Caboose Crew time per car in the consist when the new inspector buttons are utilized.

1.5x multiplier penalty to AI Brake Crew cost if no sufficiently crewed caboose nearby.

Caboose starts reloading `Crew Hours` at any Team or Repair track (no waybill), after being stationary for 30 seconds.

AutoOiler Update: Increases limit that crew will oiling a car from 75% -> 99%, also halves the time it takes (simulating crew from lead end and caboose handling half the train).

AutoOiler Update: if `{cabooseUse}` & `{autoAiRequirment.Replace("\n", " ")}` checked, then when a caboose is present, the AutoOiler will repair hotboxes afer oiling them to 100%.

AutoHotboxSpotter Update: decrease the random wait from 30 - 300 seconds to 15 - 30 seconds (Safety Is Everyone's Job)");
        #endregion

        #region CabeeseLoadOptions
        if (this.EndGearHelpersRequirePayment())
        {
            var columns = Enum.GetValues(typeof(CrewHourLoadMethod)).Cast<CrewHourLoadMethod>().Select(i => i.ToString()).ToList();
            builder.Spacer(spacing);
            builder.AddField("Refill Option",
                builder.AddDropdown(
                    columns,
                    (int)(settings?.LoadCrewHoursMethod ?? CrewHourLoadMethod.Tracks),
                    delegate (int column)
                    {
                        if (settings == null) settings = new();
                        settings.LoadCrewHoursMethod = (CrewHourLoadMethod)column;
                        builder.Rebuild();
                    }
                )
            ).Tooltip("Crew Hours Load Option", "Select whether you want to manually reload cabeese via:\n\ntrack method - (team/repair/passenger stop/interchange)\n\ndaily caboose top off - refill to 8h at new day.");
        }
        #endregion

        #region RequireCabeeseForOiler/HotboxDetection
        builder.Spacer(spacing);
        builder.AddFieldToggle(
            autoAiRequirment,
            () => this.RequireConsistCabooseForOilerAndHotboxSpotter(),
                delegate (bool enabled)
                {
                    if (settings == null) settings = new();
                    settings.RequireConsistCabooseForOilerAndHotboxSpotter = enabled;
                    builder.Rebuild();
                }
        ).Tooltip("AI Hotbox\\Oiler Requires Caboose", $@"A caboose is required in the consist to check for Hotboxes and perform Auto Oiler, if checked.");
        #endregion

        #region ShowLocomotiveConsistOilIndicator
        builder.Spacer(spacing);
        builder.AddFieldToggle(
            locoConsistOilIndication,
            () => settings?.CabooseRequiredForLocoTagOilIndication ?? false,
            delegate (bool enabled)
            {
                if (settings == null) settings = new();
                settings.CabooseRequiredForLocoTagOilIndication = enabled;
                builder.Rebuild();
            }
        ).Tooltip(locoConsistOilIndication, $@"A caboose is required in the consist to report the lowest oil level in the consist in the locomotive's tag & roster entry.");
        #endregion

        #region SafetyFirst
        builder.Spacer(spacing);
        builder.AddFieldToggle(
            "Safety First!",
            () => settings?.SafetyFirst ?? false,
            delegate (bool enabled)
            {
                if (settings == null) settings = new();
                settings.SafetyFirst = enabled;
                builder.Rebuild();
            }
        ).Tooltip("Safety First", $@"On non-express timetabled consists, a caboose is required in the consist increase AE max speed > 20 in {Enum.GetName(typeof(AutoEngineerMode), AutoEngineerMode.Road)}/{Enum.GetName(typeof(AutoEngineerMode), AutoEngineerMode.Waypoint)} mode.");
        #endregion

        #region CabeeseSearchRadius
        builder.Spacer(spacing);
        builder.AddField(
            "Cabeese Search Radius",
            builder.AddSlider(
                () => this.CabeeseSearchRadiusInMeters(),
                () => $"{string.Format(Mathf.CeilToInt(this.CabeeseSearchRadiusInMeters() * 3.28084f).ToString(), "N0")}ft",
                delegate (float input) { 
                    settings = settings ?? new();
                    settings.CabeeseSearchRadiusFtInMeters = Mathf.CeilToInt(input);
                    builder.Rebuild();
                },
                minValue: 1f,
                maxValue: Mathf.CeilToInt(5280f / 2f / 3.28084f),
                wholeNumbers: true
            )
        ).Tooltip("Cabeese Catchment Area", "How far should the cabeese hunting logic look away from the cars in the area to find a caboose?");
        #endregion
    }

    private void UiUpdates(UIPanelBuilder builder)
    {
        builder.AddFieldToggle(
            "Enable Tag Updates",
                () => settings?.HandBrakeAndAirTagModifiers ?? false,
                delegate (bool enabled)
                {
                    if (settings == null) settings = new();
                    settings.HandBrakeAndAirTagModifiers = enabled;
                    builder.Rebuild();
                }
        ).Tooltip("Enable Tag Updates", $@"Will suffix tag title with:
{TextSprites.CycleWaybills} if Air System issue.
{TextSprites.HandbrakeWheel} if there is a handbrake set.
{TextSprites.Hotbox} if a hotbox.");

        builder.Spacer(spacing);
        builder.AddFieldToggle(
            "Debt Allowance",
            () => settings?.ServicingFundPenalty ?? false,
            delegate (bool enabled)
            {
                if (settings == null) settings = new();
                settings.ServicingFundPenalty = enabled;
                builder.Rebuild();
            }
        ).Tooltip("Allow Insufficient Funds", $@"Will allow interchange service and repair shops to still function when you are insolvent, at a 20% overdraft fee.");

        builder.Spacer(spacing);
        EngineRosterShowsFuelStatusUISection(builder);
    }

    private void EngineRosterShowsFuelStatusUISection(UIPanelBuilder builder)
    {
        var columns = Enum.GetValues(typeof(EngineRosterFuelDisplayColumn)).Cast<EngineRosterFuelDisplayColumn>().Select(i => i.ToString()).ToList();
        builder.AddSection("Fuel Display in Engine Roster", delegate (UIPanelBuilder builder)
        {
            builder.Spacer(spacing);
            builder.AddField(
                "Enable",
                builder.AddDropdown(columns, (int)(settings?.EngineRosterFuelColumnSettings?.EngineRosterFuelStatusColumn ?? EngineRosterFuelDisplayColumn.None),
                    delegate (int column)
                    {
                        if (settings == null) settings = new();
                        settings.EngineRosterFuelColumnSettings.EngineRosterFuelStatusColumn = (EngineRosterFuelDisplayColumn)column;
                        builder.Rebuild();
                    }
                )
            ).Tooltip("Enable Fuel Display in Engine Roster", $"Will add reaming fuel indication to Engine Roster (with details in roster row tool tip), Examples : {string.Join(" ", Enumerable.Range(0, 4).Select(i => TextSprites.PiePercent(i, 4)))}");

            builder.Spacer(spacing);
            builder.AddFieldToggle(
                "Always Visible?",
                    () => settings?.EngineRosterFuelColumnSettings?.EngineRosterShowsFuelStatusAlways ?? false,
                    delegate (bool enabled)
                    {
                        if (settings == null) settings = new();
                        settings.EngineRosterFuelColumnSettings.EngineRosterShowsFuelStatusAlways = enabled;
                        builder.Rebuild();
                    }
            ).Tooltip("Fuel Display in Engine Roster Always Visible", $"Always displayed, if you want it hidden and only shown when you care to see, uncheck this, and then you can press ALT for it to populate on the next UI refresh cycle.");
        });
    }

    private void WebhooksListUISection(UIPanelBuilder builder)
    {
        builder.AddSection("Webhooks List", delegate (UIPanelBuilder builder)
        {
            for (int i = 1; i <= settings.WebhookSettingsList.Count; i++)
            {
                int z = i - 1;
                builder.AddSection($"Webhook {i}", delegate (UIPanelBuilder builder)
                {
                    builder.AddField(
                        "Webhook Enabled",
                        builder.AddToggle(
                            () => settings?.WebhookSettingsList[z]?.WebhookEnabled ?? false,
                            delegate (bool enabled)
                            {
                                if (settings == null) settings = new();
                                settings.WebhookSettingsList[z].WebhookEnabled = enabled;
                                settings.AddAnotherRow();
                                builder.Rebuild();
                            }
                        )
                    ).Tooltip("Webhook Enabled", "Will parse the console messages and transmit to a Discord webhook.");

                    builder.AddField(
                        "Reporting Mark",
                        builder.HStack(delegate (UIPanelBuilder field)
                        {
                            field.AddInputField(
                                settings?.WebhookSettingsList[z]?.RailroadMark,
                                delegate (string railroadMark)
                                {
                                    if (settings == null) settings = new();
                                    settings.WebhookSettingsList[z].RailroadMark = railroadMark;
                                    settings.AddAnotherRow();
                                    builder.Rebuild();
                                }, characterLimit: GameStorage.ReportingMarkMaxLength).FlexibleWidth();
                        })
                    ).Tooltip("Reporting Mark", "Reporting mark of the company this Discord webhook applies to..");

                    builder.AddField(
                        "Webhook Url",
                        builder.HStack(delegate (UIPanelBuilder field)
                        {
                            field.AddInputField(
                                settings?.WebhookSettingsList[z]?.WebhookUrl,
                                delegate (string webhookUrl)
                                {
                                    if (settings == null) settings = new();
                                    settings.WebhookSettingsList[z].WebhookUrl = webhookUrl;
                                    settings.AddAnotherRow();
                                    builder.Rebuild();
                                }).FlexibleWidth();
                        })
                    ).Tooltip("Webhook Url", "Url of Discord webhook to publish messages to.");
                });
            }
        });
    }

    public void ModTabDidClose()
    {
        logger.Information("Nighttime...");
        this.moddingContext.SaveSettingsData(this.modDefinition.Id, settings ?? new());
    }
}
