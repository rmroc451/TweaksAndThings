using HarmonyLib;
using Model;
using Model.Definition;
using Model.Physics;
using Railloader;
using System;
using static Model.Car;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(BaseLocomotive))]
[HarmonyPatch(nameof(BaseLocomotive.FindSourceLocomotive), typeof(LogicalEnd))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class BaseLocomotive_FindSourceLocomotive_Patch
{
    private static bool Prefix(BaseLocomotive __instance, LogicalEnd searchDirection, ref BaseLocomotive __result)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled()) return true;

        __result = FindSourceLocomotive(__instance, searchDirection);

        return false;
    }
    public static BaseLocomotive FindSourceLocomotive(BaseLocomotive __instance, LogicalEnd searchDirection)
    {
        bool stop = false;
        int? num = __instance.set.IndexOfCar(__instance);
        if (!num.HasValue)
        {
            throw new Exception("Couldn't find car in set");
        }
        int carIndex = num.Value;
        LogicalEnd fromEnd = ((searchDirection == LogicalEnd.A) ? LogicalEnd.B : LogicalEnd.A);
        Car car;
        while (!stop && (car = __instance.set.NextCarConnected(ref carIndex, fromEnd, IntegrationSet.EnumerationCondition.AirAndCoupled, out stop)) != null)
        {
            if (!(car == __instance) && car is BaseLocomotive baseLocomotive &&  car.Archetype != CarArchetype.Tender && !baseLocomotive.locomotiveControl.air.IsCutOut)
            {
                return baseLocomotive;
            }
        }
        return null;
    }
}

//[HarmonyPatch(typeof(LocomotiveAirSystem))]
//[HarmonyPatch(nameof(LocomotiveAirSystem._ShouldDeferToLocomotiveAir))]
//[HarmonyPatchCategory("RMROC451TweaksAndThings")]
//internal class LocomotiveAirSystem__ShouldDeferToLocomotiveAir_Patch
//{
//    private static void Postfix(LocomotiveAirSystem __instance, ref LocomotiveAirSystem locomotiveAirSystem, ref bool __result)
//    {
//        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
//        if (!tweaksAndThings.IsEnabled()) return;

//        __result = _ShouldDeferToLocomotiveAir(__instance, out locomotiveAirSystem);
//    }
//    public static bool _ShouldDeferToLocomotiveAir(LocomotiveAirSystem __instance, out LocomotiveAirSystem locomotiveAirSystem)
//    {
//        locomotiveAirSystem = null;
//        if (__instance.car.set == null)
//        {
//            return false;
//        }
//        if (!(__instance.car.air is LocomotiveAirSystem locomotiveAirSystem2) || !(__instance.car is BaseLocomotive baseLocomotive))
//        {
//            return false;
//        }
//        if (!locomotiveAirSystem2.IsCutOut || locomotiveAirSystem2.IsMuEnabled)
//        {
//            return false;
//        }
//        BaseLocomotive baseLocomotive2 = baseLocomotive.FindMuSourceLocomotive();
//        if (baseLocomotive2 == null)
//        {
//            return false;
//        }
//        if (!(baseLocomotive2.air is LocomotiveAirSystem locomotiveAirSystem3))
//        {
//            return false;
//        }
//        locomotiveAirSystem = locomotiveAirSystem3;
//        return true;
//    }
//}

//[HarmonyPatch(typeof(CarAirSystem))]
//[HarmonyPatch(nameof(CarAirSystem.ShouldDeferToLocomotiveAir))]
//[HarmonyPatchCategory("RMROC451TweaksAndThings")]
//internal class CarAirSystem_ShouldDeferToLocomotiveAir_Patch
//{
//    private static void Postfix(CarAirSystem __instance, ref LocomotiveAirSystem locomotiveAirSystem, ref bool __result)
//    {
//        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
//        if (!tweaksAndThings.IsEnabled()) return;

//        __result = ShouldDeferToLocomotiveAir(__instance, out locomotiveAirSystem);
//    }

//    public static bool ShouldDeferToLocomotiveAir(CarAirSystem __instance, out LocomotiveAirSystem locomotiveAirSystem)
//    {
//        locomotiveAirSystem = null;
//        if (__instance.car.set == null)
//        {
//            return false;
//        }
//        if (__instance.car.Archetype != CarArchetype.Tender)
//        {
//            return false;
//        }
//        if (!__instance.car.TryGetAdjacentCar(__instance.car.EndToLogical(Car.End.F), out var adjacent) || !adjacent.IsLocomotive)
//        {
//            return false;
//        }
//        if (!(adjacent.air is LocomotiveAirSystem locomotiveAirSystem2))
//        {
//            return false;
//        }
//        locomotiveAirSystem = locomotiveAirSystem2;
//        if (locomotiveAirSystem.IsMuEnabled)
//        {
//            return true;
//        }
//        if (!locomotiveAirSystem.IsCutOut)
//        {
//            return true;
//        }
//        return locomotiveAirSystem.ShouldDeferToLocomotiveAir(out locomotiveAirSystem);
//    }
//}
