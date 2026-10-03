using Game.Notices;
using HarmonyLib;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(NoticeManager), nameof(NoticeManager.PostEphemeralLocal))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcStatusNotice_Patch
{
    private static bool Prefix(NoticeManager __instance, EntityReference entity, string contextualKey, string content)
    {
        if (contextualKey != NpcServiceHud.NoticeKey || string.IsNullOrEmpty(content)) return true;
        string key = $"{(int)entity.Type}//{entity.Id}//{contextualKey}";
        if (!__instance._notices.TryGetValue(key, out var notice) || notice.Row == null) return true;
        if (notice.Content != content) {
            notice.Content = content;
            notice.Row.label.text = __instance.LabelTextForNotice(entity, content);
        }
        return false;
    }
}
