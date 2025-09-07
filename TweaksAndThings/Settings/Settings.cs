using Serilog;
using System.Collections.Generic;
using System.Linq;
using RMROC451.TweaksAndThings.Enums;
using UI.Builder;
using Model;
using RMROC451.TweaksAndThings.Extensions;
using UnityEngine.InputSystem;

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
        bool endGearHelpersRequirePayment,
        bool requireConsistCabooseForOilerAndHotboxSpotter,
        bool cabooseAllowsConsistInfo,
        bool cabooseRequiredForLocoTagOilIndication,
        bool servicingFundPenalty,
        bool safetyFirst,
        bool safetyFirstClientEnforce,
        CrewHourLoadMethod loadCrewHoursMethod,
        float cabeeseSearchRadiusFtInMeters,
        bool trainBrakeDisplayShowsColorsInCalloutMode,
        bool shiftPaginationOnContextMenu
    )
    {
        WebhookSettingsList = webhookSettingsList;
        HandBrakeAndAirTagModifiers = handBrakeAndAirTagModifiers;
        EngineRosterFuelColumnSettings = engineRosterFuelColumnSettings;
        EndGearHelpersRequirePayment = endGearHelpersRequirePayment;
        RequireConsistCabooseForOilerAndHotboxSpotter = requireConsistCabooseForOilerAndHotboxSpotter;
        CabooseAllowsConsistInfo = cabooseAllowsConsistInfo;
        CabooseRequiredForLocoTagOilIndication = cabooseRequiredForLocoTagOilIndication;
        ServicingFundPenalty = servicingFundPenalty;
        SafetyFirst = safetyFirst;
        SafetyFirstClientEnforce = safetyFirstClientEnforce;
        LoadCrewHoursMethod = loadCrewHoursMethod;
        CabeeseSearchRadiusFtInMeters = cabeeseSearchRadiusFtInMeters;
        TrainBrakeDisplayShowsColorsInCalloutMode = trainBrakeDisplayShowsColorsInCalloutMode;
        ShiftPaginationOnContextMenu = shiftPaginationOnContextMenu;
    }

    public readonly UIState<string> _selectedTabState = new UIState<string>(null);
    public List<WebhookSettings>? WebhookSettingsList;
    public bool HandBrakeAndAirTagModifiers;
    public RosterFuelColumnSettings? EngineRosterFuelColumnSettings;
    public bool EndGearHelpersRequirePayment;
    public bool RequireConsistCabooseForOilerAndHotboxSpotter;
    public bool CabooseAllowsConsistInfo;
    public bool CabooseRequiredForLocoTagOilIndication;
    public bool ServicingFundPenalty;
    public bool SafetyFirst;
    public bool SafetyFirstClientEnforce;
    public CrewHourLoadMethod LoadCrewHoursMethod;
    public float CabeeseSearchRadiusFtInMeters;
    public bool TrainBrakeDisplayShowsColorsInCalloutMode;
    public bool ShiftPaginationOnContextMenu;

    internal void AddAnotherRow()
    {
        WebhookSettingsList ??= new[] { new WebhookSettings() }.ToList();
        if (!string.IsNullOrEmpty(WebhookSettingsList.OrderByDescending(wsl => wsl.WebhookUrl).Last().WebhookUrl))
        {
            WebhookSettingsList.Add(new());
            Log.Debug($"Adding another {nameof(WebhookSettings)} list entry, last one was filled in");
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

public static class SettingsExtensions
{
    public static List<WebhookSettings> SanitizeEmptySettings(this IEnumerable<WebhookSettings>? settings)
    {
        List<WebhookSettings> output =
            settings?.Where(s => !string.IsNullOrEmpty(s.WebhookUrl))?.ToList() ??
            new();

        output.Add(new());

        return output;
    }

    public static bool IsEnabled(this TweaksAndThingsPlugin input) =>
        input?.IsEnabled ?? false;
    public static bool CabooseAllowsConsistInfo(this TweaksAndThingsPlugin input) =>
        input?.settings?.CabooseAllowsConsistInfo ?? false;
    public static bool EndGearHelpersRequirePayment(this TweaksAndThingsPlugin input) =>
        input?.settings?.EndGearHelpersRequirePayment ?? false;
    public static bool RequireConsistCabooseForOilerAndHotboxSpotter(this TweaksAndThingsPlugin input) =>
        input?.settings?.RequireConsistCabooseForOilerAndHotboxSpotter ?? false;
    public static bool CabooseNonMotiveAllowedSetting(this TweaksAndThingsPlugin input, Car car) =>
        input.EndGearHelpersRequirePayment() && !car.MotivePower() && (bool)car.FindMyCabooseSansLoadRequirement();
    public static bool CabooseRequiredForLocoOilIndicator(this TweaksAndThingsPlugin input) =>
        input?.settings?.CabooseRequiredForLocoTagOilIndication ?? false;
    public static bool ServiceFundPenalties(this TweaksAndThingsPlugin input) =>
        input?.settings?.ServicingFundPenalty ?? false;
    public static bool SafetyFirst(this TweaksAndThingsPlugin input) =>
        input?.settings?.SafetyFirst ?? false;
    public static bool SafetyFirstClientEnforce(this TweaksAndThingsPlugin input) =>
        input?.settings?.SafetyFirstClientEnforce ?? true;
    public static bool DayLoadCrewHours(this TweaksAndThingsPlugin input) =>
        (input?.settings?.LoadCrewHoursMethod ?? CrewHourLoadMethod.Tracks) == CrewHourLoadMethod.Daily;
    public static bool TrainBrakeDisplayShowsColorsInCalloutMode(this TweaksAndThingsPlugin input) =>
        input?.settings?.TrainBrakeDisplayShowsColorsInCalloutMode ?? false;
    public static bool ShiftForPagination(this TweaksAndThingsPlugin input) => 
        input?.settings?.ShiftPaginationOnContextMenu ?? false;

}