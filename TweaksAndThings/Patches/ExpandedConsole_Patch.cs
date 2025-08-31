using Game;
using Game.State;
using HarmonyLib;
using Model;
using Model.Ops.Timetable;
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

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(ExpandedConsole))]
[HarmonyPatch(nameof(ExpandedConsole.Add))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class ExpandedConsole_Add_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<ExpandedConsole_Add_Patch>();
    private static void Prefix(ref UI.Console.Console.Entry entry)
    {
        entry.Text = $"{entry.Timestamp} : {entry.Text}";
        entry.Timestamp = RealNow();
        SendMs((UI.Console.Console.Entry?)entry);
    }


    private static string hold => @"```ansi
[2;40m[0m[2;40m[2;34m■[0m[2;40m[0m[2;34m[2;40m[0m[2;34m[0m[2;40m[1;2m[1;34m{loco}[0m[1;40m[0m[2;40m[0m[2;40m[1;2m[1;34m[0m[1;40m[0m[2;40m[0m[2;40m[2;34m■[0m[2;40m[0m[2;34m[2;40m[0m[2;34m[0m {msg}
```";

    private static string west => @"```ansi
[2;40m[0m[2;31m[2;40m◀{loco}[0m[2;31m[0m[2;40m[2;31m [0m[2;40m[0m[2;40m[0m {msg}
```";

    private static string east => @"```ansi
[2;40m[0m[2;32m[0m[2;40m [0m[2;36m[0m[2;40m[0m[2;36m[2;40m{loco}▶[0m[2;36m[0m {msg}
```";

    internal static void SendMs(UI.Console.Console.Entry? entry, string? text = null)
    {
        try
        {
            if (entry is null && !String.IsNullOrEmpty(text)) entry = new() { Text = text };
            var msgText = entry?.Text ?? string.Empty;
            if (msgText.StartsWith("Usage:")) return;
            TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
            StateManager shared = StateManager.Shared;
            GameStorage gameStorage = shared.Storage;
            WebhookSettings settings = tweaksAndThings?.settings?.WebhookSettingsList?.FirstOrDefault(ws => ws.RailroadMark == gameStorage.RailroadMark);

            if (
                settings != null && 
                settings.WebhookEnabled && 
                !string.IsNullOrEmpty(settings.WebhookUrl) && 
                settings.RailroadMark == gameStorage.RailroadMark
            )
            {
                var t = new Regex("car:(.*)\"");

                var carId = t.IsMatch(msgText) ? Regex.Match(msgText, "car:(.*?)\"").Groups[1].Captures[0].ToString() : string.Empty;
                Model.Car? car = TrainController.Shared.CarForString(carId);
                var data = UpdateCarText(car);
                bool engineInMessage = car?.IsLocomotive ?? false;
                string msgToSend = string.Empty;
                string desc = Regex.Replace(msgText, "<.*?>", "");
                desc = !!car ? desc.Replace(car?.DisplayName ?? string.Empty, string.Empty) : desc;
                desc = desc.Trim();//.Replace(": ", "\n");

                if (!!car && engineInMessage)
                {
                    CTCPanelMarkerManager cTCPanelMarkerManager = UnityEngine.Object.FindObjectOfType<CTCPanelMarkerManager>();

                    CTCPanelMarker marker = cTCPanelMarkerManager?._markers?.Values?.FirstOrDefault(v => v.TooltipInfo.Text.Contains(car.Ident.RoadNumber));
                    
                    string markerText = marker?.TooltipInfo.Text ?? string.Empty;
                    if (markerText.StartsWith(">") || markerText.EndsWith(">") || data.Item2 == Timetable.Direction.East || msgText.Contains("*-"))
                    {
                        msgToSend = east.Replace("{loco}", car?.DisplayName.Replace(" ", string.Empty)).Replace("{msg}", desc);
                    }
                    else if (markerText.StartsWith("<") || markerText.EndsWith("<") || data.Item2 == Timetable.Direction.West || msgText.Contains("-*"))
                    {
                        msgToSend = west.Replace("{loco}", car?.DisplayName.Replace(" ", string.Empty)).Replace("{msg}", desc);
                    } 
                    else
                    {
                        msgToSend = hold.Replace("{loco}", car?.DisplayName.Replace(" ", string.Empty)).Replace("{msg}", desc);
                    }
                }
                else
                {
                    msgToSend = desc;
                }
                msgToSend = msgToSend
                    .Replace(" , ", ", ")
                    .Replace(", :", " :")
                    .Replace(" ; ", "; ")
                    .Replace("*-", string.Empty)
                    .Replace("#", string.Empty)
                    .Replace("-*", string.Empty)
                    .Trim();

                var SuccessWebHook = new
                {
                    username = $"[{gameStorage.RailroadMark}] {gameStorage.RailroadName}",
                    embeds = new List<object>
                    {
                        new
                        {
                            description= msgToSend
                        },
                    }
                };
                string EndPoint = settings.WebhookUrl;

                var content = new StringContent(JsonConvert.SerializeObject(SuccessWebHook), Encoding.UTF8, "application/json");

                tweaksAndThings.Client.PostAsync(EndPoint, content).Wait();

            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
        }
    }

    public static (string , Timetable.Direction?) UpdateCarText(Car car)
    {
        string output = string.Empty;
        Timetable.Direction? dir = null;
        if (car?.IsLocomotive ?? false)
        {
            if (StateManager.Shared.PlayersManager.TrainCrewForId(car.trainCrewId, out var trainCrew))
            {
                output = trainCrew.Name;
                if (TimetableController.Shared.TryGetTrainForTrainCrew(trainCrew, out Timetable.Train timetableTrain))
                {
                    dir = timetableTrain.Direction;
                    output += " (Train " + timetableTrain.DisplayStringShort + ")";
                }
            }
        }
        return (output, dir);
    }


    public static GameDateTime RealNow()
    {
        var now = DateTime.Now;
        return new GameDateTime(0, now.Hour + now.Minute / 60f);
    }
}
