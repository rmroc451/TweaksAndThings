using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcTrackAccessPolicyTests
{
    [TestCase(true, false, true, true)]
    [TestCase(true, false, false, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, true, true, false)]
    [TestCase(true, true, false, true)]
    public void NpcAccessAllowsUnownedButNeverDisabledTrack(bool enabled, bool owned, bool npc, bool expected)
    {
        Assert.That(NpcTrackAccessPolicy.CanUse(enabled, owned, npc), Is.EqualTo(expected));
    }
}
