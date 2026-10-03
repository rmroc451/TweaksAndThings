using System.Collections.Generic;

namespace RMROC451.TweaksAndThings;

internal sealed class NpcServiceState
{
    public List<NpcServiceRecord> Services = new List<NpcServiceRecord>();
    public List<NpcPoolUnit> PoolPower = new List<NpcPoolUnit>();
    public Dictionary<string, double> ThroughDepartures = new Dictionary<string, double>();
    public double LastScan = -1;
    public HashSet<string> MissedPickupCars = new HashSet<string>();
    public HashSet<string> ForfeitedPickupCars = new HashSet<string>();
    public Dictionary<string, NpcPickupPayment> ForfeitedPickupBills = new Dictionary<string, NpcPickupPayment>();
    public Dictionary<string, NpcPickupSnapshot> DelinquentPickups = new Dictionary<string, NpcPickupSnapshot>();
    public Dictionary<string, NpcPickupPayment> PickupPayments = new Dictionary<string, NpcPickupPayment>();
    public List<NpcHeldDelivery> HeldDeliveries = new List<NpcHeldDelivery>();
    public Dictionary<string, string> ProtectedTimetables = new Dictionary<string, string>();
    public Dictionary<string, string> AiCrews = new Dictionary<string, string>();
    public List<NpcRandomDeparture> RandomDepartures = new List<NpcRandomDeparture>();
    public int RandomScheduleDay = -1;
    public List<NpcTrafficPlan> TrafficPlans = new List<NpcTrafficPlan>();
    public Dictionary<string, int> BoardMarkerDays = new Dictionary<string, int>();
    public HashSet<string> SkippedTrafficPlans = new HashSet<string>();
    public int NextNpcTrainNumber = 1;
    public int TimetablePlanVersion;
    public List<NpcPulpwoodOrder> PulpwoodOrders = new List<NpcPulpwoodOrder>();
}

internal sealed class NpcRandomDeparture
{
    public string Symbol = string.Empty;
    public string From = string.Empty;
    public string To = string.Empty;
    public int TrainClass;
    public double Scheduled;
    public List<string> FreightDefinitions = new List<string>();
}

// A timetable reservation, not a physical train. No car IDs or crew are allocated
// until dispatch succeeds, so future services never appear in the roster/world.
internal sealed class NpcTrafficPlan
{
    public string Symbol = string.Empty;
    public string ReturnSymbol = string.Empty;
    public string InterchangeId = string.Empty;
    public double Dispatch;
    public double ServiceTime;
    public bool Extra;
    public bool Dispatched;
}

internal sealed class NpcPulpwoodOrder
{
    public string Tag = string.Empty;
    public string Destination = string.Empty;
    public string Interchange = string.Empty;
    public int Remaining;
    public int FeePercent;
    public double UnitValue;
    public int EstimatedFeePerCar;
    public HashSet<string> Fulfilled = new HashSet<string>();
}

internal sealed class NpcHeldDelivery
{
    public string Id = string.Empty;
    public string InterchangeId = string.Empty;
    public string DestinationId = string.Empty;
    public string CarTypeFilter = string.Empty;
    public string LoadId = string.Empty;
    public string Description = string.Empty;
    public string Tag = string.Empty;
    public bool NoPayment;
    public double MissedService;
    public double NextService;
    public double Deadline;
    public double BonusStarts;
    public int PremiumPercent;
    public int BasePayment;
    public string BardoCarId = string.Empty;
    public string CarId = string.Empty;
    public double WaybillCreated;
}

internal sealed class NpcPickupPayment
{
    public double WaybillCreated;
    public string InterchangeId = string.Empty;
    public int Amount;
}

internal sealed class NpcPickupSnapshot
{
    public string DestinationId = string.Empty;
    public string CarId = string.Empty;
    public string CarName = string.Empty;
    public string InterchangeId = string.Empty;
    public List<string> SpanIds = new List<string>();
    public double WaybillCreated;
    public int Payout;
    public bool PayoutWasCredited;
}

internal sealed class NpcPickup
{
    public string CarId = string.Empty;
    public string? Bardo;
    public bool Queued;
    public bool Missed;
}

internal sealed class NpcServiceRecord
{
    public bool PoolOutbound;
    public string Id = string.Empty;
    public string InterchangeId = string.Empty;
    public string TrainSymbol = string.Empty;
    public string ReturnTrainSymbol = string.Empty;
    public string CrewId = string.Empty;
    public bool RandomFreight;
    public string LeadId = string.Empty;
    public string Spawn = string.Empty;
    public string Target = string.Empty;
    public string Phase = "Approaching";
    public string WaitingReason = string.Empty;
    public double Scheduled;
    public double NextTransfer;
    public double TransferSecondsPerCar = NpcServicePolicy.SecondsPerCar;
    public int StopIndex = 1;
    public int SetoutDone;
    public int PickupDone;
    public bool OutboundCollected;
    public bool OrdersPrepared;
    public bool Suspended;
    public bool PickupSnapshotTaken;
    public int PlannedInboundCars;
    public float ReservedDeliveryPounds;
    public bool InboundPlanKnown;
    public List<NpcPickupSnapshot> PickupSnapshot = new List<NpcPickupSnapshot>();
    public HashSet<string> MissesHandled = new HashSet<string>();
    public HashSet<string> Announcements = new HashSet<string>();
    public List<string> PowerIds = new List<string>();
    public List<string> Inbound = new List<string>();
    public List<NpcPickup> Outbound = new List<NpcPickup>();
}

internal sealed class NpcPoolUnit
{
    public string Id = string.Empty;
    public string InterchangeId = string.Empty;
    public string ParkingLocation = string.Empty;
    public string ExitLocation = string.Empty;
    public string AreaId = string.Empty;
    public List<string> PowerIds = new List<string>();
}
