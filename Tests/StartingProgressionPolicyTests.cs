using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public sealed class StartingProgressionPolicyTests
{
    private static Dictionary<string, string[]> Railroad() => new()
    {
        ["sylva"] = Array.Empty<string>(), ["whittier"] = new[] { "sylva" },
        ["bryson"] = new[] { "whittier" }, ["andrews"] = new[] { "bryson" },
        ["industry"] = new[] { "whittier", "bryson" }
    };
    [Test]
    public void BrysonStartUnlocksNativeApproachWithoutChangingExpansionTree()
    {
        var native = Railroad();
        var selected = StartingProgressionPolicy.Select(native, Array.Empty<string>(), "bryson");
        Assert.That(selected, Is.EquivalentTo(new[] { "sylva", "whittier", "bryson" }));
        Assert.That(native["bryson"], Is.EqualTo(new[] { "whittier" }));
        Assert.That(selected, Does.Not.Contain("andrews"));
    }
    [Test]
    public void MultiApproachStartIncludesEveryRequiredBranch()
    {
        var selected = StartingProgressionPolicy.Select(Railroad(), Array.Empty<string>(), "industry");
        Assert.That(selected, Is.EquivalentTo(new[] { "sylva", "whittier", "bryson", "industry" }));
    }
    [Test]
    public void AndrewsSelectionIncludesTransitivePrerequisites()
    {
        var selected = StartingProgressionPolicy.Select(Railroad(), Array.Empty<string>(), "andrews");
        Assert.That(selected, Is.EquivalentTo(new[] { "sylva", "whittier", "bryson", "andrews" }));
    }
    [Test]
    public void RemovingBrysonRemovesAndrewsAndDependentIndustry()
    {
        var selected = StartingProgressionPolicy.Remove(Railroad(), Railroad().Keys, "bryson");
        Assert.That(selected, Is.EquivalentTo(new[] { "sylva", "whittier" }));
    }
    [Test]
    public void BrysonOriginReversesOnlyItsApproachAndDoesNotUnlockIt()
    {
        var native = Railroad();
        var graph = StartingProgressionPolicy.ForOrigin(native, "bryson");
        Assert.That(graph["bryson"], Is.Empty);
        Assert.That(graph["whittier"], Is.EqualTo(new[] { "bryson" }));
        Assert.That(graph["sylva"], Is.EqualTo(new[] { "whittier" }));
        Assert.That(graph["andrews"], Is.EqualTo(new[] { "bryson" }));
        Assert.That(StartingProgressionPolicy.Select(graph, Array.Empty<string>(), "bryson"), Is.EquivalentTo(new[] { "bryson" }));
        Assert.That(native["bryson"], Is.EqualTo(new[] { "whittier" }));
    }
    [Test]
    public void NormalOriginDoesNotChangeNativeDependencies() =>
        Assert.That(StartingProgressionPolicy.ForOrigin(Railroad(), StartingProgressionPolicy.OriginalOrigin)["andrews"], Is.EqualTo(new[] { "bryson" }));
    [Test]
    public void MissingDefinitionsAndCyclesFailValidation()
    {
        Assert.Throws<ArgumentException>(() => StartingProgressionPolicy.Validate(new Dictionary<string, string[]> { ["a"] = new[] { "missing" } }));
        Assert.Throws<ArgumentException>(() => StartingProgressionPolicy.Validate(new Dictionary<string, string[]> { ["a"] = new[] { "b" }, ["b"] = new[] { "a" } }));
    }
    [Test]
    public void AmbiguousMultiApproachOriginIsNotSilentlyUnlocked() =>
        Assert.Throws<ArgumentException>(() => StartingProgressionPolicy.ForOrigin(Railroad(), "industry"));
}
