using Game;
using Game.Messages;
using Game.Notices;
using Game.State;
using HarmonyLib;
using Model;
using Model.AI;
using Model.Definition;
using Network;
using Network.Messages;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Track;
using UI.EngineControls;
using UI.EngineRoster;
using UnityEngine;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static UnityEngine.InputSystem.InputRemoting;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(AutoEngineerPlanner))]
[HarmonyPatch(nameof(AutoEngineerPlanner.HandleCommand))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoEngineerPlanner_HandleCommand_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<AutoEngineerPlanner_HandleCommand_Patch>();
    private static int governedSpeed = 20;

    static bool Prefix(AutoEngineerPlanner __instance, ref AutoEngineerCommand command, ref IPlayer sender)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        LocoNoticeWPSet(__instance, command, sender);
        if (!tweaksAndThings.IsEnabled() || !tweaksAndThings.SafetyFirst() || (sender.IsRemote && !tweaksAndThings.SafetyFirstClientEnforce()) || command.MaxSpeedMph <= governedSpeed) return true;
        BaseLocomotive loco = __instance._locomotive;

        if (SafetyFirstGoverningApplies(loco))
        {
            int orig = command.MaxSpeedMph;
            int limitedSpeed = Math.Min(command.MaxSpeedMph, governedSpeed);
            command.MaxSpeedMph = command.Mode switch
            {
                AutoEngineerMode.Road => limitedSpeed,
                AutoEngineerMode.Waypoint => limitedSpeed,
                _ => command.MaxSpeedMph,
            };

            string message = $"{Enum.GetName(typeof(AutoEngineerMode), command.Mode)}[{loco.DisplayName}] governed{{0}}due to Safety First rules.";
            if (orig != command.MaxSpeedMph)
            {
                message = string.Format(message, $" from {orig} to {command.MaxSpeedMph} MPH ");
            }
            else
            {
                message = string.Format(message, " ");
            }
            _log.Debug(message);
            Multiplayer.SendError(sender, message, AlertLevel.Info);
        }

        return true;
    }

    private static void LocoNoticeWPSet(AutoEngineerPlanner __instance, AutoEngineerCommand command, IPlayer sender)
    {
        OrderWaypoint? wp =
                    string.IsNullOrEmpty(command.WaypointLocationString) ?
                    null :
                    new OrderWaypoint?(new OrderWaypoint(command.WaypointLocationString, command.WaypointCoupleToCarId));

        if (
            wp.HasValue &&
            !string.IsNullOrEmpty(wp.Value.LocationString) &&
            !__instance._orders.Waypoint.Equals(wp)
        )
        {
            _log.Debug($"start setWP");
            Car selectedLoco = __instance._locomotive;
            _log.Debug($"{selectedLoco?.DisplayName ?? ""} set WP");
            if (LocationPositionFromString(wp.Value, out Vector3 gamePoint))
            {
                EntityReference entityReference = new EntityReference(EntityType.Position, new Vector4(gamePoint.x, gamePoint.y, gamePoint.z, 0));
                selectedLoco.PostNotice("ai-wpt-rmroc451", new Hyperlink(entityReference.URI(), $"WP SET [{sender.Name}]"));
            }
        }
    }

    internal static bool LocationPositionFromString(OrderWaypoint waypoint, out Vector3 position)
    {
        Location location;
        position = default;
        try
        {
            location = Graph.Shared.ResolveLocationString(waypoint.LocationString);
            position = location.GetPosition();
            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Couldn't get location from waypoint: {locStr}", waypoint.LocationString);
            return false;
        }
    }

    internal static bool SafetyFirstGoverningApplies(BaseLocomotive loco)
    {
        var _persistence = new AutoEngineerPersistence(loco.KeyValueObject);
        var OrdersHelper = new AutoEngineerOrdersHelper(loco, _persistence);

        if (loco.EnumerateCoupled().All(c => c.IsCaboose() || c.MotivePower())) return false;

        bool cabooseReq = SingletonPluginBase<TweaksAndThingsPlugin>.Shared.RequireConsistCabooseForOilerAndHotboxSpotter();
        string logMessage = $"\n{nameof(SafetyFirstGoverningApplies)}:{Enum.GetName(typeof(AutoEngineerMode), OrdersHelper.Mode)}[{loco.DisplayName}] ";
        Func<bool> firstClass = () =>
        {
            var output = TrainController.Shared.SelectedEngineExpress();
            logMessage += $"\nfirst class {output}";
            return output;
        };

        Func<bool> FreightConsist = () =>
        {
            bool output = !loco.EnumerateCoupled().ConsistNoFreight();
            logMessage += $"\nFreightConsist? {output}";
            logMessage += " " + string.Join(" / ", loco.EnumerateCoupled().Where(c => !c.MotivePower()).Select(c => $"{c.id} {Enum.GetName(typeof(CarArchetype), c.Archetype)}"));
            return output;
        };

        Func<bool> noCaboose = () =>
        {
            bool output = loco.FindMyCabooseSansLoadRequirement() == null;
            logMessage += $"\ncaboose? {!output}";
            return output;
        };

        logMessage += $"\nCaboose Required {cabooseReq}";

        bool output =
            cabooseReq &&
            !firstClass() &&
            FreightConsist() &&
            noCaboose();

        logMessage += $"\nGovern AE? {output}";
        if (_log.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            logMessage += $"\n{Environment.StackTrace}";

        _log.Debug(logMessage);

        return output;
    }
}

