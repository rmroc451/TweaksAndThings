using HarmonyLib;
using Game;
using Network;
using Network.Client;
using Network.Messages;
using Network.Server;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(HostManager), nameof(HostManager.ClientDidConnect))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ModJoinCompatibility_Connect_Patch
{
    private static void Postfix(HostManager __instance, IServerManager __0, ClientId __1, ulong __2) =>
        ModJoinCompatibility.Connected(__instance, __0, __1, __2);
}
[HarmonyPatch(typeof(HostManager), nameof(HostManager.ClientDidDisconnect))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ModJoinCompatibility_Disconnect_Patch
{
    private static void Prefix(ClientId __1) => ModJoinCompatibility.Disconnected(__1);
}
[HarmonyPatch(typeof(HostManager), nameof(HostManager.HandleMessage))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ModJoinCompatibility_Message_Patch
{
    private static bool Prefix(IServerManager __0, ClientId __1, ref INetworkMessage __2) =>
        ModJoinCompatibility.Validate(__0, __1, ref __2);
}
[HarmonyPatch(typeof(GameClient), nameof(GameClient.SendLogin))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ModJoinCompatibility_Login_Patch
{
    // Scope the constructor hook to this native login call, not arbitrary login objects.
    [System.ThreadStatic] internal static int Sending;
    private static void Prefix() => Sending++;
    private static System.Exception? Finalizer(System.Exception? __exception) { Sending--; return __exception; }
}
[HarmonyPatch(typeof(Login), MethodType.Constructor, typeof(string), typeof(string), typeof(Game.Messages.Snapshot.CharacterCustomization))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ModJoinCompatibility_LoginPayload_Patch
{
    private static void Postfix(ref Login __instance)
    {
        if (ModJoinCompatibility_Login_Patch.Sending > 0)
            __instance.Name = ModJoinPolicy.Encode(__instance.Name, ModJoinCompatibility.Version);
    }
}
[HarmonyPatch(typeof(GameClient), nameof(GameClient.HandleMessage))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ModJoinCompatibility_Rejection_Patch
{
    private static bool Prefix(INetworkMessage __0)
    {
        if (__0 is Alert alert && alert.Message?.StartsWith(ModJoinPolicy.RejectionMarker, System.StringComparison.Ordinal) == true)
        {
            ModJoinCompatibility.ReceivedRejection(alert.Message.Substring(ModJoinPolicy.RejectionMarker.Length));
            return false;
        }
        return true;
    }
}
