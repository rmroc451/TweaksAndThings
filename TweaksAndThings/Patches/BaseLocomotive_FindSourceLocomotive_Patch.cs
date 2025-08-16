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
