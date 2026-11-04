using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class TimetablePassingForecastTests
{
    [Test]
    public void EarlyPassingTrainDoesNotForecastAWaitAtDillsboro()
    { Assert.That(TimetableForecastPolicy.Departure(320, 0, 330, 0, 0, passing: true), Is.EqualTo(320)); }

    [Test]
    public void PassingForecastStillHonorsAnActualMeetHold()
    { Assert.That(TimetableForecastPolicy.Departure(320, 0, 330, 335, 0, passing: true), Is.EqualTo(335)); }

    [Test]
    public void ServiceStopStillHonorsItsConfiguredTimeAndWork()
    { Assert.That(TimetableForecastPolicy.Departure(320, 5, 330, 0, 0), Is.EqualTo(330)); }
}
