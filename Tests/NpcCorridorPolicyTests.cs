using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcCorridorPolicyTests
{
    [Test] public void SpawnNegotiatesDistantAuthorityButPreservesFirmRefuge()
    {
        var policy = new NpcCorridorPolicy();
        policy.TryReserve("running", new[] { "tail", "refuge", "distant", "spawn" }, new string[0], new string[0]);
        var firm = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> {
            ["running"] = new(new[] { "tail", "refuge" })
        };
        Assert.That(policy.TryAdmit(new[] { "spawn", "distant" }, new string[0], firm), Is.True);
        Assert.That(policy.Covers("running", new[] { "tail", "refuge" }), Is.True);
        Assert.That(policy.OwnerOf("spawn"), Is.Null);
        Assert.That(policy.TryReserve("new", new[] { "spawn", "distant" }, new string[0], new string[0]), Is.True);
        Assert.That(policy.TryReserve("new", new[] { "refuge" }, new string[0], new string[0]), Is.False);
    }
    [TestCase("refuge")]
    [TestCase("occupied")]
    public void FailedNegotiationLeavesAllAuthoritiesIntact(string obstruction)
    {
        var policy = new NpcCorridorPolicy();
        policy.TryReserve("one", new[] { "distant" }, new string[0], new string[0]);
        policy.TryReserve("two", new[] { "refuge" }, new string[0], new string[0]);
        var firm = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> {
            ["one"] = new(), ["two"] = new(new[] { "refuge" })
        };
        Assert.That(policy.TryAdmit(new[] { "distant", obstruction }, new[] { "occupied" }, firm), Is.False);
        Assert.That(policy.OwnerOf("distant"), Is.EqualTo("one"));
        Assert.That(policy.OwnerOf("refuge"), Is.EqualTo("two"));
    }
    [Test] public void OpposingTrainsCannotReserveSameCorridor()
    {
        var policy = new NpcCorridorPolicy();
        Assert.That(policy.TryReserve("east", new[] { "a", "b" }, new string[0], new string[0]), Is.True);
        Assert.That(policy.TryReserve("west", new[] { "b", "a" }, new string[0], new string[0]), Is.False);
    }
    [Test] public void ParallelPassingTracksAllowSimultaneousReservations()
    {
        var policy = new NpcCorridorPolicy();
        Assert.That(policy.TryReserve("east", new[] { "main" }, new string[0], new string[0]), Is.True);
        Assert.That(policy.TryReserve("west", new[] { "siding" }, new string[0], new string[0]), Is.True);
    }
    [Test] public void ForeignOccupancyPreventsGrantButOwnConsistDoesNot()
    {
        var policy = new NpcCorridorPolicy();
        Assert.That(policy.TryReserve("east", new[] { "a", "b" }, new[] { "a" }, new[] { "b" }), Is.False);
        Assert.That(policy.TryReserve("west", new[] { "a" }, new[] { "a" }, new string[0]), Is.True);
    }
    [Test] public void TailRetainsOldCorridorUntilCleared()
    {
        var policy = new NpcCorridorPolicy();
        policy.TryReserve("east", new[] { "a", "b" }, new[] { "a" }, new string[0]);
        policy.TryReserve("east", new[] { "c" }, new[] { "b" }, new string[0]);
        Assert.That(policy.TryReserve("west", new[] { "b" }, new string[0], new string[0]), Is.False);
        policy.RetainOccupied("east", new[] { "c" });
        Assert.That(policy.TryReserve("west", new[] { "b" }, new string[0], new string[0]), Is.True);
    }
    [Test] public void FailedClaimDoesNotPartiallyReserveOrReplaceExistingClaim()
    {
        var policy = new NpcCorridorPolicy();
        policy.TryReserve("east", new[] { "a" }, new string[0], new string[0]);
        policy.TryReserve("west", new[] { "b" }, new string[0], new string[0]);
        Assert.That(policy.TryReserve("east", new[] { "b", "c" }, new string[0], new string[0]), Is.False);
        Assert.That(policy.TryReserve("third", new[] { "c" }, new string[0], new string[0]), Is.True);
        Assert.That(policy.TryReserve("third", new[] { "a" }, new string[0], new string[0]), Is.False);
    }
    [Test] public void RemovedTrainAndRestoredSaveReleaseReservations()
    {
        var policy = new NpcCorridorPolicy();
        policy.TryReserve("east", new[] { "a" }, new string[0], new string[0]);
        policy.Remove("east");
        Assert.That(policy.TryReserve("west", new[] { "a" }, new string[0], new string[0]), Is.True);
        policy.Clear();
        Assert.That(policy.TryReserve("east", new[] { "a" }, new string[0], new string[0]), Is.True);
    }
    [Test] public void ReroutedTrainCannotReuseGrantForDifferentPassingTrack()
    {
        var policy = new NpcCorridorPolicy();
        policy.TryReserve("east", new[] { "entry", "main", "exit" }, new string[0], new string[0]);
        Assert.That(policy.Covers("east", new[] { "main", "exit" }), Is.True);
        Assert.That(policy.Covers("east", new[] { "siding", "exit" }), Is.False);
    }
    [Test] public void RollingOnwardGrantsKeepOccupiedTailAndBlockedHopKeepsRefuge()
    {
        var policy = new NpcCorridorPolicy();
        Assert.That(policy.TryReserve("east", new[] { "approach", "refuge" }, new[] { "approach" }, new string[0]), Is.True);
        Assert.That(policy.TryReserve("east", new[] { "next" }, new[] { "approach", "refuge" }, new string[0]), Is.True);
        Assert.That(policy.Covers("east", new[] { "approach", "refuge", "next" }), Is.True);
        Assert.That(policy.TryReserve("east", new[] { "blocked" }, new[] { "refuge" }, new[] { "blocked" }), Is.False);
        Assert.That(policy.Covers("east", new[] { "refuge" }), Is.True);
        Assert.That(policy.TryReserve("west", new[] { "refuge" }, new string[0], new string[0]), Is.False);
    }
    [Test] public void NewSpawnCannotOccupyAlreadyGrantedTrack()
    {
        var policy = new NpcCorridorPolicy();
        policy.TryReserve("running", new[] { "corridor", "spawn" }, new[] { "corridor" }, new string[0]);
        Assert.That(policy.OwnerOf("spawn"), Is.EqualTo("running"));
        Assert.That(policy.Covers("running", new[] { "spawn" }), Is.True);
        policy.Remove("running");
        Assert.That(policy.OwnerOf("spawn"), Is.Null);
    }
}
