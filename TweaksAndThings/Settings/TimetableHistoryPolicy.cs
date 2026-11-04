using System;
using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal sealed class TimetableHistorySample
{
    public double Minute, PlannedMinute, Fraction;
    public string From = "", To = "", Vehicle = "";
    public double Variance => Minute - PlannedMinute;
}

internal static class TimetableHistoryPolicy
{
    internal static bool Append(List<TimetableHistorySample> samples, TimetableHistorySample sample)
    {
        if (double.IsNaN(sample.Minute) || double.IsNaN(sample.PlannedMinute) || double.IsNaN(sample.Fraction)) return false;
        // Rewinding a save must not leave observations from its discarded future.
        samples.RemoveAll(s => s.Minute > sample.Minute);
        if (samples.Count > 0 && sample.Minute - samples[samples.Count - 1].Minute < 1) return false;
        sample.Fraction = Math.Max(0, Math.Min(1, sample.Fraction));
        samples.Add(sample);
        double earliest = Math.Floor(sample.Minute / 1440) * 1440 - 1440;
        samples.RemoveAll(s => s.Minute < earliest);
        if (samples.Count > 2880) samples.RemoveRange(0, samples.Count - 2880);
        return true;
    }
    internal static bool Connect(TimetableHistorySample a, TimetableHistorySample b) =>
        b.Minute > a.Minute && b.Minute - a.Minute <= 3 && a.Vehicle == b.Vehicle;
    internal static double? Row(TimetableHistorySample sample, IReadOnlyList<string> stations)
    {
        int from = -1, to = -1;
        for (int i = 0; i < stations.Count; i++) {
            if (stations[i] == sample.From) from = i;
            if (stations[i] == sample.To) to = i;
        }
        if (from < 0 || to < 0) return null;
        return from + (to - from) * sample.Fraction;
    }
    internal static string VarianceText(double minutes) => Math.Abs(minutes) < 0.5 ? "on time" :
        $"{Math.Abs(minutes):N0} min " + (minutes > 0 ? "late" : "early");
}
