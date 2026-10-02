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
}
