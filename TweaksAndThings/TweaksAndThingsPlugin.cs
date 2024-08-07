﻿// Ignore Spelling: RMROC

using GalaSoft.MvvmLight.Messaging;
using Game.State;
using HarmonyLib;
using Railloader;
using Serilog;
using System;
using System.Linq;
using System.Net.Http;
using UI.Builder;

using RMROC451.TweaksAndThings.Enums;
using RMROC451.TweaksAndThings.Commands;

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

        settings = moddingContext.LoadSettingsData<Settings>(self.Id);
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

        builder.AddTabbedPanels(settings._selectedTabState, delegate (UITabbedPanelBuilder tabBuilder)
        {
            tabBuilder.AddTab("Caboose Mods", "cabooseUpdates", CabooseMods);
            tabBuilder.AddTab("UI", "rosterUi", UiUpdates);
            tabBuilder.AddTab("Webhooks", "webhooks", WebhooksListUISection);
        });
    }

    private static string cabooseUse => "Caboose Use";
    private static string autoAiRequirment => "AutoAI\nRequirement";

    private void CabooseMods(UIPanelBuilder builder)
    {
        builder.AddField(
            cabooseUse,
            builder.AddToggle(
                () => settings?.EndGearHelpersRequirePayment ?? false,
                delegate (bool enabled)
                {
                    if (settings == null) settings = new();
                    settings.EndGearHelpersRequirePayment = enabled;
                    builder.Rebuild();
                }
            )
        ).Tooltip("Enable End Gear Helper Cost", @$"Will cost 1 minute of AI Brake Crew & Caboose Crew time per car in the consist when the new inspector buttons are utilized.

1.5x multiplier penalty to AI Brake Crew cost if no sufficiently crewed caboose nearby.

Caboose starts reloading `Crew Hours` at any Team or Repair track (no waybill), after being stationary for 30 seconds.

AutoOiler Update: Increases limit that crew will oiling a car from 75% -> 99%, also halves the time it takes (simulating crew from lead end and caboose handling half the train).

AutoOiler Update: if `{cabooseUse}` & `{autoAiRequirment.Replace("\n", " ")}` checked, then when a caboose is present, the AutoOiler will repair hotboxes afer oiling them to 100%.

AutoHotboxSpotter Update: decrease the random wait from 30 - 300 seconds to 15 - 30 seconds (Safety Is Everyone's Job)");

        builder.AddField(
            autoAiRequirment,
            builder.AddToggle(
                () => settings?.RequireConsistCabooseForOilerAndHotboxSpotter ?? false,
                delegate (bool enabled)
                {
                    if (settings == null) settings = new();
                    settings.RequireConsistCabooseForOilerAndHotboxSpotter = enabled;
                    builder.Rebuild();
                }
            )
        ).Tooltip("AI Engineer Requires Caboose", $@"A caboose is required in the consist to check for Hotboxes and perform Auto Oiler, if checked.");
    }

    private void UiUpdates(UIPanelBuilder builder)
    {
        builder.AddField(
            "Enable Tag Updates",
            builder.AddToggle(
                () => settings?.HandBrakeAndAirTagModifiers ?? false,
                delegate (bool enabled)
                {
                    if (settings == null) settings = new();
                    settings.HandBrakeAndAirTagModifiers = enabled;
                    builder.Rebuild();
                }
            )
        ).Tooltip("Enable Tag Updates", $@"Will suffix tag title with:
{TextSprites.CycleWaybills} if Air System issue.
{TextSprites.HandbrakeWheel} if there is a handbrake set.
{TextSprites.Hotbox} if a hotbox.");

        EngineRosterShowsFuelStatusUISection(builder);
    }

    private void EngineRosterShowsFuelStatusUISection(UIPanelBuilder builder)
    {
        var columns = Enum.GetValues(typeof(EngineRosterFuelDisplayColumn)).Cast<EngineRosterFuelDisplayColumn>().Select(i => i.ToString()).ToList();
        builder.AddSection("Fuel Display in Engine Roster", delegate (UIPanelBuilder builder)
        {
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

            builder.AddField(
                "Always Visible?",
                builder.AddToggle(
                    () => settings?.EngineRosterFuelColumnSettings?.EngineRosterShowsFuelStatusAlways ?? false,
                    delegate (bool enabled)
                    {
                        if (settings == null) settings = new();
                        settings.EngineRosterFuelColumnSettings.EngineRosterShowsFuelStatusAlways = enabled;
                        builder.Rebuild();
                    }
                )
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
