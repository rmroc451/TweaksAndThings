using System.Collections.Generic;
using System.Linq;
using Game;
using Network;
using Network.Messages;
using Network.Server;
using UI.Common;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class ModJoinCompatibility
{
    private sealed class Waiting
    {
        internal IServerManager Server = null!;
        internal double Started;
        internal bool Verified;
    }
    private static readonly Dictionary<ClientId, Waiting> waiting = new();
    private static string? prompt;
    private static HostManager? host;
    internal static string Version => TweaksAndThingsPlugin.ModVersion;
    internal static void Connected(HostManager manager, IServerManager server, ClientId id, ulong steamId)
    {
        if (!ReferenceEquals(host, manager)) { waiting.Clear(); host = manager; }
        if (steamId == manager.MySteamId) return; // Native local host connection.
        waiting[id] = new Waiting { Server = server, Started = Time.realtimeSinceStartup };
    }
    internal static void Disconnected(ClientId id) => waiting.Remove(id);
    internal static void Reset() { waiting.Clear(); host = null; prompt = null; }
    internal static bool Validate(IServerManager server, ClientId id, ref INetworkMessage message)
    {
        if (!waiting.TryGetValue(id, out var pending))
        {
            // Only the native local-host connection can bypass the wire proof.
            var client = HostManager.Shared?.ClientForId(id);
            if (client != null && client.RemotePlayerSteamId == HostManager.Shared.MySteamId) return true;
            Reject(server, id, null); return false;
        }
        if (message is Login login)
        {
            if (!ModJoinPolicy.TryDecode(login.Name, out var name, out var version) || !ModJoinPolicy.Matches(Version, version))
            { Reject(server, id, version); return false; }
            login.Name = name;
            message = login;
            pending.Verified = true;
            ModDiagnosticLog.Write("MULTIPLAYER", "Verified joining client " + id + " at mod version " + version);
        }
        if (message is RequestActive && !pending.Verified)
        { Reject(server, id, null); return false; }
        return true;
    }
    private static void Reject(IServerManager server, ClientId id, string? actual)
    {
        string reason = ModJoinPolicy.Rejection(Version, actual);
        // Native messages keep rejection readable even on an unmodded client.
        server.SendTo(id, new Alert(AlertStyle.Console, AlertLevel.Error, ModJoinPolicy.RejectionMarker + reason, 0));
        server.DropClient(id, 2001, reason);
        waiting.Remove(id);
        ModDiagnosticLog.Write("MULTIPLAYER", "Rejected joining client " + id + ": " + reason);
    }
    internal static void ReceivedRejection(string reason) => prompt = reason;
    internal static void Update()
    {
        if (host != null && !ReferenceEquals(host, HostManager.Shared)) { waiting.Clear(); host = null; }
        foreach (var entry in waiting.Where(e => !e.Value.Verified && ModJoinPolicy.Expired(e.Value.Started, Time.realtimeSinceStartup)).ToArray())
            Reject(entry.Value.Server, entry.Key, null);
        if (prompt == null || ModalAlertController.Shared == null) return;
        string text = prompt; prompt = null;
        ModalAlertController.Present("Match the host's mod version", text,
            new[] { (true, "Open releases"), (false, "Not now") },
            open => { if (open) Application.OpenURL(ModJoinPolicy.ReleaseUrl); });
    }
}
