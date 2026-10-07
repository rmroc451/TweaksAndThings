using System;
using System.Text;

namespace RMROC451.TweaksAndThings;

internal static class ModJoinPolicy
{
    internal const string Marker = "TNT-JOIN-1:";
    internal const string RejectionMarker = "TNT-REQUIRED:";
    internal const string ReleaseUrl = "https://github.com/rmroc451/TweaksAndThings/releases/latest";
    internal const double TimeoutSeconds = 30;
    internal static string Encode(string name, string version) => Marker + version + ":" +
        Convert.ToBase64String(Encoding.UTF8.GetBytes(name ?? string.Empty));
    internal static bool TryDecode(string wireName, out string name, out string version)
    {
        name = string.Empty; version = string.Empty;
        if (string.IsNullOrEmpty(wireName) || wireName.Length > 4096 || !wireName.StartsWith(Marker, StringComparison.Ordinal)) return false;
        int split = wireName.IndexOf(':', Marker.Length);
        if (split <= Marker.Length || split - Marker.Length > 64) return false;
        version = wireName.Substring(Marker.Length, split - Marker.Length);
        try { name = Encoding.UTF8.GetString(Convert.FromBase64String(wireName.Substring(split + 1))); return true; }
        catch (FormatException) { return false; }
    }
    internal static bool Matches(string host, string client) => !string.IsNullOrWhiteSpace(host) &&
        string.Equals(host, client, StringComparison.Ordinal);
    internal static bool Expired(double started, double now) => now - started >= TimeoutSeconds;
    internal static string Rejection(string required, string? actual) =>
        "This railroad uses TweaksAndThings " + required + ". " +
        (string.IsNullOrEmpty(actual) ? "Please install and enable the host's matching version." :
        "Your version is " + actual + "; please install the host's matching version.") +
        " Releases: " + ReleaseUrl + ". If that version is not released yet, ask the host for their build. Reconnect once you are synced.";
}
