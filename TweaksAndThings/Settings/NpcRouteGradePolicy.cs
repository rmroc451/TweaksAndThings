using System;
using System.Collections.Generic;

namespace RMROC451.TweaksAndThings;

internal static class NpcRouteGradePolicy
{
    internal readonly struct Sample
    {
        internal readonly double Length, Grade;
        internal readonly int Speed;
        internal Sample(double length, double grade, int speed) { Length = length; Grade = grade; Speed = speed; }
    }

    internal static Dictionary<int, double> Requirements(IReadOnlyList<Sample> samples, double trainLength)
    {
        var ends = new double[samples.Count + 1];
        var areas = new double[samples.Count + 1];
        for (int i = 0; i < samples.Count; i++)
        { ends[i + 1] = ends[i] + samples[i].Length; areas[i + 1] = areas[i] + samples[i].Length * samples[i].Grade; }
        double window = Math.Min(Math.Max(1, trainLength), ends[samples.Count]);
        var result = new Dictionary<int, double>();
        int left = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            double end = ends[i + 1];
            if (end < window || window <= 0) continue;
            double start = end - window;
            while (left + 1 < ends.Length && ends[left + 1] <= start) left++;
            double startArea = areas[left] + (start - ends[left]) * samples[left].Grade;
            double grade = Math.Max(0, (areas[i + 1] - startArea) / window);
            int speed = samples[i].Speed;
            result[speed] = Math.Max(result.TryGetValue(speed, out double previous) ? previous : 0, grade);
        }
        return result;
    }
}
