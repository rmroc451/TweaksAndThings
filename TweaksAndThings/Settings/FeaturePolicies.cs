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

    internal static bool ShouldAcceptWaypointPickerHit(bool mouseClicked, bool escapePressed, bool locationChanged) =>
        mouseClicked && !escapePressed && locationChanged;

    internal static bool CanSetWaypointFromMap(bool routeFound, bool safetyFirstApplies) =>
        routeFound && !safetyFirstApplies;

    internal static bool ShouldSafetyFirstGovern(bool safetyFirstEnabled, bool hasNonMotiveCars, bool expressTrain,
        bool allCarsFreight, bool hasCaboose) =>
        safetyFirstEnabled && hasNonMotiveCars && !expressTrain && allCarsFreight && !hasCaboose;

    internal static bool ShouldRunWaypointRefresh(bool waypointMode, bool hasLocomotive) => waypointMode && hasLocomotive;

    internal static bool CanBeginRunaround(bool locomotiveStopped, bool oneMotiveBoundary, bool oneTailCoupler,
        bool destinationResolved) =>
        locomotiveStopped && oneMotiveBoundary && oneTailCoupler && destinationResolved;

    internal static bool ShouldCompleteRunaround(bool runaroundPending, bool tailCarRecoupled) =>
        runaroundPending && tailCarRecoupled;
}
