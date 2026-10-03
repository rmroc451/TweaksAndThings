using System;

namespace RMROC451.TweaksAndThings;

public enum InterchangeServiceMode { Automatic, Simulated }
public enum NpcSpawnMode { NearestMapEdge, FarthestReachable }

public sealed class InterchangeApproachOverride
{
    public string InterchangeId = string.Empty;
    public string SpawnLocation = string.Empty;
    public string ServiceLocation = string.Empty;
}

internal static class NpcServicePolicy
{
    internal const int BatchSize = 2;
    internal const double SecondsPerCar = 120;
    internal static bool CanCollectOwnedCar(bool owned, string? tag, bool offMapLoader) =>
        !owned || tag == "sell" || offMapLoader;
    internal static string TravelLabel(string phase, bool nearDestination) =>
        phase == "Approaching" ? (nearDestination ? "Approaching" : "In transit") :
        phase == "Departing" ? (nearDestination ? "Approaching exit" : "In transit (return)") : phase;
    internal static bool HasServiceDemand(int inboundCars, int pickupCars) => inboundCars > 0 || pickupCars > 0;
    internal static int TrimRandomToFit<T>(System.Collections.Generic.List<T> cars,
        Func<System.Collections.Generic.IReadOnlyList<T>, bool> fits, Func<int, int> randomIndex, Action<T> defer)
    {
        int removed = 0;
        while (cars.Count > 0 && !fits(cars))
        {
            int index = randomIndex(cars.Count);
            var car = cars[index];
            cars.RemoveAt(index);
            defer(car);
            removed++;
        }
        return removed;
    }
    internal static double DispatchTime(double serviceTime, double travelSeconds) => serviceTime - travelSeconds - 300;

    internal static double? WarpBoundary(double now, double requestedEnd, System.Collections.Generic.IEnumerable<double> dispatchTimes)
    {
        if (requestedEnd <= now) return null;
        double? earliest = null;
        foreach (double dispatch in dispatchTimes)
        {
            if (double.IsNaN(dispatch) || double.IsInfinity(dispatch) || dispatch > requestedEnd) continue;
            double boundary = Math.Max(now, dispatch);
            if (!earliest.HasValue || boundary < earliest.Value) earliest = boundary;
        }
        return earliest;
    }

    internal static bool Crossed(double previous, double now, double scheduled) =>
        now >= previous && previous < scheduled && scheduled <= now;

    internal static double NextTransfer(double start, int cars, double secondsPerCar = SecondsPerCar) =>
        start + Math.Max(0, cars) * secondsPerCar;

    internal static double TransferRate(bool cabooseNearby) => cabooseNearby ? SecondsPerCar / 2 : SecondsPerCar;

    internal static double RescaleDeadline(double now, double deadline, double oldRate, double newRate) =>
        deadline <= now ? deadline : now + (deadline - now) * newRate / oldRate;

    internal static string NextPhase(string phase, int setoutsRemaining, int pickupsRemaining)
    {
        if (phase == "Setting out" && setoutsRemaining > 0) return "Setting out";
        if (phase == "Picking up" && pickupsRemaining > 0) return "Picking up";
        return setoutsRemaining > 0 ? "Setting out" : pickupsRemaining > 0 ? "Picking up" : "Departing";
    }

    internal static double Horsepower(float forcePounds, float speedMph) =>
        Math.Max(0, forcePounds) * speedMph / 375d;

    internal static double Tonnes(float pounds) => pounds / 2204.62262;

    internal static int RequiredPowerUnits(double freightTonnes, double unitTonnes, double horsepower, double hpPerTonne)
    {
        double available = horsepower - hpPerTonne * unitTonnes;
        if (available <= 0) return 0;
        double required = Math.Ceiling(hpPerTonne * Math.Max(0, freightTonnes) / available);
        return (int)Math.Min(int.MaxValue, Math.Max(1, required));
    }

    internal static double DistanceToPoint(double distanceA, double distanceB, double length, double distance, bool fromA) =>
        Math.Min(distanceA + (fromA ? distance : length - distance),
            distanceB + (fromA ? length - distance : distance));

    internal static int SpeedLimit(int requested, bool safetyFirst, bool requireCaboose, bool firstClass, bool freight, bool hasCaboose, bool npc = false) =>
        !npc && safetyFirst && requireCaboose && !firstClass && freight && !hasCaboose ? Math.Min(requested, 20) : requested;
}
