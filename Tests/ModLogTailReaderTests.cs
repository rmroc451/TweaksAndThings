using System;
using System.IO;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class ModLogTailReaderTests
{
    [Test]
    public void ReaderUsesBoundedTailAndDoesNotRereadUnchangedFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "TweaksAndThings-tail-test-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            File.WriteAllText(path, new string('x', 100000) + "\nLatest entry\n");
            var reader = new ModLogTailReader(path);
            var now = DateTime.UtcNow;
            Assert.That(reader.Read(now), Is.EqualTo("Latest entry\n"));
            Assert.That(reader.BytesRead, Is.EqualTo(ModLogTailReader.MaxReadBytes));
            reader.Read(now.AddSeconds(2));
            Assert.That(reader.BytesRead, Is.EqualTo(ModLogTailReader.MaxReadBytes));
            File.AppendAllText(path, "New event\n");
            Assert.That(reader.Read(now.AddSeconds(2.5)), Does.Not.Contain("New event"));
            Assert.That(reader.Read(now.AddSeconds(4)), Does.Contain("New event"));
            Assert.That(reader.BytesRead, Is.EqualTo(ModLogTailReader.MaxReadBytes * 2));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void RotationAndMissingLogAreHandledWithoutAnOpenFileLock()
    {
        string path = Path.Combine(Path.GetTempPath(), "TweaksAndThings-tail-test-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            var now = DateTime.UtcNow;
            var reader = new ModLogTailReader(path);
            Assert.That(reader.Read(now), Does.Contain("not been created"));
            File.WriteAllText(path, "Old event\n");
            Assert.That(reader.Read(now.AddSeconds(2)), Does.Contain("Old event"));
            File.Move(path, path + ".previous");
            File.WriteAllText(path, "New session\n");
            Assert.That(reader.Read(now.AddSeconds(4)), Is.EqualTo("New session\n"));
        }
        finally { File.Delete(path); File.Delete(path + ".previous"); }
    }
}
