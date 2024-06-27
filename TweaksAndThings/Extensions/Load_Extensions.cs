using Core;
using System;

namespace RMROC451.TweaksAndThings.Extensions;

public static class Load_Extensions
{
    //https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings?redirectedfrom=MSDN#the--section-separator
    //https://dotnetfiddle.net/iHVevM
    public static string FormatCrewHours(this float quantity, string description)
    {
        var ts = TimeSpan.FromHours(quantity);
        float minutes = ts.Minutes - (ts.Minutes % 15);

        string output = string.Format("{0:;;No}{1:##}{2:\\.##;\\.##;.} {3} {4}", 
            ts.Hours + minutes, 
            ts.Hours, (minutes / 60.0f) * 100, 
            description, 
            "Hour".Pluralize(quantity == 1 ? 1 : 0)
        ).Trim();

        if (ts.Hours < 1)
        {
            output = string.Format("{0} {1} Minutes", ts.Minutes, description).Trim();
        }
        return output;
    }
}
