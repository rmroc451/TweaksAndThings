using Serilog;
using System.Collections.Generic;
using System.Linq;
using RMROC451.TweaksAndThings.Enums;

namespace RMROC451.TweaksAndThings;

public class Settings
{

    public Settings()
    {
        WebhookSettingsList = new[] { new WebhookSettings() }.ToList();
        EngineRosterFuelColumnSettings = new();
    }

    public Settings(
        List<WebhookSettings> webhookSettingsList,
        bool handBrakeAndAirTagModifiers,
        RosterFuelColumnSettings engineRosterFuelColumnSettings,
        bool endGearHelpersRequirePayment
    )
    {
        WebhookSettingsList = webhookSettingsList;
        HandBrakeAndAirTagModifiers = handBrakeAndAirTagModifiers;
        EngineRosterFuelColumnSettings = engineRosterFuelColumnSettings;
        EndGearHelpersRequirePayment = endGearHelpersRequirePayment;
    }

    public List<WebhookSettings>? WebhookSettingsList;
    public bool HandBrakeAndAirTagModifiers;
    public RosterFuelColumnSettings? EngineRosterFuelColumnSettings;
    public bool EndGearHelpersRequirePayment;

    internal void AddAnotherRow()
    {
        WebhookSettingsList = !WebhookSettingsList?.Any() ?? false ? new[] { new WebhookSettings() }.ToList() : new List<WebhookSettings>();
        if (!string.IsNullOrEmpty(WebhookSettingsList.OrderByDescending(wsl => wsl.WebhookUrl).Last().WebhookUrl))
        {
            WebhookSettingsList.Add(new());
            Log.Information($"Adding another {nameof(WebhookSettings)} list entry, last one was filled in");
        }
    }
}

public class WebhookSettings
{
    public WebhookSettings() { }
    public WebhookSettings(
        bool webhookEnabled,
        string railroadMark,
        string webhookUrl
    )
    {
        WebhookEnabled = webhookEnabled;
        RailroadMark = railroadMark;
        WebhookUrl = webhookUrl;
    }

    public bool WebhookEnabled = false;
    public string RailroadMark = string.Empty;
    public string WebhookUrl = string.Empty;
}

public class RosterFuelColumnSettings
{
    public RosterFuelColumnSettings() { }
    public RosterFuelColumnSettings(
        bool engineRosterShowsFuelStatusAlways,
        EngineRosterFuelDisplayColumn engineRosterFuelStatusColumn
    )
    {
        this.EngineRosterShowsFuelStatusAlways = engineRosterShowsFuelStatusAlways;
        this.EngineRosterFuelStatusColumn = engineRosterFuelStatusColumn;
    }

    public bool EngineRosterShowsFuelStatusAlways;
    public EngineRosterFuelDisplayColumn EngineRosterFuelStatusColumn;
}