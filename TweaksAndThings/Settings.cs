﻿using Newtonsoft.Json;
using System.IO;
using System.Runtime;
using System;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace TweaksAndThings;

public class Settings
{

    public Settings() 
    {
        WebhookSettingsList = new[] { new WebhookSettings() }.ToList();
    }

    public Settings( 
        List<WebhookSettings> webhookSettingsList, 
        bool handBrakeAndAirTagModifiers
    )
    {
        this.WebhookSettingsList = webhookSettingsList;
        this.HandBrakeAndAirTagModifiers = handBrakeAndAirTagModifiers;
    }

    public List<WebhookSettings> WebhookSettingsList;
    public bool HandBrakeAndAirTagModifiers;

    internal void AddAnotherRow()
    {
        if (!string.IsNullOrEmpty(WebhookSettingsList.Last().WebhookUrl))
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
        this.WebhookEnabled = webhookEnabled;
        this.RailroadMark = railroadMark;
        this.WebhookUrl = webhookUrl;
    }

    public bool WebhookEnabled = false;
    public string RailroadMark = string.Empty;
    public string WebhookUrl = string.Empty;
}