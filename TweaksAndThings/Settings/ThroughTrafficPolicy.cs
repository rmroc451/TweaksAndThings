using System;

namespace RMROC451.TweaksAndThings;

/// <summary>
/// Pure rules used by the through-traffic scheduler. Keeping these calculations
/// independent of Unity and the game model makes the schedule and consist rules
/// testable without loading the game.
/// </summary>
internal static class ThroughTrafficPolicy
{
    internal const string DefaultTrainSymbolPrefix = "Z-";
    internal const int DefaultOnTimeGraceMinutes = 5;

    internal static bool IsMarkedTrain(string? trainSymbol, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(trainSymbol) || string.IsNullOrWhiteSpace(prefix)) return false;
        return trainSymbol.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsEligibleTrain(string? trainSymbol, string? prefix, bool isFirstClass, bool originHasSignals, bool destinationHasSignals) =>
        IsMarkedTrain(trainSymbol, prefix) && isFirstClass && originHasSignals && destinationHasSignals;

    internal static bool IsDepartureDue(int nowMinuteOfDay, int scheduledMinuteOfDay, int graceMinutes = 0)
    {
        if (graceMinutes < 0) throw new ArgumentOutOfRangeException(nameof(graceMinutes));
        if (nowMinuteOfDay < 0 || nowMinuteOfDay >= 1440 || scheduledMinuteOfDay < 0 || scheduledMinuteOfDay >= 1440) return false;
        int elapsed = ((nowMinuteOfDay - scheduledMinuteOfDay) % 1440 + 1440) % 1440;
        return elapsed <= graceMinutes;
    }

    internal static int PassengerCarsForDemand(int passengerDemand, int passengerCapacityPerCar)
    {
        if (passengerDemand <= 0 || passengerCapacityPerCar <= 0) return 0;
        return (int)Math.Ceiling((double)passengerDemand / passengerCapacityPerCar);
    }

    internal static double HorsepowerPerTonneForGrade(double gradePercent, double speedMph, double drivetrainEfficiency = 0.8)
    {
        if (gradePercent <= 0 || speedMph <= 0) return 0;
        if (drivetrainEfficiency <= 0 || drivetrainEfficiency > 1) throw new ArgumentOutOfRangeException(nameof(drivetrainEfficiency));

        // Grade resistance is approximately 20 lb per US ton for each 1% grade.
        // Horsepower is force (lb) × speed (mph) / 375.
        return (22.046d * gradePercent * speedMph / 375d) / drivetrainEfficiency;
    }

    internal static bool IsWithinGracePeriod(int scheduledArrivalMinutes, int actualArrivalMinutes, int graceMinutes)
    {
        if (graceMinutes < 0) throw new ArgumentOutOfRangeException(nameof(graceMinutes));

        // Timetable minutes are in a daily clock, so normalize the difference to
        // the nearest day-boundary crossing (e.g. 23:58 to 00:02 is four minutes).
        int minutesLate = ((actualArrivalMinutes - scheduledArrivalMinutes) % 1440 + 1440) % 1440;
        if (minutesLate > 720) minutesLate -= 1440;
        return Math.Abs(minutesLate) <= graceMinutes;
    }

    internal static int PassengerCarSettlement(int passengerCount, bool onTime, int dollarsPerPassenger)
    {
        if (passengerCount <= 0 || dollarsPerPassenger <= 0) return 0;
        int amount = checked(passengerCount * dollarsPerPassenger);
        return onTime ? amount : -amount;
    }
}
