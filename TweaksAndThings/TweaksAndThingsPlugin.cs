// Ignore Spelling: RMROC

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
        harmony.PatchCategory(modDefinition.Id.Replace(".",string.Empty));
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

        //WebhookUISection(ref builder);
        //builder.AddExpandingVerticalSpacer();
        WebhooksListUISection(ref builder);
        builder.AddExpandingVerticalSpacer();
        HandbrakesAndAnglecocksUISection(ref builder);
        builder.AddExpandingVerticalSpacer();
        EnginRosterShowsFuelStatusUISection(ref builder);
    }

    private void EnginRosterShowsFuelStatusUISection(ref UIPanelBuilder builder)
    {
        var columns = Enum.GetValues(typeof(EngineRosterFuelDisplayColumn)).Cast<EngineRosterFuelDisplayColumn>().Select(i => i.ToString()).ToList();
        builder.AddSection("Fuel Display in Engine Roster", delegate (UIPanelBuilder builder)
        {
            builder.AddField(
                "Enable",
                builder.AddDropdown(columns, (int)(settings?.EngineRosterFuelColumnSettings?.EngineRosterFuelStatusColumn ?? EngineRosterFuelDisplayColumn.None),
                    delegate (int column)
                    {
                        if (settings == null) settings = new() { WebhookSettingsList = new[] { new WebhookSettings() }.ToList(), EngineRosterFuelColumnSettings = new() };
                        settings.EngineRosterFuelColumnSettings.EngineRosterFuelStatusColumn = (EngineRosterFuelDisplayColumn)column;
                        builder.Rebuild();
                    }
                )
            ).Tooltip("Enable Fuel Display in Engine Roster", $"Will add reaming fuel indication to Engine Roster (with details in roster row tool tip), Examples : {string.Join(" ", Enumerable.Range(0,4).Select(i => TextSprites.PiePercent(i, 4)))}");

            builder.AddField(
                "Always Visible?",
                builder.AddToggle(
                    () => settings?.EngineRosterFuelColumnSettings?.EngineRosterShowsFuelStatusAlways ?? false,
                    delegate (bool enabled)
                    {
                        if (settings == null) settings = new() { WebhookSettingsList = new[] { new WebhookSettings() }.ToList(), EngineRosterFuelColumnSettings = new() };
                        settings.EngineRosterFuelColumnSettings.EngineRosterShowsFuelStatusAlways = enabled;
                        builder.Rebuild();
                    }
                )
            ).Tooltip("Fuel Display in Engine Roster Always Visible", $"Always displayed, if you want it hidden and only shown when you care to see, uncheck this, and then you can press ALT for it to populate on the next UI refresh cycle.");
        });
    }

    private void HandbrakesAndAnglecocksUISection(ref UIPanelBuilder builder)
    {
        builder.AddSection("Tag Callout Handbrake and Air System Helper", delegate (UIPanelBuilder builder)
        {
            builder.AddField(
                "Enable Tag Updates",
                builder.AddToggle(
                    () => settings?.HandBrakeAndAirTagModifiers ?? false,
                    delegate (bool enabled)
                    {
                        if (settings == null) settings = new() { WebhookSettingsList = new[] { new WebhookSettings() }.ToList() };
                        settings.HandBrakeAndAirTagModifiers = enabled;
                        builder.Rebuild();
                    }
                )
            ).Tooltip("Enable Tag Updates", $"Will add {TextSprites.CycleWaybills} to the car tag title having Air System issues. Also prepends {TextSprites.HandbrakeWheel} if there is a handbrake set.\n\nHolding Left Alt while tags are displayed only shows tag titles that have issues.");
        });
    }

    private void WebhooksListUISection(ref UIPanelBuilder builder)
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
                                if (settings == null) settings = new() { WebhookSettingsList = new[] { new WebhookSettings() }.ToList() };
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
                                    if (settings == null) settings = new() { WebhookSettingsList = new[] { new WebhookSettings() }.ToList() };
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
                                    if (settings == null) settings = new() { WebhookSettingsList = new[] { new WebhookSettings() }.ToList() };
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
