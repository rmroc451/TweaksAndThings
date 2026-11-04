namespace RMROC451.TweaksAndThings;

internal static class NpcTrackAccessPolicy
{
    internal static bool CanUse(bool enabled, bool available, bool npcPlacement) => enabled && (available || npcPlacement);
}
