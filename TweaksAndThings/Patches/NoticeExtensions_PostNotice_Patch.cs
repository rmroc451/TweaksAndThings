using Game.Notices;
using Game.State;
using HarmonyLib;
using Model;
using Serilog;
using System;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(NoticeExtensions))]
[HarmonyPatch(nameof(NoticeExtensions.PostNotice), typeof(Car), typeof(string), typeof(string))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class NoticeExtensions_PostNotice_Patch
{
    private static ILogger _log => Log.ForContext<NoticeExtensions_PostNotice_Patch>();
    static void Postfix(Car car, string key, string content)
    {
        if (!StateManager.IsHost) return;
        try
        {

            //Log.Debug($"{car.DisplayName} patch PostNotice");
            if (!string.IsNullOrEmpty(content) &&
                key.Equals("ai-wpt") && 
                content.ToLower().Contains("Arrived at Waypoint".ToLower())
            )
            {
                car.PostNotice("ai-wpt-rmroc451", null);
            }
        } catch (Exception ex)
        {
            _log.ForContext("car", car).Error(ex, "woops");
        }
    }

}
