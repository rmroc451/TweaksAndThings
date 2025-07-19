using RMROC451.TweaksAndThings.Patches;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace RMROC451.TweaksAndThings.Extensions;

public static class TextSprite_Extensions
{
    public static string TriColorPiePercent(this float quantity, float capacity)
    {
        int num;
        if (capacity <= 0f)
        {
            num = 0;
        }
        else
        {
            float num2 = Mathf.Clamp01(quantity / capacity);
            int num3 = ((!(num2 < 0.01f)) ? ((!(num2 > 0.99f)) ? (Mathf.FloorToInt(num2 * 15f) + 1) : 16) : 0);
            num = num3;
        }
        string color = "#219106"; //Green
        if (num > 5 && num <= 10)
        {
            color = "#CE8326"; //orange
        } else if (num <= 5)
        {
            color = "#D53427"; //Red
        }

        return $"<sprite tint=1 color={color} name=Pie{num:D2}>";
    }
}
