using System;

namespace RMROC451.TweaksAndThings;

internal static class FeaturePolicies
{
    internal static bool IsRepairEligible(bool hasRepairWorkOrder, Settings? settings) =>
        hasRepairWorkOrder || (settings?.AllowRepairsWithoutWaybill ?? true);

    internal static bool ShouldShowWaypointSetNotification(Settings? settings) =>
        settings?.ShowWaypointSetNotifications ?? true;

    internal static int CalculateServicingOverdraftFee(int servicingCost, bool canAfford) =>
        servicingCost > 0 && !canAfford ? (int)Math.Ceiling(servicingCost * 0.2d) : 0;

    internal static bool IsWaypointCacheForDifferentLocomotive(string? cachedLocomotiveId, string currentLocomotiveId) =>
        !string.Equals(cachedLocomotiveId, currentLocomotiveId, StringComparison.Ordinal);

    internal static bool IsWaypointDestinationSelectable(bool locationResolved, string? destinationId, string? destinationName) =>
        locationResolved && !string.IsNullOrWhiteSpace(destinationId) && !string.IsNullOrWhiteSpace(destinationName);
}
