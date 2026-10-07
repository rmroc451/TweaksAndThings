using System.Collections.Generic;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcTimetableProtectionTests
{
    [Test]
    public void OwnershipFilterCannotRemoveNpcLockedCallsButStillFiltersPlayerTrains()
    {
        var captured = new Dictionary<string, string> { ["NI0001"] = "Sylva,Dillsboro,Whittier" };
        var filtered = new Dictionary<string, string> { ["PLAYER"] = "Whittier" };
        NpcTimetableProtection.Merge(filtered, captured, s => s);
        Assert.That(filtered["NI0001"], Is.EqualTo("Sylva,Dillsboro,Whittier"));
        Assert.That(filtered["PLAYER"], Is.EqualTo("Whittier"));
        filtered["NI0001"] = "Whittier";
        NpcTimetableProtection.Merge(filtered, captured, s => s);
        Assert.That(filtered["NI0001"], Is.EqualTo("Sylva,Dillsboro,Whittier"));
    }
    [Test]
    public void RestoresDeletedAndEditedNpcsWithoutDiscardingPlayerEdits()
    {
        var saved = new Dictionary<string, string> { ["NPC-A"] = "original A", ["NPC-B"] = "original B" };
        var proposed = new Dictionary<string, string> { ["NPC-A"] = "tampered class and times", ["PLAYER"] = "new player schedule" };
        Assert.That(NpcTimetableProtection.Merge(proposed, saved, s => s), Is.True);
        Assert.That(proposed["NPC-A"], Is.EqualTo("original A"));
        Assert.That(proposed["NPC-B"], Is.EqualTo("original B"));
        Assert.That(proposed["PLAYER"], Is.EqualTo("new player schedule"));
        Assert.That(NpcTimetableProtection.Merge(proposed, saved, s => s), Is.False);
    }

    [Test]
    public void CompletingNpcReleasesItsColumnForRemoval()
    {
        var proposed = new Dictionary<string, string>();
        var snapshots = new Dictionary<string, string>();
        Assert.That(NpcTimetableProtection.Merge(proposed, snapshots, s => s), Is.False);
        Assert.That(proposed, Is.Empty);
    }
}
