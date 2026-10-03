using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class TimetableChartGeometryTests
{
    [Test]
    public void MidnightPathKeepsTimeMovingForward()
    {
        Assert.That(TimetableChartGeometry.Unwrap(15, 1430), Is.EqualTo(1455));
        Assert.That(TimetableChartGeometry.Unwrap(20, 1455), Is.EqualTo(1460));
        Assert.That(TimetableChartGeometry.Unwrap(1460, 1455), Is.EqualTo(1460));
    }

    [Test]
    public void MidnightTravelClipsToEachSideWithoutFalseCrossing()
    {
        Assert.That(TimetableChartGeometry.Clip(1430, 0, 1450, 2, out var a, out var b, out var c, out var d), Is.True);
        Assert.That((a, b, c, d), Is.EqualTo((1430d, 0d, 1440d, 1d)));
        Assert.That(TimetableChartGeometry.Clip(-10, 0, 10, 2, out a, out b, out c, out d), Is.True);
        Assert.That((a, b, c, d), Is.EqualTo((0d, 1d, 10d, 2d)));
    }

    [Test]
    public void WaitingAtStationRetainsHorizontalDwell()
    {
        Assert.That(TimetableChartGeometry.Clip(600, 3, 620, 3, out var a, out var b, out var c, out var d), Is.True);
        Assert.That((a, b, c, d), Is.EqualTo((600d, 3d, 620d, 3d)));
    }

    [Test]
    public void OffDaySegmentsAreNotDrawn()
    {
        Assert.That(TimetableChartGeometry.Clip(-30, 0, -10, 1, out _, out _, out _, out _), Is.False);
        Assert.That(TimetableChartGeometry.Clip(1500, 0, 1520, 1, out _, out _, out _, out _), Is.False);
        Assert.That(TimetableChartGeometry.Clip(1600, 0, 1600, 1, out _, out _, out _, out _), Is.False);
    }

    [TestCase(1455, "00:15")]
    [TestCase(-10, "23:50")]
    [TestCase(600, "10:00")]
    public void TimesAreReadableDailyClockTimes(int minutes, string expected)
        => Assert.That(TimetableChartGeometry.Clock(minutes), Is.EqualTo(expected));
}
