using Game.State;

namespace RMROC451.TweaksAndThings;

internal static class ModSettingsAuthority
{
    internal static bool HostControlsLocked => StateManager.Shared != null && !StateManager.IsHost;
}
