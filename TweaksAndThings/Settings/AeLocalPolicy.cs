using System;
using System.Collections.Generic;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class AeLocalPolicy
{
    internal static int MoveSpeed(int requested, bool coupling, bool moving) => !moving ? 0 : Math.Min(coupling ? 3 : 35, Math.Max(0, requested));
    internal static bool DeliverToWaybill(bool pickup, bool direct, bool selected) => !pickup || direct || selected;
    internal static int Progress(int completed, int total) => total <= 0 ? 0 : Math.Min(100, (int)(100L * Math.Max(0, completed) / total));
    internal static IEnumerable<int> BinaryCuts(int count)
    {
        while (count > 0) { yield return count; if (count == 1) yield break; count = (count + 1) / 2; }
    }
    internal static List<string> DeferredCars(IEnumerable<string> existing, IEnumerable<string> added, IEnumerable<string> retrieved)
    {
        var remove = retrieved.ToHashSet();
        return existing.Concat(added).Distinct().Where(id => !remove.Contains(id)).ToList();
    }
    // Physical sorting peels a contiguous destination cut from the power end.
    // It never pretends an inaccessible car in the middle is already sorted.
    internal static List<string> LeadingCut(IEnumerable<(string id, string destination)> cars)
    {
        var all = cars.ToList();
        return all.Count == 0 ? new List<string>() : all.TakeWhile(c => c.destination == all[0].destination).Select(c => c.id).ToList();
    }
    internal static bool ReadyPickup(string currentPosition, string destination, bool completed) =>
        !completed && !string.IsNullOrEmpty(currentPosition) && !string.IsNullOrEmpty(destination) && currentPosition != destination;
}

internal sealed class AeLocalTask
{
    public string CarId = string.Empty;
    public string Destination = string.Empty;
    public bool Pickup;
    public bool Collected;
    public bool Done;
    public bool Optional;
    public bool Skipped;
    public bool DirectDelivery;
    public bool Relocation;
}

internal sealed class AeLocalJob
{
    public string Id = Guid.NewGuid().ToString("N");
    public string LeadId = string.Empty;
    public string HomeArea = string.Empty;
    public string HomePoint = string.Empty;
    public string ParkingPoint = string.Empty;
    public string YardPoint = string.Empty;
    public string YardSpan = string.Empty;
    public string Phase = "Setup";
    public string Status = "Select industries, yard return track and final parking waypoint";
    public string Target = string.Empty;
    public string CoupleTo = string.Empty;
    public string AfterMove = string.Empty;
    public string PickingTarget = string.Empty;
    public bool IncludeNearby;
    public int MaxSpeedMph = 35;
    public List<string> IndustryIds = new();
    public List<string> OwnedIds = new();
    public List<string> PowerIds = new();
    public List<string> WorkingCut = new();
    public List<string> YardRemainder = new();
    public List<AeLocalTask> Tasks = new();
    public bool Paused;
    public bool RestoreOrders;
    public double MoveStarted;
    public bool RunaroundAttempted;
    public List<string> MoveIds = new();
    public bool ClearanceReturn;
    public List<string> ClearedObstacles = new();
    public List<string> StageKeepIds = new();
    public List<string> StageDeferredIds = new();
    public string StageSpan = string.Empty;
    public int PlanningPass;
    public string PlanDetail = string.Empty;
    public bool SpotShove;
    public string CrewId = string.Empty;
    public List<string> SwitchListIds = new();
    public Dictionary<string, int> RelocationProgress = new();
    public Dictionary<string, double> DestinationRetry = new();
    public double RetryAt;
    public string NativeStatus = string.Empty;
    public Dictionary<string, double> RejectedSpots = new();
    public Dictionary<string, string> OriginalCrews = new();
    public string PendingObstruction = string.Empty;
}
