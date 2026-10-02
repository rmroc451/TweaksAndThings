namespace RMROC451.TweaksAndThings;

internal static class FeaturePolicies
{
    internal static bool IsRepairEligible(bool hasRepairWorkOrder, Settings? settings) =>
        hasRepairWorkOrder || (settings?.AllowRepairsWithoutWaybill ?? true);

    internal static bool ShouldShowWaypointSetNotification(Settings? settings) =>
        settings?.ShowWaypointSetNotifications ?? true;
}
