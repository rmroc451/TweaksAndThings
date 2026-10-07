using HarmonyLib;
using Model;
using System.Linq;
using UI;

namespace RMROC451.TweaksAndThings.Patches;

// Keep the native frame loop, marker, hit testing and Escape registration.
// DidEscape is the registered cancellation callback, not an input predicate.
[HarmonyPatch(typeof(AutoEngineerDestinationPicker), nameof(AutoEngineerDestinationPicker.StartPickingLocation))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoEngineerDestinationPicker_Loop_Patch
{
    private static bool Prefix(object[] __args) =>
        !ThroughTrafficGuard.BlockInteraction(__args.OfType<BaseLocomotive>().FirstOrDefault());
}
