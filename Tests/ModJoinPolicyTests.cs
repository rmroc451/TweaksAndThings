using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public sealed class ModJoinPolicyTests
{
    [TestCase("RMROC451", "2.2.0")]
    [TestCase("Train Driver: 🚂", "2.2.0-beta.1")]
    public void LoginPreservesNameAndExactVersion(string name, string version)
    {
        Assert.That(ModJoinPolicy.TryDecode(ModJoinPolicy.Encode(name, version), out var decoded, out var actual), Is.True);
        Assert.That(decoded, Is.EqualTo(name));
        Assert.That(ModJoinPolicy.Matches(version, actual), Is.True);
    }
    [TestCase("Unmodded player")]
    [TestCase("TNT-JOIN-1:")]
    [TestCase("TNT-JOIN-1:2.2.0:bad base64")]
    [TestCase("")]
    public void MissingOrMalformedProofCannotJoin(string wire) => Assert.That(ModJoinPolicy.TryDecode(wire, out _, out _), Is.False);
    [TestCase("2.2.0", "2.1.9")]
    [TestCase("2.2.0", "2.2.0-beta.1")]
    [TestCase("2.2.0", "")]
    [TestCase("", "")]
    public void RequireExactHostVersion(string host, string client) => Assert.That(ModJoinPolicy.Matches(host, client), Is.False);
    [Test]
    public void SilentClientsHaveBoundedAdmissionTime()
    {
        Assert.That(ModJoinPolicy.Expired(100, 129.9), Is.False);
        Assert.That(ModJoinPolicy.Expired(100, 130), Is.True);
    }
    [Test]
    public void RejectionExplainsUnreleasedBuildAndDownloadUrl()
    {
        string message = ModJoinPolicy.Rejection("2.2.0", "2.1.9");
        Assert.That(message, Does.Contain("2.2.0").And.Contain("2.1.9").And.Contain(ModJoinPolicy.ReleaseUrl).And.Contain("ask the host"));
    }
}
