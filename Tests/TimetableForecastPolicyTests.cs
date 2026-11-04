using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class TimetableForecastPolicyTests
{
    [Test]
    public void MovingProjectionUsesCurrentSpeedAndFutureTrackLimits()
    {
        Assert.That(TimetableForecastPolicy.TravelSeconds(1609.344, 30, 15, false), Is.EqualTo(240).Within(0.01));
        Assert.That(TimetableForecastPolicy.TravelSeconds(1609.344, 15, 30, false), Is.EqualTo(240).Within(0.01));
    }
    [Test]
    public void UnknownStopDoesNotInventADeparture()
    {
        Assert.That(TimetableForecastPolicy.TravelSeconds(1000, 0, 30, false), Is.Null);
        Assert.That(TimetableForecastPolicy.TravelSeconds(1000, 0, 30, true), Is.GreaterThan(0));
    }
    [Test]
    public void ForecastPreservesDwellMeetAndWorkingTimeWhenLate()
    {
        Assert.That(TimetableForecastPolicy.Departure(1000, 120, 1050, 1100, 0), Is.EqualTo(1120));
        Assert.That(TimetableForecastPolicy.Departure(1000, 120, 1050, 1300, 1250), Is.EqualTo(1300));
        Assert.That(TimetableForecastPolicy.Departure(1000, 120, 1050, 1300, 1400), Is.EqualTo(1400));
    }
    [Test]
    public void ForecastClockMatchesTheNearbyDayAcrossMidnight()
    {
        Assert.That(TimetableForecastPolicy.ClockNear(5, 1430), Is.EqualTo(1445));
        Assert.That(TimetableForecastPolicy.ClockNear(1430, 1445), Is.EqualTo(1430));
        Assert.That(TimetableForecastPolicy.ClockNear(600, 2100), Is.EqualTo(2040));
    }
}
