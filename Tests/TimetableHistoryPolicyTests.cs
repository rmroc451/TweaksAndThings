using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;

namespace RMROC451.TweaksAndThings.Tests;

public class TimetableHistoryPolicyTests
{
    private static TimetableHistorySample S(double time, string vehicle = "engine") => new() { Minute = time, PlannedMinute = time - 5, From = "a", To = "b", Fraction = 0.5, Vehicle = vehicle };
    [Test] public void RecordsEarlyLateAndOnTime()
    {
        Assert.That(TimetableHistoryPolicy.VarianceText(5), Is.EqualTo("5 min late"));
        Assert.That(TimetableHistoryPolicy.VarianceText(-3), Is.EqualTo("3 min early"));
        Assert.That(TimetableHistoryPolicy.VarianceText(0.3), Is.EqualTo("on time"));
    }
    [Test] public void ThrottlesToOneObservationPerGameMinute()
    {
        var samples = new List<TimetableHistorySample>();
        Assert.That(TimetableHistoryPolicy.Append(samples, S(10)), Is.True);
        Assert.That(TimetableHistoryPolicy.Append(samples, S(10.5)), Is.False);
        Assert.That(TimetableHistoryPolicy.Append(samples, S(11)), Is.True);
    }
    [Test] public void RewindingDropsDiscardedFuture()
    {
        var samples = new List<TimetableHistorySample> { S(10), S(20) };
        TimetableHistoryPolicy.Append(samples, S(12));
        Assert.That(samples.ConvertAll(s => s.Minute), Is.EqualTo(new[] { 10d, 12d }));
    }
    [Test] public void MidnightUsesAbsoluteTimeAndPrunesOldDays()
    {
        var samples = new List<TimetableHistorySample> { S(100), S(1439), S(1441) };
        TimetableHistoryPolicy.Append(samples, S(2881));
        Assert.That(samples.ConvertAll(s => s.Minute), Is.EqualTo(new[] { 1441d, 2881d }));
        Assert.That(TimetableHistoryPolicy.Connect(S(1439), S(1441)), Is.True);
    }
    [Test] public void DoesNotInventTravelAcrossMissingSamplesOrReassignedTrain()
    {
        Assert.That(TimetableHistoryPolicy.Connect(S(10), S(15)), Is.False);
        Assert.That(TimetableHistoryPolicy.Connect(S(10), S(11, "other")), Is.False);
    }
    [Test] public void MapsRecordedAnchorsToBranchRowsWithoutInventingOtherBranches()
    {
        Assert.That(TimetableHistoryPolicy.Row(S(10), new[] { "a", "x", "b" }), Is.EqualTo(1));
        Assert.That(TimetableHistoryPolicy.Row(S(10), new[] { "a", "x" }), Is.Null);
    }
    [Test] public void SaveReloadPreservesOriginalPlanVariance()
    {
        var restored = JsonConvert.DeserializeObject<TimetableHistorySample>(JsonConvert.SerializeObject(S(10)))!;
        Assert.That(restored.Variance, Is.EqualTo(5));
        Assert.That(restored.From, Is.EqualTo("a"));
    }
}
