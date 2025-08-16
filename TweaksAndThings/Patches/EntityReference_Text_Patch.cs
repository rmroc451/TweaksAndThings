using HarmonyLib;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(EntityReference))]
[HarmonyPatch(nameof(EntityReference.Text))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class EntityReference_Text_Patch
{
    static void Postfix(ref string __result)
    {
        if (__result == "Unknown")
            __result = string.Empty;
    }
}
