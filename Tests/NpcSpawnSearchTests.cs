using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcSpawnSearchTests
{
    [Test]
    public void RefinesClosestFitAcrossTrackBoundaries()
    {
        var offset = NpcSpawnSearch.FirstFit(d => d >= 347.8, 1000);
        Assert.That(offset, Is.GreaterThanOrEqualTo(347.8).And.LessThanOrEqualTo(348.05));
    }

    [Test]
    public void ChoosesFirstAvailablePocketBeforeLaterFreeTrack()
    {
        var offset = NpcSpawnSearch.FirstFit(d => d >= 113 && d <= 150 || d >= 500, 1000);
        Assert.That(offset, Is.GreaterThanOrEqualTo(113).And.LessThanOrEqualTo(113.25));
    }

    [Test]
    public void IncludesTerminalAndExactSearchEndpoint()
    {
        Assert.That(NpcSpawnSearch.FirstFit(_ => true, 0), Is.Zero);
        Assert.That(NpcSpawnSearch.FirstFit(d => d >= 99, 99), Is.EqualTo(99));
        Assert.That(NpcSpawnSearch.FirstFit(_ => false, 99), Is.Null);
    }
}
