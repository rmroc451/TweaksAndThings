using HarmonyLib;
using Model;
using Model.Definition;
using Model.Definition.Data;
using System.Linq;

namespace RMROC451.TweaksAndThings.Patches;

// Setup receives a complete definition; constructors do not. Never read Car.Definition
// from a constructor hook before DefinitionInfo has been assigned.
[HarmonyPatch(typeof(Car), nameof(Car.Setup))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class CabooseCrewLoadSlot_Patch
{
    private static void Prefix(CarDescriptor descriptor) => EnsureCrewLoadSlot(descriptor.DefinitionInfo.Definition);

    internal static void EnsureCrewLoadSlot(CarDefinition definition)
    {
        if (definition == null || definition.Archetype != CarArchetype.Caboose) return;
        if (definition.LoadSlots == null) definition.LoadSlots = new System.Collections.Generic.List<LoadSlot>();
        if (definition.LoadSlots.Any(slot => slot.RequiredLoadIdentifier == "crew-hours")) return;
        definition.LoadSlots.Add(new LoadSlot(LoadUnits.Quantity, 8, "crew-hours"));
    }
}
