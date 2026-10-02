using HarmonyLib;
using Model;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RMROC451.TweaksAndThings.Patches;

/// <summary>
/// The previous package added this slot to each caboose definition before
/// Railroader instantiated cars. UMM has no content-mixin manifest, so add
/// the equivalent slot to the shared game definition during car creation.
/// </summary>
[HarmonyPatch(typeof(Car))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class CabooseCrewLoadSlot_Patch
{
    private const string CrewLoadId = "crew-hours";
    private const string SlotJson = "[{\"maximumCapacity\":8,\"loadUnits\":\"Quantity\",\"requiredLoadIdentifier\":\"crew-hours\"}]";
    private static bool failureLogged;

    private static IEnumerable<MethodBase> TargetMethods() =>
        typeof(Car).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static void Prefix(object[] __args)
    {
        try
        {
            foreach (var argument in __args)
            {
                if (argument == null) continue;
                EnsureCrewLoadSlot(argument);
            }
        }
        catch (Exception exception) { LogFailure(exception); }
    }

    private static void Postfix(Car __instance)
    {
        try
        {
            if (__instance != null) EnsureCrewLoadSlot(__instance);
        }
        catch (Exception exception) { LogFailure(exception); }
    }

    internal static void EnsureCrewLoadSlot(object candidate)
    {
        object? target = candidate;
        var directSlots = ReadMember(target, "LoadSlots") as IList;
        if (directSlots == null)
        {
            target = ReadMember(candidate, "Definition");
            if (target == null) return;
        }

        var archetype = ReadMember(target, "Archetype");
        if (archetype == null || !string.Equals(archetype.ToString(), "Caboose", StringComparison.OrdinalIgnoreCase)) return;

        var slots = directSlots ?? ReadMember(target, "LoadSlots") as IList;
        if (slots == null)
        {
            var definition = ReadMember(candidate, "Definition");
            if (definition == null) return;
            archetype = ReadMember(definition, "Archetype");
            if (archetype == null || !string.Equals(archetype.ToString(), "Caboose", StringComparison.OrdinalIgnoreCase)) return;
            slots = ReadMember(definition, "LoadSlots") as IList;
        }

        if (slots == null || slots.Cast<object>().Any(slot =>
                string.Equals(ReadMember(slot, "requiredLoadIdentifier")?.ToString(), CrewLoadId, StringComparison.OrdinalIgnoreCase)))
            return;

        var parsed = JsonConvert.DeserializeObject(SlotJson, slots.GetType()) as IList;
        if (parsed == null || parsed.Count == 0) return;
        slots.Add(parsed[0]);
    }

    private static object? ReadMember(object target, string name)
    {
        var type = target.GetType();
        for (var current = type; current != null; current = current.BaseType)
        {
            var property = current.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(target, null);
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(target);
        }
        return null;
    }

    private static void LogFailure(Exception exception)
    {
        if (failureLogged) return;
        failureLogged = true;
        TweaksAndThingsPlugin.LogException("Unable to add the crew-hours load slot to a caboose definition", exception);
    }
}

[HarmonyPatch]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class Car_Definition_Get_CrewLoadSlot_Patch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var getter = typeof(Car).GetProperty("Definition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetGetMethod(true);
        if (getter != null) yield return getter;
    }

    private static void Postfix(object? __result)
    {
        try
        {
            if (__result != null) CabooseCrewLoadSlot_Patch.EnsureCrewLoadSlot(__result);
        }
        catch (Exception exception)
        {
            TweaksAndThingsPlugin.LogException("Unable to add the crew-hours load slot to a caboose definition", exception);
        }
    }
}
