using Game.Notices;
using Game.State;
using HarmonyLib;
using Model;
using Network;
using Serilog;
using UI.EngineControls;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Patches;


[HarmonyPatch(typeof(AutoEngineerOrdersHelper))]
[HarmonyPatch(nameof(AutoEngineerOrdersHelper.SetWaypoint), typeof(Track.Location), typeof(string))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoEngineerOrdersHelper_SetWaypoint_patch
{
    private static Serilog.ILogger _log => Log.ForContext<AutoEngineerOrdersHelper_SetWaypoint_patch>();
    static void Postfix(AutoEngineerOrdersHelper __instance, Track.Location location, string coupleToCarId)
    {
        if (StateManager.IsHost)
        {
            _log.Debug($"start setWP");
            Car selectedLoco = __instance._locomotive;
            _log.Debug($"{selectedLoco?.DisplayName ?? ""} set WP");
            Vector3 gamePoint = location.GetPosition();
            EntityReference entityReference = new EntityReference(EntityType.Position, new Vector4(gamePoint.x, gamePoint.y, gamePoint.z, 0));
            selectedLoco.PostNotice("ai-wpt-rmroc451", new Hyperlink(entityReference.URI(), $"WP SET"));
        }
    }
}
