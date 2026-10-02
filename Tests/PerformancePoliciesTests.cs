using System.Collections.Generic;
using NUnit.Framework;
using RMROC451.TweaksAndThings;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class PerformancePoliciesTests
{
    [Test]
    public void RetainActiveCrewStatuses_KeepsOnlyStatusesForCaboosesFoundThisScan()
    {
        var statuses = new Dictionary<string, (bool spotted, bool filling)>
        {
            ["active"] = (true, false),
            ["moved"] = (false, true),
            ["removed"] = (true, true)
        };

        var result = PerformancePolicies.RetainActiveCrewStatuses(
            statuses,
            new HashSet<string> { "active", "moved" });

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result["active"], Is.EqualTo((true, false)));
        Assert.That(result["moved"], Is.EqualTo((false, true)));
        Assert.That(result.ContainsKey("removed"), Is.False);
    }


    [Test]
    public void SelectIds_CollectsOnlyMatchingIdsInOnePass()
    {
        var visits = 0;
        var rows = new[]
        {
            (id: "visible", hidden: false),
            (id: "hidden-a", hidden: true),
            (id: "hidden-b", hidden: true)
        };

        var result = PerformancePolicies.SelectIds(
            rows,
            row => { visits++; return row.hidden; },
            row => row.id);

        Assert.That(visits, Is.EqualTo(rows.Length));
        Assert.That(result, Is.EquivalentTo(new[] { "hidden-a", "hidden-b" }));
    }


    [Test]
    public void SummarizeOilingConsist_ReportsFlagsAndLowestOilInOnePass()
    {
        var visits = 0;
        var cars = new[]
        {
            (NeedsOiling: false, HasHotbox: false, Oiled: 0.8f),
            (NeedsOiling: true, HasHotbox: true, Oiled: 0.35f),
            (NeedsOiling: false, HasHotbox: false, Oiled: 0.6f)
        };

        var result = PerformancePolicies.SummarizeOilingConsist(
            CountVisits(cars, () => visits++));

        Assert.That(visits, Is.EqualTo(cars.Length));
        Assert.That(result.HasNeedsOiling, Is.True);
        Assert.That(result.HasHotbox, Is.True);
        Assert.That(result.LowestOil, Is.EqualTo(0.35f).Within(0.0001f));
    }

    [Test]
    public void SummarizeOilingConsist_EmptyInputHasNoFlagsOrMinimum()
    {
        var result = PerformancePolicies.SummarizeOilingConsist(
            new (bool NeedsOiling, bool HasHotbox, float Oiled)[0]);

        Assert.That(result.HasNeedsOiling, Is.False);
        Assert.That(result.HasHotbox, Is.False);
        Assert.That(result.LowestOil, Is.Null);
    }

    private static IEnumerable<(bool NeedsOiling, bool HasHotbox, float Oiled)> CountVisits(
        IEnumerable<(bool NeedsOiling, bool HasHotbox, float Oiled)> cars,
        System.Action visited)
    {
        foreach (var car in cars)
        {
            visited();
            yield return car;
        }
    }


    [TestCase(true, false, false)]
    [TestCase(false, false, false)]
    [TestCase(false, true, true)]
    public void ShouldReportConsistOiling_EvaluatesCabooseRuleOnlyForHotboxOnlyCase(
        bool needsOil, bool hasHotbox, bool expected)
    {
        var cabooseCheckCount = 0;

        var result = PerformancePolicies.ShouldReportConsistOiling(
            needsOil,
            hasHotbox,
            () => { cabooseCheckCount++; return true; });

        Assert.That(result, Is.EqualTo(expected));
        Assert.That(cabooseCheckCount, Is.EqualTo(!needsOil && hasHotbox ? 1 : 0));
    }
}
