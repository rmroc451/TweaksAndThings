using System.Collections.Generic;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcPassingSidingPolicyTests
{
    private static NpcPassingSidingPolicy.Edge E(string id, string a, string b, double length) => new(id, a, b, length);
    private static readonly HashSet<string> Route = new() { "in", "main", "out" };
    private static NpcPassingSidingPolicy.Edge[] Graph => new[] {
        E("in", "0", "1", 100), E("main", "1", "2", 200), E("out", "2", "3", 100),
        E("s1", "1", "x", 180), E("s2", "x", "2", 220) };

    [Test] public void SumsConnectedSidingSegmentsAndReservesFoulingClearance() =>
        Assert.That(NpcPassingSidingPolicy.Longest(Graph, Route, (_, _, _) => true), Is.EqualTo(380));

    [Test] public void RejectsDeadEndAndUnrelatedLoop() =>
        Assert.That(NpcPassingSidingPolicy.Longest(new[] { E("in", "0", "1", 100), E("main", "1", "2", 200),
            E("out", "2", "3", 100), E("spur", "1", "x", 1000), E("loop", "u", "v", 5000) }, Route, (_, _, _) => true), Is.Zero);

    [Test] public void RejectsDisabledOrGeometricallyUnreachableConnection() =>
        Assert.That(NpcPassingSidingPolicy.Longest(Graph, Route, (node, _, _) => node != "2"), Is.Zero);

    [Test] public void IndustrySpurDoesNotInvalidateOrLengthenPassingSiding() {
        var graph = new List<NpcPassingSidingPolicy.Edge>(Graph) { E("yard", "x", "y", 1000) };
        Assert.That(NpcPassingSidingPolicy.Longest(graph, Route, (_, _, _) => true), Is.EqualTo(380));
    }

    [Test] public void ChoosesLongestOfSeveralPassingSidings() {
        var graph = new List<NpcPassingSidingPolicy.Edge>(Graph) { E("long", "1", "2", 650) };
        Assert.That(NpcPassingSidingPolicy.Longest(graph, Route, (_, _, _) => true), Is.EqualTo(630));
    }

    [Test] public void DoesNotCountSidingBeyondRouteDestination() =>
        Assert.That(NpcPassingSidingPolicy.Longest(Graph, Route, (_, _, _) => true,
            junctions: new HashSet<string> { "1" }), Is.Zero);

    [Test] public void UsesSwitchFoulingDistancesForUsableLength() =>
        Assert.That(NpcPassingSidingPolicy.Longest(Graph, Route, (_, _, _) => true,
            foulingClearance: (_, _) => 70), Is.EqualTo(330));
}
