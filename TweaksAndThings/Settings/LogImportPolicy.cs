using System;

namespace RMROC451.TweaksAndThings;

internal static class LogImportPolicy
{
    internal static int Fee(int normalDeliveryPayout) => (int)Math.Round(Math.Max(0, normalDeliveryPayout) / 2d, MidpointRounding.AwayFromZero);
    internal static bool Due(int deliveryDay, int currentDay, bool settled) => !settled && deliveryDay < currentDay;
    internal static string BillKey(string car, double created, string destination) => car + ":" +
        created.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + destination;
}
