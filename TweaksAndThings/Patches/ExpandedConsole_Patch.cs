using Game;
using Game.State;
using HarmonyLib;
using Helpers;
using Newtonsoft.Json;
using Railloader;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Track.Signals.Panel;
using UI.Console;

namespace TweaksAndThings.Patches;

[HarmonyPatch(typeof(ExpandedConsole))]
[HarmonyPatch(nameof(ExpandedConsole.Add))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
public class ExpandedConsole_Add_Patch
{
    private static void Prefix(ref UI.Console.Console.Entry entry)
    {
        entry.Text = $"{entry.Timestamp} : {entry.Text}";
        entry.Timestamp = RealNow();
        SendMs(ref entry);
    }

    static void SendMs(ref UI.Console.Console.Entry entry)
    {
        try
        {
            TweaksAndThings tweaksAndThings = SingletonPluginBase<TweaksAndThings>.Shared;
            StateManager shared = StateManager.Shared;
            GameStorage gameStorage = shared.Storage;
            WebhookSettings settings = tweaksAndThings.settings.WebhookSettingsList.FirstOrDefault(ws => ws.RailroadMark == gameStorage.RailroadMark);

            Log.Information(entry.Text);

            if (
                settings != null && 
                settings.WebhookEnabled && 
                !string.IsNullOrEmpty(settings.WebhookUrl) && 
                settings.RailroadMark == gameStorage.RailroadMark
            )
            {
                var t = new Regex("car:(.*)\"");

                var carId = t.IsMatch(entry.Text) ? Regex.Match(entry.Text, "car:(.*?)\"").Groups[1].Captures[0].ToString() : string.Empty;
                Model.Car? car = TrainController.Shared.CarForString(carId);
                Log.Information($"|{carId}| {car?.IsLocomotive}");
                bool engineInMessage = car?.IsLocomotive ?? false;
                var image = engineInMessage ?
                    new
                    {
                        url = string.Empty
                    } :
                    null;

                if (engineInMessage)
                {
                    CTCPanelMarkerManager cTCPanelMarkerManager = UnityEngine.Object.FindObjectOfType<CTCPanelMarkerManager>();

                    CTCPanelMarker marker = cTCPanelMarkerManager?._markers?.Values?.FirstOrDefault(v => v.TooltipInfo.Text.Contains(car.Ident.RoadNumber));

                    string color = CTCPanelMarker.InferColorFromText(car?.DisplayName).HexString().Replace("#", string.Empty);
                    if (marker != null)
                    {
                        color = CTCPanelMarker.InferColorFromText(marker.TooltipInfo.Text).HexString().Replace("#", string.Empty);
                    }
                    image = new
                    {
                        url = $"https://img.shields.io/badge/{car.DisplayName.Replace(" ", "%20")}-%20-{color}.png"
                    };

                }

                var SuccessWebHook = new
                {
                    username = $"[{gameStorage.RailroadMark}] {gameStorage.RailroadName}",
                    embeds = new List<object>
                    {
                        new
                        {
                            description= Regex.Replace(entry.Text, "<.*?>", "").Replace(": ", "\n"),
                            timestamp=DateTime.UtcNow,
                            image
                        },
                    }
                };
                string EndPoint = settings.WebhookUrl;
                Log.Information(JsonConvert.SerializeObject(SuccessWebHook));

                var content = new StringContent(JsonConvert.SerializeObject(SuccessWebHook), Encoding.UTF8, "application/json");

                tweaksAndThings.Client.PostAsync(EndPoint, content).Wait();

            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
        }
    }

    public static GameDateTime RealNow()
    {
        var now = DateTime.Now;
        return new GameDateTime(0, now.Hour + now.Minute / 60f);
    }
}
