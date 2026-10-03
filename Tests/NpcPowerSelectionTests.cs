using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcPowerSelectionTests
{
    [Test]
    public void MixesEnginesToAvoidOversizing()
    {
        var selected = NpcPowerSelection.Select(new[] { new[] { 60d }, new[] { 40d } }, new[] { 100d });
        Assert.That(selected, Is.EquivalentTo(new[] { 0, 1 }));
    }

    [Test]
    public void ChoosesSmallerSingleEngineWhenEnough()
    {
        Assert.That(NpcPowerSelection.Select(new[] { new[] { 200d }, new[] { 105d } }, new[] { 100d }), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void AddsMultipleUnitsForHeavyTrain()
    {
        Assert.That(NpcPowerSelection.Select(new[] { new[] { 100d } }, new[] { 250d }), Is.EqualTo(new[] { 0, 0, 0 }));
    }

    [Test]
    public void MustCoverEveryRouteSpeedAndGrade()
    {
        var capacities = new[] { new[] { 100d, 10d }, new[] { 10d, 100d } };
        Assert.That(NpcPowerSelection.Select(capacities, new[] { 110d, 110d }), Is.EquivalentTo(new[] { 0, 1 }));
    }

    [Test]
    public void RejectsImpossibleOrExcessivePower()
    {
        Assert.That(NpcPowerSelection.Select(new[] { new[] { 0d } }, new[] { 100d }), Is.Null);
        Assert.That(NpcPowerSelection.Select(new[] { new[] { 10d } }, new[] { 100d }, maxUnits: 2), Is.Null);
        Assert.That(NpcPowerSelection.Select(System.Array.Empty<double[]>(), new[] { 100d }), Is.Null);
    }

    [Test]
    public void FlatRouteNeedsOnlyOneEngine()
    {
        Assert.That(NpcPowerSelection.Select(new[] { new[] { 100d } }, new[] { 0d }), Is.EqualTo(new[] { 0 }));
    }

    [Test]
    public void PrefersOneAdequateEngineOverManySmallExactMatches()
    {
        Assert.That(NpcPowerSelection.Select(new[] { new[] { 20d }, new[] { 110d } }, new[] { 100d }), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void PrefersTwoUnitsOverACloserThreeUnitMatch()
    {
        var units = NpcPowerSelection.Select(new[] { new[] { 34d }, new[] { 60d } }, new[] { 100d });
        Assert.That(units, Has.Count.EqualTo(2));
    }

    [Test]
    public void ChecksComplementaryTwoEngineCombinationBeforeAddingMore()
    {
        var capacities = new[] { new[] { 120d, -10d }, new[] { -10d, 120d }, new[] { 20d, 20d } };
        Assert.That(NpcPowerSelection.Select(capacities, new[] { 100d, 100d }), Is.EquivalentTo(new[] { 0, 1 }));
    }
}
