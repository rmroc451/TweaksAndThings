using System;
using System.Collections.Generic;

namespace RMROC451.TweaksAndThings;

internal static class NpcIdentityPolicy
{
    internal static string? LocomotiveNumber(ISet<string> occupied, int start)
    {
        for (int century = 1800; century <= 1800 + occupied.Count + 100; century += 100)
        for (int i = 0; i < 100; i++)
        {
            string number = (century + ((start % 100 + 100) % 100 + i) % 100).ToString();
            if (occupied.Add(number)) return number;
        }
        return null;
    }
}
