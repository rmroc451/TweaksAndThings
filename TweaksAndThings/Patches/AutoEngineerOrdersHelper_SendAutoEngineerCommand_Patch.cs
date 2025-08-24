using Game.Messages;
using HarmonyLib;
using Model.AI;
using Model.Definition;
using Railloader;
using RMROC451.TweaksAndThings.Extensions;
using Serilog;
using System;
using System.Linq;
using UI.EngineControls;

namespace RMROC451.TweaksAndThings.Patches;


[HarmonyPatch(typeof(AutoEngineerOrdersHelper))]
[HarmonyPatch(nameof(AutoEngineerOrdersHelper.SendAutoEngineerCommand), typeof(AutoEngineerMode), typeof(bool), typeof(int), typeof(float), typeof(OrderWaypoint))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoEngineerOrdersHelper_SendAutoEngineerCommand_Patch
{

    private static Serilog.ILogger _log => Log.ForContext<AutoEngineerOrdersHelper_SendAutoEngineerCommand_Patch>();

    static bool Prefix(AutoEngineerMode mode, ref int maxSpeedMph)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled() || !tweaksAndThings.SafetyFirst()) return true;

        if (SafetyFirstGoverningApplies())
        {
            int orig = maxSpeedMph;
            int limitedSpeed = Math.Min(maxSpeedMph, 20);
            maxSpeedMph = mode switch
            {
                AutoEngineerMode.Road => limitedSpeed,
                AutoEngineerMode.Waypoint => limitedSpeed,
                _ => maxSpeedMph,
            };

            if (orig != maxSpeedMph)
            {
                _log.Debug($"{Enum.GetName(typeof(AutoEngineerMode), mode)}[{TrainController.Shared.SelectedLocomotive.DisplayName}] {nameof(AutoEngineerOrdersExtensions.MaxSpeedMph)} limited to {limitedSpeed} from {orig}; No Caboose in Consist;");
            }
        }

        return true;

    }

    internal static bool SafetyFirstGoverningApplies()
    {
        var _persistence = new AutoEngineerPersistence(TrainController.Shared.SelectedLocomotive.KeyValueObject);
        var OrdersHelper = new AutoEngineerOrdersHelper(TrainController.Shared.SelectedLocomotive, _persistence);

        if (TrainController.Shared.SelectedLocomotive.EnumerateCoupled().All(c => c.IsCaboose() || c.MotivePower())) return false;

        bool cabooseReq = SingletonPluginBase<TweaksAndThingsPlugin>.Shared.RequireConsistCabooseForOilerAndHotboxSpotter();
        string logMessage = $"\n{nameof(SafetyFirstGoverningApplies)}:{Enum.GetName(typeof(AutoEngineerMode), OrdersHelper.Mode)}[{TrainController.Shared.SelectedLocomotive.DisplayName}] ";
        Func<bool> firstClass = () =>
        {
            var output = TrainController.Shared.SelectedEngineExpress(); 
            logMessage += $"\nfirst class {output}";
            return output;
        };

        Func<bool> FreightConsist = () =>
        {
            bool output = !TrainController.Shared.SelectedLocomotive.EnumerateCoupled().ConsistNoFreight();
            logMessage += $"\nFreightConsist? {output}";
            logMessage += " " + string.Join(" / ", TrainController.Shared.SelectedLocomotive.EnumerateCoupled().Where(c => !c.MotivePower()).Select(c => $"{c.id} {Enum.GetName(typeof(CarArchetype), c.Archetype)}"));
            return output;
        };

        Func<bool> noCaboose = () =>
        {
            var output = TrainController.Shared.SelectedLocomotive.FindMyCaboose(0.0f, false) == null;
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

        _log.Debug(logMessage);

        return output;
    }
}
