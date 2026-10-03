using System;
using System.IO;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class ModDiagnosticLogTests
{
    [Test]
    public void LogIsFlushedAndRepeatedDiagnosticsAreThrottled()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TweaksAndThings-log-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            ModDiagnosticLog.Initialize(directory, Assert.Fail);
            ModDiagnosticLog.Write("TRAFFIC", "First attempt", "same-key");
            ModDiagnosticLog.Write("TRAFFIC", "Repeated attempt", "same-key");
            string content = File.ReadAllText(ModDiagnosticLog.FilePath);
            Assert.That(content, Does.Contain("First attempt").And.Not.Contain("Repeated attempt"));
            Assert.That(content, Does.Contain("Starting TweaksAndThings diagnostics"));
            Assert.That(ModDiagnosticLog.FilePath, Does.StartWith(directory));
        }
        finally { File.Delete(Path.Combine(directory, "TweaksAndThings-debug.log")); Directory.Delete(directory); }
    }

    [Test]
    public void SizeRotationRetainsPreviousSessionWithoutLosingNewEvents()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TweaksAndThings-log-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "TweaksAndThings-debug.log");
        try
        {
            ModDiagnosticLog.Initialize(directory, Assert.Fail);
            File.WriteAllText(path, new string('x', 5 * 1024 * 1024));
            ModDiagnosticLog.Write("TRAFFIC", "New event");
            Assert.That(new FileInfo(path + ".previous").Length, Is.EqualTo(5 * 1024 * 1024));
            Assert.That(File.ReadAllText(path), Does.Contain("New event"));
        }
        finally { File.Delete(path); File.Delete(path + ".previous"); Directory.Delete(directory); }
    }
}
