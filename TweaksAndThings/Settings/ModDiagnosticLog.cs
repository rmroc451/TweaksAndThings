using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RMROC451.TweaksAndThings;

// Bounded, flushed diagnostics: the file can be shared even after a game crash.
internal static class ModDiagnosticLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object gate = new();
    private static readonly Dictionary<string, DateTime> lastWrites = new();
    internal static string FilePath { get; private set; } = string.Empty;
    private static bool failureReported;
    private static DateTime retryRotationAfterUtc;

    internal static void Initialize(string directory)
    {
        lock (gate)
        {
            FilePath = Path.Combine(directory, "TweaksAndThings-debug.log");
            failureReported = false;
            retryRotationAfterUtc = DateTime.MinValue;
            lastWrites.Clear();
        }
        Write("SESSION", "Starting TweaksAndThings diagnostics; UTC timestamps. Archives use numbered suffixes and are retained.");
    }

    // Retain the old call shape for existing test and tooling callers; log failures never escape to UMM.
    internal static void Initialize(string directory, Action<string>? ignoredFailureCallback)
    {
        _ = ignoredFailureCallback;
        Initialize(directory);
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
                string line = $"{now:O} [{category}] {message}{Environment.NewLine}";
                long incomingBytes = Utf8.GetByteCount(line);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length + incomingBytes > MaxBytes && now >= retryRotationAfterUtc)
                {
                    try
                    {
                        Rotate();
                        retryRotationAfterUtc = DateTime.MinValue;
                    }
                    catch (Exception exception)
                    {
                        // Don't hammer a failed rename on every single log entry. Record the
                        // failure in the same log and retry after a short delay.
                        retryRotationAfterUtc = now.AddSeconds(10);
                        string failure = $"{now:O} [LOG ROTATION ERROR] Could not archive the active log: {exception.GetType().Name}: {exception.Message}{Environment.NewLine}";
                        try { File.AppendAllText(FilePath, failure, Utf8); } catch { }
                    }
                }
                File.AppendAllText(FilePath, line, Utf8);
                if (throttleKey != null) lastWrites[throttleKey] = now;
            }
            catch (Exception)
            {
                if (failureReported) return;
                failureReported = true;
                // Keep diagnostics self-contained; do not send mod-authored log messages to UMM/Player.log.
            }
        }
    }

    private static readonly UTF8Encoding Utf8 = new(false);

    private static void Rotate()
    {
        // Never shift existing archives: a stale/locked .0 or a gap in the suffixes must not
        // block rollover. Move the active file to the first unused numbered archive instead.
        int number = 0;
        string archivePath;
        do archivePath = FilePath + "." + number++;
        while (File.Exists(archivePath) || Directory.Exists(archivePath));
        File.Move(FilePath, archivePath);
    }
}
