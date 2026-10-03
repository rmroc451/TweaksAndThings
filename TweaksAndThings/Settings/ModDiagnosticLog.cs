using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RMROC451.TweaksAndThings;

// Bounded, flushed diagnostics: the file can be shared even after a game crash.
internal static class ModDiagnosticLog
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly object gate = new();
    private static readonly Dictionary<string, DateTime> lastWrites = new();
    internal static string FilePath { get; private set; } = string.Empty;
    private static bool failureReported;
    private static Action<string>? onFailure;

    internal static void Initialize(string directory, Action<string> reportFailure)
    {
        lock (gate)
        {
            FilePath = Path.Combine(directory, "TweaksAndThings-debug.log");
            onFailure = reportFailure;
            failureReported = false;
            lastWrites.Clear();
        }
        Write("SESSION", "Starting TweaksAndThings diagnostics; UTC timestamps. Previous sessions are retained until rotation.");
    }

    internal static void Write(string category, string message, string? throttleKey = null, double intervalSeconds = 30)
    {
        lock (gate)
        {
            if (FilePath.Length == 0) return;
            DateTime now = DateTime.UtcNow;
            if (throttleKey != null && lastWrites.TryGetValue(throttleKey, out var last) && (now - last).TotalSeconds < intervalSeconds) return;
            try
            {
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length >= MaxBytes)
                {
                    string previous = FilePath + ".previous";
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(FilePath, previous);
                }
                File.AppendAllText(FilePath, $"{now:O} [{category}] {message}{Environment.NewLine}", new UTF8Encoding(false));
                if (throttleKey != null) lastWrites[throttleKey] = now;
            }
            catch (Exception ex)
            {
                if (failureReported) return;
                failureReported = true;
                onFailure?.Invoke("Cannot write mod diagnostic log at " + FilePath + ": " + ex.Message);
            }
        }
    }
}
