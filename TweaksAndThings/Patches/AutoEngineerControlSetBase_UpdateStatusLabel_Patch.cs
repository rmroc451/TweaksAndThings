using HarmonyLib;
using Railloader;
using UI.Builder;
using UI.EngineControls;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(AutoEngineerControlSetBase))]
[HarmonyPatch(nameof(AutoEngineerControlSetBase.UpdateStatusLabel))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class AutoEngineerControlSetBase_UpdateStatusLabel_Patch
{
    static void Postfix(AutoEngineerControlSetBase __instance)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled() || !AutoEngineerOrdersHelper_SendAutoEngineerCommand_Patch.SafetyFirstGoverningApplies()) return;

        string orig = __instance.statusLabel.text;
        __instance.statusLabel.text = $"{orig}; <b>Safety</b>";
        __instance.statusLabel.rectTransform.Tooltip("Status", $"<b>Safety First, Speed Limited</b>\n\n{orig}");
    }
}
