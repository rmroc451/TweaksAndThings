using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class TimetableChartGeometryTests
{
    [Test]
    public void TrimmedRangeClipsAtScheduledBoundsRatherThanFullDay()
    {
        Assert.That(TimetableChartGeometry.ClipRange(240, 0, 600, 12, 300, 540,
            out var a, out var b, out var c, out var d), Is.True);
        Assert.That(a, Is.EqualTo(300)); Assert.That(c, Is.EqualTo(540));
        Assert.That(b, Is.EqualTo(2)); Assert.That(d, Is.EqualTo(10));
    }
    [Test]
    public void OvernightRangeKeepsTheFollowingMorningVisible()
    {
        Assert.That(TimetableChartGeometry.ClipRange(1430, 0, 1500, 1, 1370, 1560,
            out var a, out _, out var c, out _), Is.True);
        Assert.That(a, Is.EqualTo(1430)); Assert.That(c, Is.EqualTo(1500));
    }
    [Test]
    public void HourBeforeEarlyStartCanExtendIntoPreviousDay()
    {
        Assert.That(TimetableChartGeometry.ClipRange(-30, 0, -30, 1, -45, 120,
            out _, out _, out _, out _), Is.True);
    }
    [TestCase(10, 11, 804.672, 804.672, 10.5)]
    [TestCase(11, 10, 804.672, 804.672, 10.5)]
    [TestCase(10, 11, 804.672, 2414.016, 9.5)]
    [TestCase(46, 48, 782.8032, 3035.8848, 46.3)]
    public void MilepostUsesSurveyedMarkersInEitherDirection(double a, double b, double da, double db, double expected)
    { Assert.That(TimetableChartGeometry.Milepost(a, b, da, db), Is.EqualTo(expected).Within(0.001)); }
    [Test]
    public void DistanceSpacingAssignsTenPercentOfHeightToTenPercentOfTrack()
    {
        var rows = TimetableChartGeometry.DistanceRows(new[] { 100d, 900d }, 0.1);
        Assert.That(rows, Is.EqualTo(new[] { 0d, 10d, 100d }));
        Assert.That(TimetableChartGeometry.RowPosition(rows, 1.5), Is.EqualTo(55));
    }

    [Test]
    public void MilepostGapsProduceProportionalHeights()
    {
        var rows = TimetableChartGeometry.DistanceRows(new[] { 1.5 * 1609.344, 6 * 1609.344 }, 1 / 1609.344);
        Assert.That(rows[1], Is.EqualTo(1.5).Within(0.0001));
        Assert.That(rows[2] - rows[1], Is.EqualTo(6).Within(0.0001));
    }

    [Test]
    public void CoLocatedRowsAndEndpointsKeepTheirExactPositions()
    {
        var rows = TimetableChartGeometry.DistanceRows(new[] { 0d, 100d }, 1);
        Assert.That(rows, Is.EqualTo(new[] { 0d, 0d, 100d }));
        Assert.That(TimetableChartGeometry.RowPosition(rows, -1), Is.Zero);
        Assert.That(TimetableChartGeometry.RowPosition(rows, 10), Is.EqualTo(100));
    }
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
