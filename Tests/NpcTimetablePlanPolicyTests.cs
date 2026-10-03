using System.Collections.Generic;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcTimetablePlanPolicyTests
{
    [Test] public void SparseHumanScheduleUsesNativeLegWeightsAcrossMidnight()
    {
        Assert.That(NpcTimetablePlanPolicy.Interpolate(1430, 1470, 15, 40), Is.EqualTo(1445));
        Assert.That(NpcTimetablePlanPolicy.Interpolate(1430, 1470, 40, 40), Is.EqualTo(1470));
    }
    [Test] public void IncludesEveryLocationAndUsesSharedJunctionAcrossBranches()
    {
        var edges = new[] { new NpcTimetablePlanPolicy.Link("A", "J", 10), new("J", "B", 15), new("J", "C", 8), new("C", "D", 12) };
        Assert.That(NpcTimetablePlanPolicy.Path(edges, "A", "D"), Is.EqualTo(new[] { "A", "J", "C", "D" }));
        Assert.That(NpcTimetablePlanPolicy.Path(edges, "D", "A"), Is.EqualTo(new[] { "D", "C", "J", "A" }));
    }
    [Test] public void DisconnectedLocationsHaveNoInventedRoute()
    {
        Assert.That(NpcTimetablePlanPolicy.Path(new[] { new NpcTimetablePlanPolicy.Link("A", "B", 5) }, "A", "C"), Is.Empty);
    }
    [Test] public void PlansMeetBeforeHeadOnCrossingAtUsablePassingLocation()
    {
        var partner = new Dictionary<string, int> { ["B"] = 90, ["C"] = 75 };
        Assert.That(NpcTimetablePlanPolicy.Meet(new[] { "A", "B", "C", "D" }, new[] { 60, 70, 85, 95 }, partner, c => c == "B"), Is.EqualTo(1));
        Assert.That(NpcTimetablePlanPolicy.Meet(new[] { "A", "B", "C", "D" }, new[] { 60, 70, 85, 95 }, partner, c => false), Is.EqualTo(-1));
    }
    [Test] public void NoMeetWhenOpposingTrainHasAlreadyPassed()
    {
        Assert.That(NpcTimetablePlanPolicy.Meet(new[] { "A", "B", "C", "D" }, new[] { 100, 110, 120, 130 },
            new Dictionary<string, int> { ["B"] = 80, ["C"] = 70 }, c => true), Is.EqualTo(-1));
    }
    [Test] public void AllowanceHandlesCrossMidnightWithoutComparingClockStrings()
    {
        Assert.That(NpcTimetablePlanPolicy.Meet(new[] { "A", "B", "C", "D" }, new[] { 1430, 1440, 1455, 1470 },
            new Dictionary<string, int> { ["B"] = 1460, ["C"] = 1450 }, c => true), Is.EqualTo(1));
    }
}
