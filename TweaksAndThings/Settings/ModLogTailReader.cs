using System;
using System.IO;
using System.Text;

namespace RMROC451.TweaksAndThings;

// Poll metadata once a second and read at most 32 KB when the file changes.
// This reader is owned by the visible Debug tab and has no background worker.
internal sealed class ModLogTailReader
{
    internal const int MaxReadBytes = 32 * 1024;
    private const int MaxLines = 160;
    private readonly string path;
    private DateTime nextPoll, lastWrite;
    private long lastLength = -1;
    private string text = "Waiting for debug log...";
    internal long BytesRead { get; private set; }

    internal ModLogTailReader(string path) { this.path = path; }
    internal string Read(DateTime utcNow, bool force = false)
    {
        if (!force && utcNow < nextPoll) return text;
        nextPoll = utcNow.AddSeconds(1);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) { lastLength = -1; return text = "Debug log has not been created yet."; }
            if (!force && info.Length == lastLength && info.LastWriteTimeUtc == lastWrite) return text;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long length = stream.Length;
            long offset = Math.Max(0, length - MaxReadBytes);
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] buffer = new byte[(int)(length - offset)];
            int read = 0;
            while (read < buffer.Length)
            {
                int count = stream.Read(buffer, read, buffer.Length - read);
                if (count == 0) break;
                read += count;
            }
            BytesRead += read;
            string content = Encoding.UTF8.GetString(buffer, 0, read);
            // Drop a partial first line (including any split UTF-8 sequence).
            if (offset > 0)
            {
                int newline = content.IndexOf('\n');
                if (newline >= 0) content = content.Substring(newline + 1);
            }
            int lines = 0, start = 0;
            for (int i = content.Length - 1; i >= 0; i--)
                if (content[i] == '\n' && ++lines > MaxLines) { start = i + 1; break; }
            text = content.Substring(start);
            lastLength = length;
            lastWrite = info.LastWriteTimeUtc;
            return text.Length == 0 ? "Debug log is empty." : text;
        }
        catch (IOException ex) { return "Debug log temporarily unavailable: " + ex.Message; }
        catch (UnauthorizedAccessException ex) { return "Cannot read debug log: " + ex.Message; }
    }
}
