using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.State;
using KeyValue.Runtime;
using Model;
using Model.Ops;
using RMROC451.TweaksAndThings.Extensions;
using UnityEngine;
using Track;
using Track.Search;

namespace RMROC451.TweaksAndThings;

/// <summary>Owns each interchange independently; the native backend remains the source of cars and waybills.</summary>
internal static class SimulatedInterchangeService
{
    [ThreadStatic] internal static NpcServiceRecord? Loading;
    [ThreadStatic] internal static NpcServiceRecord? Collecting;
    [ThreadStatic] internal static string? CollectingCarId;
    private static float scanRemainder;
    private static readonly Dictionary<string, float> nextDispatchAttempt = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> nextLoadAttempt = new Dictionary<string, float>();

    internal static void Tick(Settings settings, float deltaTime)
    {
        if (!StateManager.IsHost || TrainController.Shared == null || OpsController.Shared == null || !NpcServiceStore.Load())
        {
            NpcTrafficDiagnostics.Report("scheduler", "Waiting for host, loaded railroad and persistent service state");
            return;
        }
        NpcTrafficDiagnostics.Report("scheduler", "Running; interchange mode=" + settings.InterchangeService);
        scanRemainder += deltaTime;
        if (scanRemainder < 0.5f) return;
        scanRemainder = 0;
        NpcHeldDeliveryTracking.EnsureOrdersAfterLoad();
        double now = TimeWeather.Now.TotalSeconds;
        foreach (var service in NpcServiceStore.State.Services.Where(s => s.InterchangeId.Length > 0).ToList())
        {
            try { Advance(service, now); NpcTrafficDiagnostics.Report(service.InterchangeId, $"{service.Phase}; setout {service.SetoutDone}/{service.Inbound.Count}, pickup {service.PickupDone}/{service.Outbound.Count}; {service.WaitingReason}"); }
            catch (Exception ex)
            {
                service.WaitingReason = "Service interrupted; retrying safely";
                TweaksAndThingsPlugin.LogException("Interchange service " + service.InterchangeId, ex);
            }
        }
        if (settings.InterchangeService == InterchangeServiceMode.Simulated)
        {
            foreach (var interchange in OpsController.Shared.EnabledInterchanges)
            {
                if (HasInboundService(interchange)) continue;
                if (nextDispatchAttempt.TryGetValue(interchange.Identifier, out float retry) && Time.realtimeSinceStartup < retry) continue;
                try
                {
                    var due = interchange.GetNextServiceTime(TimeWeather.Now, out _);
                    if (!NpcTrainOperations.TryApproach(interchange, settings, out var spawn, out var target, out var travelSeconds))
                    { NpcTrafficDiagnostics.Report(interchange.Identifier, NpcTrainOperations.ApproachStatus(interchange.Identifier)); continue; }
                    if (now < PlannedDispatch(interchange, due.TotalSeconds, travelSeconds))
                        NpcTrafficDiagnostics.Report(interchange.Identifier, $"Service due {due}; dispatch {new GameDateTime((float)PlannedDispatch(interchange, due.TotalSeconds, travelSeconds))}; travel {travelSeconds / 60:N1} minutes; waiting for dispatch time");
                    if (now >= PlannedDispatch(interchange, due.TotalSeconds, travelSeconds))
                    {
                        // Failed power/placement checks must not repeat expensive route sizing every half-second.
                        nextDispatchAttempt[interchange.Identifier] = Time.realtimeSinceStartup + 10f;
                        Dispatch(interchange, spawn, target, due);
                    }
                }
                catch (Exception ex) { TweaksAndThingsPlugin.LogException("Interchange dispatch " + interchange.Identifier, ex); }
            }
        }
        NpcServiceStore.Save();
    }

    internal static bool Owns(Interchange interchange) => NpcServiceStore.Load() &&
        NpcServiceStore.State.Services.Any(s => s.InterchangeId == interchange.Identifier);

    internal static bool HasInboundService(Interchange interchange) => NpcServiceStore.Load() && NpcServiceStore.State.Services.Any(s => s.InterchangeId == interchange.Identifier && !s.PoolOutbound);

    private static double PlannedDispatch(Interchange interchange, double due, double travel) =>
        NpcTrafficPlanPolicy.ForInterchange(NpcServiceStore.State, interchange.Identifier, due)?.Dispatch ?? NpcServicePolicy.DispatchTime(due, travel);

    internal static bool PrepareWarp(Settings settings)
    {
        bool pending = false;
        foreach (var interchange in OpsController.Shared.EnabledInterchanges)
        {
            if (HasInboundService(interchange)) continue;
            NpcTrainOperations.TryApproach(interchange, settings, out _, out _, out _);
            pending |= NpcTrainOperations.IsApproachPlanning(interchange.Identifier);
        }
        return !pending;
    }

    internal static double? WarpBoundary(Settings settings, double now, double end)
    {
        var next = NextNeededDispatch(settings, now, refreshDemand: true);
        return next.HasValue && next.Value <= end ? next : null;
    }

    internal static double? NextNeededDispatch(Settings settings, double now, bool refreshDemand = false)
    {
        var times = new List<double>();
        if (refreshDemand)
        {
            foreach (var interchange in OpsController.Shared.EnabledInterchanges.Where(i => !HasInboundService(i))) interchange.PrepareToService();
            OpsController.Shared.RequestIndustriesOrderCars();
        }
        foreach (var interchange in OpsController.Shared.EnabledInterchanges)
        {
            if (HasInboundService(interchange) || !NpcTrainOperations.TryApproach(interchange, settings, out _, out _, out var travel)) continue;
            if (!NpcServicePolicy.HasServiceDemand(interchange.Orders.Sum(o => o.CarCount), PickupCars(interchange).Count())) continue;
            var due = interchange.GetNextServiceTime(new GameDateTime((float)now), out _);
            times.Add(PlannedDispatch(interchange, due.TotalSeconds, travel));
        }
        return NpcServicePolicy.WarpBoundary(now, now + 7 * 86400, times);
    }

    internal static void DispatchAtWarpBoundary(Settings settings)
    {
        foreach (var interchange in OpsController.Shared.EnabledInterchanges)
        {
            if (HasInboundService(interchange) || !NpcTrainOperations.TryApproach(interchange, settings, out var spawn, out var target, out var travel)) continue;
            var due = interchange.GetNextServiceTime(TimeWeather.Now, out _);
            // Native clock storage rounds to float hours; allow one second at the boundary.
            if (PlannedDispatch(interchange, due.TotalSeconds, travel) > TimeWeather.Now.TotalSeconds + 1) continue;
            try { Dispatch(interchange, spawn, target, due); }
            catch (Exception ex) { TweaksAndThingsPlugin.LogException("Timewarp interchange dispatch " + interchange.Identifier, ex); }
        }
    }

    private static void Dispatch(Interchange interchange, Location spawn, Location target, GameDateTime due)
    {
        if (!NpcTrainOperations.Route(spawn, target, out var route, out _)) { NpcTrafficDiagnostics.Report(interchange.Identifier, "Dispatch blocked: route unavailable"); return; }
        interchange.PrepareToService();
        OpsController.Shared.RequestIndustriesOrderCars();
        var inbound = NpcInboundPlan.Build(interchange, due);
        var pickups = PickupCars(interchange).ToList();
        int pooledPickups = NpcPoolPower.Dispatch(interchange, pickups, TimeWeather.Now.TotalSeconds);
        if (!NpcServicePolicy.HasServiceDemand(inbound.Count, pickups.Count))
        {
            if (pooledPickups > 0 || interchange.Orders.Any(o => o.CarCount > 0))
            {
                NpcHeldDeliveryTracking.CaptureRemaining(interchange, due, inbound);
                interchange.LastServiced = due;
                if (interchange.TryGetExtraScheduled(out var deferredExtra) && deferredExtra <= due) interchange.ScheduleExtra(null);
            }
            NpcTrafficDiagnostics.Report(interchange.Identifier, pooledPickups > 0 ? $"Outbound pool service assigned {pooledPickups} cars; no new inbound train required" :
                interchange.Orders.Any(o => o.CarCount > 0) ? "Service deferred: destination yard full; no pickup cars" : "Service skipped: no inbound orders or pickup cars");
            NpcDailyTrafficPlans.Skip(interchange, due.TotalSeconds);
            return;
        }
        float outboundWeight = pickups.Sum(c => c.Weight);
        float reserve = Math.Max(inbound.Weight, outboundWeight);
        NpcTrafficDiagnostics.Report(interchange.Identifier, $"Power sizing: {inbound.Count} inbound cars, {inbound.Weight / 2000:N0} inbound tons, {outboundWeight / 2000:N0} pickup tons; planning for {reserve / 2000:N0} tons");
        if (!NpcTrainOperations.Route(target, spawn, out var returnRoute, out _)) { NpcTrafficDiagnostics.Report(interchange.Identifier, "Dispatch blocked: return route unavailable"); return; }
        if (!NpcTrainOperations.TryCatalogPower(inbound.Weight, route, null, out var descriptors, returnRoute, outboundWeight,
            trainLength: NpcPassingSidings.Length(inbound.Descriptors) + 60, returnTrainLength: NpcPassingSidings.Length(pickups.Select(c => c.Descriptor())) + 60))
        { NpcTrafficDiagnostics.Report(interchange.Identifier, $"Dispatch blocked: no suitable catalog power for delivery {inbound.Weight / 2000:N0} tons and pickup {outboundWeight / 2000:N0} tons"); return; }
        if (!FitPassingSidings(interchange, inbound, pickups, route, returnRoute, ref descriptors)) return;
        outboundWeight = pickups.Sum(c => c.Weight);
        float fullLength = TrainController.ApproximateLength(descriptors.Concat(inbound.Descriptors));
        if (!NpcDeparturePlacement.Find(ref spawn, target, fullLength))
        { NpcTrafficDiagnostics.Report(interchange.Identifier, "Dispatch blocked: neither departure end has room for the complete train"); return; }
        // Relocating inward can change the route and its grades. Size power again for the actual departure point.
        if (!NpcTrainOperations.Route(spawn, target, out route, out _) || !NpcTrainOperations.Route(target, spawn, out returnRoute, out _)) return;
        if (!FitPassingSidings(interchange, inbound, pickups, route, returnRoute, ref descriptors)) return;
        outboundWeight = pickups.Sum(c => c.Weight);
        if (!NpcDeparturePlacement.Find(ref spawn, target, TrainController.ApproximateLength(descriptors.Concat(inbound.Descriptors)))) return;
        if (!NpcTrainOperations.Route(spawn, target, out route, out _) || !NpcTrainOperations.Route(target, spawn, out returnRoute, out _) ||
            !FitPassingSidings(interchange, inbound, pickups, route, returnRoute, ref descriptors)) return;
        outboundWeight = pickups.Sum(c => c.Weight);
        inbound.TrimForSpawn(interchange, spawn, descriptors);
        if (!NpcServicePolicy.HasServiceDemand(inbound.Count, pickups.Count))
        {
            NpcHeldDeliveryTracking.CaptureRemaining(interchange, due, inbound);
            interchange.LastServiced = due;
            if (interchange.TryGetExtraScheduled(out var deferredExtra) && deferredExtra <= due) interchange.ScheduleExtra(null);
            NpcTrafficDiagnostics.Report(interchange.Identifier, "Service deferred: no deliveries fit destination/spawn and no pickups");
            NpcDailyTrafficPlans.Skip(interchange, due.TotalSeconds); return;
        }
        reserve = Math.Max(inbound.Weight, outboundWeight);
        var ids = descriptors.Select(_ => Guid.NewGuid().ToString("N")).ToList();
        if (!NpcTrainOperations.CanPlaceAt(spawn, TrainController.ApproximateLength(descriptors))) { NpcTrafficDiagnostics.Report(interchange.Identifier, "Dispatch blocked: insufficient free track at spawn"); return; }
        if (!NpcTrafficRouting.MaySpawn(spawn, target, TrainController.ApproximateLength(descriptors.Concat(inbound.Descriptors)) + 20)) { NpcTrafficDiagnostics.Report(interchange.Identifier, "Dispatch deferred: staging or route to next passing siding unavailable"); return; }
        var service = new NpcServiceRecord
        {
            Id = Guid.NewGuid().ToString("N"), InterchangeId = interchange.Identifier, LeadId = ids[0],
            PowerIds = ids, Spawn = Graph.Shared.LocationToString(spawn), Target = Graph.Shared.LocationToString(target),
            Scheduled = due.TotalSeconds, Phase = "Loading", OrdersPrepared = true,
            PlannedInboundCars = inbound.Count, InboundPlanKnown = true, ReservedDeliveryPounds = inbound.Weight
        };
        NpcTrainOperations.PlacePower(spawn, descriptors, ids);
        if (!TrainController.Shared.TryGetCarForId(service.LeadId, out var lead) || !(lead is BaseLocomotive)) return;
        RMROC451.TweaksAndThings.Patches.InterchangeTimewarp_Patch.CancelForSpawn(interchange.DisplayName);
        var plan = NpcDailyTrafficPlans.Claim(interchange, due.TotalSeconds);
        if (plan != null) { service.TrainSymbol = plan.Symbol; service.ReturnTrainSymbol = plan.ReturnSymbol; }
        NpcServiceStore.State.Services.Add(service);
        NpcTimetableRegistry.RegisterInterchange(service);
        NpcPickupTracking.Snapshot(service, interchange, pickups);
        TweaksAndThingsPlugin.LogDiagnostic($"Dispatched NPC service {service.Id} for {interchange.DisplayName}; due {due}, {descriptors.Count} power vehicles, {inbound.Count} planned setouts, {pickups.Count} pickups, {inbound.Deferred} cars deferred; reserve {reserve / 2000:N0} tons.");
        // Persist ownership before invoking the native operations, so normal service cannot race this service.
        NpcServiceStore.Save();
        NpcInboundPlan.Active = inbound;
        try { InitializeInbound(service, interchange, target); }
        finally { NpcInboundPlan.Active = null; }
        NpcServiceStore.Save();
    }

    private static bool FitPassingSidings(Interchange interchange, NpcInboundPlan inbound, List<Car> pickups,
        List<RouteSearch.Step> route, List<RouteSearch.Step> returnRoute, ref List<CarDescriptor> power)
    {
        float limit = Math.Min(NpcPassingSidings.Limit(route), NpcPassingSidings.Limit(returnRoute));
        if (limit <= 0)
        { NpcTrafficDiagnostics.Report(interchange.Identifier, "Dispatch blocked: no usable passing siding on the service route"); return false; }
        int originalInbound = inbound.Count, originalPickups = pickups.Count;
        while (true)
        {
            inbound.TrimToLength(interchange, limit, power);
            while (pickups.Count > 0 && NpcPassingSidings.Length(power.Concat(pickups.Select(c => c.Descriptor()))) > limit)
                pickups.RemoveAt(UnityEngine.Random.Range(0, pickups.Count));
            if (!NpcTrainOperations.TryCatalogPower(inbound.Weight, route, null, out power, returnRoute, pickups.Sum(c => c.Weight),
                trainLength: NpcPassingSidings.Length(inbound.Descriptors) + 60,
                returnTrainLength: NpcPassingSidings.Length(pickups.Select(c => c.Descriptor())) + 60)) return false;
            if (NpcPassingSidings.Length(power) > limit && inbound.Count == 0 && pickups.Count == 0)
            { NpcTrafficDiagnostics.Report(interchange.Identifier, "Dispatch blocked: locomotive power alone exceeds passing siding length"); return false; }
            if (NpcPassingSidings.Length(power.Concat(inbound.Descriptors)) <= limit &&
                NpcPassingSidings.Length(power.Concat(pickups.Select(c => c.Descriptor()))) <= limit) break;
        }
        ModDiagnosticLog.Write("SIDING", $"{interchange.Identifier}: usable passing siding {limit:N0} m; delivery train {NpcPassingSidings.Length(power.Concat(inbound.Descriptors)):N0} m; pickup train {NpcPassingSidings.Length(power.Concat(pickups.Select(c => c.Descriptor()))):N0} m; deliveries deferred={originalInbound - inbound.Count}; pickups left for later={originalPickups - pickups.Count}.");
        return true;
    }

    internal static IEnumerable<TrackSpan> PickupSpans(Interchange interchange, Car car)
    {
        if (!car.Waybill.HasValue) return Enumerable.Empty<TrackSpan>();
        var wb = car.Waybill.Value;
        var loader = interchange.Loaders.FirstOrDefault(l => wb.Destination.Identifier == l.Identifier &&
            !l.ProgressionDisabled && new OpsCarAdapter(car, OpsController.Shared).IsEmptyOrContains(l.load));
        if (loader != null && NpcServicePolicy.CanCollectOwnedCar(car.IsOwnedByPlayer, wb.Tag, true)) return loader.TrackSpans;
        return wb.Destination.Identifier == interchange.Identifier && NpcServicePolicy.CanCollectOwnedCar(car.IsOwnedByPlayer, wb.Tag, false)
            ? interchange.TrackSpans : Enumerable.Empty<TrackSpan>();
    }

    internal static IEnumerable<Car> PickupCars(Interchange interchange) => TrainController.Shared.Cars.Where(c =>
        !c.IsInBardo && !c.IsLocomotive && !ThroughTrafficGuard.IsGenerated(c) &&
        !NpcServiceStore.State.Services.Any(s => s.PoolOutbound && s.Outbound.Any(p => p.CarId == c.id && !p.Missed)) &&
        PickupSpans(interchange, c).Any(s => s.Contains(c.OpsLocation)));

    private static void InitializeInbound(NpcServiceRecord service, Interchange interchange, Location target)
    {
        if (nextLoadAttempt.TryGetValue(service.Id, out var retry) && Time.realtimeSinceStartup < retry) return;
        int previousInbound = service.Inbound.Count;
        var activePlan = NpcInboundPlan.Active;
        try
        {
            Loading = service;
            if (!service.OrdersPrepared)
            {
                interchange.PrepareToService();
                OpsController.Shared.RequestIndustriesOrderCars();
                service.OrdersPrepared = true;
            }
            if (NpcInboundPlan.Active == null)
            {
                NpcInboundPlan.Active = NpcInboundPlan.Build(interchange, new GameDateTime((float)service.Scheduled));
                if (service.ReservedDeliveryPounds <= 0 && NpcInboundPlan.Active.Count > 0)
                {
                    var installed = service.PowerIds.Select(id => TrainController.Shared.CarForId(id)).Where(c => c != null).ToList();
                    if (NpcTrainOperations.Route(Graph.Shared.ResolveLocationString(service.Spawn), target, out var route, out _))
                        NpcTrainOperations.TryCatalogPower(0, route, null, out _, installedPower: installed, reportCapacity: pounds => service.ReservedDeliveryPounds = pounds);
                    if (!service.InboundPlanKnown) service.PlannedInboundCars = NpcInboundPlan.Active.Count;
                    service.InboundPlanKnown = true;
                }
                NpcInboundPlan.Active.TrimToReservation(interchange, service.PlannedInboundCars, service.ReservedDeliveryPounds);
                var reservedPower = service.PowerIds.Select(id => TrainController.Shared.CarForId(id)).Where(c => c != null).Select(c => c.Descriptor()).ToList();
                if (NpcTrainOperations.Route(Graph.Shared.ResolveLocationString(service.Spawn), target, out var reservedRoute, out _))
                    NpcInboundPlan.Active.TrimToLength(interchange, NpcPassingSidings.Limit(reservedRoute), reservedPower);
                // Recover older loading services stranded at a short terminal without sacrificing their reserved freight.
                if (service.Inbound.Count == 0 && NpcInboundPlan.Active.Count > 0)
                {
                    var power = service.PowerIds.Select(id => TrainController.Shared.CarForId(id)).ToList();
                    var position = Graph.Shared.ResolveLocationString(service.Spawn);
                    var old = position;
                    if (NpcDeparturePlacement.Find(ref position, target, TrainController.ApproximateLength(power.Select(c => c.Descriptor()).Concat(NpcInboundPlan.Active.Descriptors)),
                        new HashSet<string>(service.PowerIds)) && Graph.Shared.LocationToString(position) != Graph.Shared.LocationToString(old))
                    {
                        NpcDeparturePlacement.MoveExisting(position, power);
                        service.Spawn = Graph.Shared.LocationToString(position);
                    }
                }
            }
            NpcTrafficDiagnostics.Detail("load:" + service.Id,
                $"Loading service={service.Id}, interchange={interchange.Identifier}, planned={service.PlannedInboundCars}, selected now={NpcInboundPlan.Active.Count}, attached={service.Inbound.Count}, pending native orders={interchange.Orders.Sum(o => o.CarCount)}, lead={service.LeadId}, spawn={service.Spawn}");
            // Native service creates waybills, shuffles using blocking difficulty and consumes only fitted orders.
            // The context excludes outbound cars until arrival, and the placement hook loads the NPC instead.
            interchange.ServeInterchange(new NpcInterchangeContext(interchange.CreateContext(new GameDateTime((float)service.Scheduled), 0f), true));
            if (service.Inbound.Count > 0 || service.PlannedInboundCars == 0)
                NpcHeldDeliveryTracking.CaptureRemaining(interchange, new GameDateTime((float)service.Scheduled), NpcInboundPlan.Active);
            if (interchange.TryGetExtraScheduled(out var extra) && extra.TotalSeconds <= service.Scheduled) interchange.ScheduleExtra(null);
        }
        finally { Loading = null; NpcInboundPlan.Active = activePlan; }
        if (service.Inbound.Count == 0 && service.PlannedInboundCars > 0 && interchange.Orders.Any(o => o.CarCount > 0))
        {
            // A failed append must not send a bare engine out as a successful delivery.
            service.WaitingReason = "Waiting for room to attach ordered inbound cars at spawn";
            nextLoadAttempt[service.Id] = Time.realtimeSinceStartup + 10f;
            return;
        }
        nextLoadAttempt.Remove(service.Id);
        if (service.Inbound.Count == 0 && service.PickupSnapshot.Count == 0)
        {
            // Recover older saves that contain a loading locomotive with no viable work.
            NpcTrainOperations.Trusted(() =>
            {
                foreach (var id in service.PowerIds)
                    if (TrainController.Shared.TryGetCarForId(id, out _)) TrainController.Shared.RemoveCarSmart(id);
            });
            NpcServiceStore.State.Services.Remove(service);
            NpcTimetableRegistry.Remove(service);
            TweaksAndThingsPlugin.LogDiagnostic($"Canceled empty NPC service {service.Id}: no attached deliveries or reserved pickups; deferred orders retained.");
            return;
        }
        TweaksAndThingsPlugin.LogDiagnostic($"NPC {interchange.DisplayName}: attached {service.Inbound.Count - previousInbound} inbound cars ({service.Inbound.Count} total); {interchange.Orders.Sum(o => o.CarCount)} orders remaining.");
        NpcTrainOperations.Order((BaseLocomotive)TrainController.Shared.CarForId(service.LeadId), target);
        service.Phase = "Approaching";
        NpcTrafficMessages.Send(service, "inbound", $"On the move toward {interchange.DisplayName} for interchange service.");
    }

    internal static bool LoadInbound(List<CarDescriptor> descriptors, List<string> carIds, ref bool result)
    {
        var service = Loading;
        if (service == null) return true;
        var ids = carIds.Select(id => id ?? Guid.NewGuid().ToString("N")).ToList();
        var marked = descriptors.Select(d =>
        {
            var properties = new Dictionary<string, Value>(d.Properties) { [ThroughTrafficGuard.MarkerKey] = Value.Bool(true) };
            return new CarDescriptor(d.DefinitionInfo, d.Ident, d.Bardo, service.CrewId, d.Flipped, properties);
        }).ToList();
        result = NpcTrainOperations.Append(service, marked, ids);
        if (result) service.Inbound.AddRange(ids);
        if (result) foreach (var id in ids)
        {
            NpcHeldDeliveryTracking.RegisterCar(TrainController.Shared.CarForId(id), service.Scheduled, loadingRetry: true);
        }
        if (result)
        {
            var interchange = OpsController.Shared.AllInterchanges.First(i => i.Identifier == service.InterchangeId);
            NpcPulpwoodOrdering.Restore(interchange);
        }
        NpcTrafficDiagnostics.Detail("attach:" + service.Id + ":" + descriptors.Count,
            $"Append service={service.Id}, requested={descriptors.Count}, success={result}, total attached={service.Inbound.Count}; cars={string.Join(",", ids)}");
        return false;
    }

    internal static bool QueuePickup(string carId, string? bardo = null)
    {
        if (Collecting == null) return false;
        if (CollectingCarId != carId) return true;
        var pickup = Collecting.Outbound.FirstOrDefault(c => c.CarId == carId);
        if (pickup != null)
        {
            pickup.Queued = true;
            pickup.Bardo = bardo;
            NpcServiceStore.Save();
        }
        return true;
    }

    private static void Advance(NpcServiceRecord service, double now)
    {
        var interchange = OpsController.Shared.AllInterchanges.FirstOrDefault(i => i.Identifier == service.InterchangeId);
        if (interchange == null) { service.WaitingReason = "Interchange unavailable"; return; }
        if (!TrainController.Shared.TryGetCarForId(service.LeadId, out var car) || !(car is BaseLocomotive lead))
        {
            service.WaitingReason = "Service locomotive missing; manual recovery required";
            return;
        }
        var spawn = Graph.Shared.ResolveLocationString(service.Spawn);
        var target = Graph.Shared.ResolveLocationString(service.Target);
        if (service.OutboundCollected && !service.PickupSnapshotTaken)
        {
            // Older saves had already run the native collection backend at arrival.
            // Do not charge retrospective missed-pickup penalties or collect those cars twice.
            foreach (var pickup in service.Outbound) pickup.Queued = true;
            service.PickupSnapshotTaken = true;
        }
        if (service.Suspended)
        {
            service.Suspended = false;
            if (service.Phase == "Approaching" || service.Phase == "Departing")
            {
                if (!NpcDeparturePlacement.FaceDeparture(service, service.Phase == "Departing" ? spawn : target))
                { service.Suspended = true; service.WaitingReason = "Clear track to turn NPC power toward its destination"; return; }
                NpcTrainOperations.Order(lead, service.Phase == "Departing" ? spawn : target);
            }
            else service.NextTransfer = Deadline(service, now, PendingBatch(service));
        }
        if (service.Phase == "Loading") { InitializeInbound(service, interchange, target); return; }
        service.WaitingReason = string.Empty;
        if (service.Phase == "Approaching")
        {
            NpcTrafficMessages.Approaching(service, lead, target, interchange.DisplayName, "approach-inbound");
            if (!NpcTrainOperations.Arrived(lead, target)) return;
            if (now < service.Scheduled) { service.WaitingReason = "Waiting for configured service time"; return; }
            NpcTrainOperations.Order(lead, target, false);
            if (!service.OutboundCollected)
            {
                if (!service.PickupSnapshotTaken) NpcPickupTracking.Snapshot(service, interchange, PickupCars(interchange).ToList());
                AddExtraPickups(service, interchange, target, spawn);
                service.Outbound = service.PickupSnapshot.Select(p => new NpcPickup { CarId = p.CarId }).ToList();
                service.OutboundCollected = true;
            }
            service.Phase = "Setting out";
            TrimArrival(service, interchange);
            service.TransferSecondsPerCar = NpcServicePolicy.TransferRate(CabooseNearby(interchange));
            service.NextTransfer = Deadline(service, now, PendingBatch(service));
        }
        if (service.Phase == "Picking up" || service.Phase == "Setting out")
        {
            double rate = NpcServicePolicy.TransferRate(CabooseNearby(interchange));
            if (rate != service.TransferSecondsPerCar)
            {
                service.NextTransfer = NpcServicePolicy.RescaleDeadline(now, service.NextTransfer, service.TransferSecondsPerCar, rate);
                service.TransferSecondsPerCar = rate;
            }
            while (now >= service.NextTransfer)
            {
                int count = PendingBatch(service);
                if (count == 0)
                {
                    service.Phase = NpcServicePolicy.NextPhase(service.Phase,
                        service.Inbound.Count - service.SetoutDone, service.Outbound.Count - service.PickupDone);
                    if (service.Phase == "Departing")
                    {
                        if (!service.Outbound.Any(p => p.Queued && !p.Missed)) { NpcPoolPower.Park(service); return; }
                        if (!NpcDeparturePlacement.FaceDeparture(service, spawn))
                        { service.Phase = "Picking up"; service.WaitingReason = "Waiting for room to place power at the departure end"; service.NextTransfer = now + 10; break; }
                        NpcTimetableRegistry.RegisterReturn(service);
                        NpcTrainOperations.Order(lead, spawn);
                        NpcTrafficMessages.Send(service, "outbound", $"Departing {interchange.DisplayName} with {service.Outbound.Count(p => !p.Missed)} pickup cars; {service.Outbound.Count(p => p.Missed)} missed pickups.");
                        break;
                    }
                    service.NextTransfer = Deadline(service, service.NextTransfer, PendingBatch(service));
                    continue;
                }
                bool success = service.Phase == "Picking up" ? Pickup(service, count) : Setout(service, interchange, count);
                if (!success)
                {
                    service.WaitingReason = service.Phase == "Picking up" ? "Waiting for room on service train" : "Waiting for free interchange track";
                    // Full interchange: collect a group to free space. Full train: set out a group first.
                    if (service.Phase == "Setting out" && service.PickupDone < service.Outbound.Count) service.Phase = "Picking up";
                    else if (service.Phase == "Picking up" && service.SetoutDone < service.Inbound.Count) service.Phase = "Setting out";
                    service.NextTransfer = Deadline(service, now, PendingBatch(service));
                    break;
                }
                service.NextTransfer = Deadline(service, service.NextTransfer, PendingBatch(service));
                NpcServiceStore.Save();
            }
        }
        if (service.Phase == "Departing") NpcTrafficMessages.Approaching(service, lead, spawn, "railroad exit", "approach-outbound");
        if (service.Phase == "Departing" && NpcTrainOperations.Arrived(lead, spawn))
        {
            NpcTrafficMessages.Send(service, "complete", $"Clear of {interchange.DisplayName}; service train leaving the railroad.");
            NpcTrainOperations.Trusted(() =>
            {
                foreach (var pickup in service.Outbound)
                {
                    if (pickup.Missed || !pickup.Queued) continue;
                    if (!TrainController.Shared.TryGetCarForId(pickup.CarId, out var picked)) continue;
                    NpcTrainOperations.Marker(picked, false);
                    if (pickup.Bardo != null) TrainController.Shared.MoveToBardo(pickup.CarId, pickup.Bardo);
                    else TrainController.Shared.RemoveCarSmart(pickup.CarId);
                }
                foreach (string id in service.PowerIds)
                    if (TrainController.Shared.TryGetCarForId(id, out _)) TrainController.Shared.RemoveCarSmart(id);
            });
            NpcServiceStore.State.Services.Remove(service);
            NpcTimetableRegistry.Remove(service);
            // Match the native extra-service retry policy for unfilled orders.
            if (interchange.Orders.Any(o => o.CarCount > 0))
            {
                var next = Interchange.NextAvailableServiceTime(TimeWeather.Now);
                if (Interchange.NextAvailableServiceTime(next) <= interchange.GetNextServiceTime(TimeWeather.Now, out _, true))
                    interchange.ScheduleExtra(next);
            }
        }
    }

    private static void AddExtraPickups(NpcServiceRecord service, Interchange interchange, Location from, Location to)
    {
        if (!NpcTrainOperations.Route(from, to, out var route, out _)) return;
        var power = service.PowerIds.Select(id => TrainController.Shared.CarForId(id)).Where(c => c != null).ToList();
        float capacity = 0;
        NpcTrainOperations.TryCatalogPower(0, route, null, out _, installedPower: power, utilization: 0.9, reportCapacity: pounds => capacity = pounds);
        float reserved = service.PickupSnapshot.Where(p => NpcPickupTracking.Available(p)).Sum(p => TrainController.Shared.CarForId(p.CarId).Weight);
        float lengthLimit = NpcPassingSidings.Limit(route);
        var reservedCars = service.PickupSnapshot.Where(p => NpcPickupTracking.Available(p)).Select(p => TrainController.Shared.CarForId(p.CarId).Descriptor()).ToList();
        var powerDescriptors = power.Select(c => c.Descriptor()).ToList();
        var extras = PickupCars(interchange).Where(c => !service.PickupSnapshot.Any(p => p.CarId == c.id)).OrderBy(_ => UnityEngine.Random.value).ToList();
        int accepted = 0;
        foreach (var car in extras)
        {
            if (reserved + car.Weight > capacity) continue;
            if (NpcPassingSidings.Length(powerDescriptors.Concat(reservedCars).Concat(new[] { car.Descriptor() })) > lengthLimit) continue;
            var wb = car.Waybill!.Value;
            service.PickupSnapshot.Add(new NpcPickupSnapshot { CarId = car.id, CarName = car.DisplayName, InterchangeId = interchange.Identifier, DestinationId = wb.Destination.Identifier,
                SpanIds = PickupSpans(interchange, car).Where(s => s.Contains(car.OpsLocation)).Select(s => s.id).ToList(), WaybillCreated = wb.Created.TotalSeconds,
                PayoutWasCredited = wb.Completed, Payout = Math.Max(0, wb.PaymentOnArrival - wb.ConditionFineForCarCondition(car.Condition)) });
            reserved += car.Weight;
            reservedCars.Add(car.Descriptor());
            accepted++;
        }
        if (accepted > 0) NpcTrafficMessages.Send(service, "extra-pickups", $"{interchange.DisplayName}: reserving {accepted} additional pickup cars received while en route; keeping 10% power headroom.");
        ModDiagnosticLog.Write("EXTRA PICKUPS", $"service={service.Id}; candidates={extras.Count}; accepted={accepted}; return tons={reserved / 2000:N0}; 90% capacity tons={capacity / 2000:N0}");
    }

    private static int PendingBatch(NpcServiceRecord service) => Math.Min(NpcServicePolicy.BatchSize,
        service.Phase == "Picking up" ? service.Outbound.Count - service.PickupDone : service.Inbound.Count - service.SetoutDone);

    private static double Deadline(NpcServiceRecord service, double start, int count) =>
        NpcServicePolicy.NextTransfer(start, count, service.TransferSecondsPerCar);

    private static bool CabooseNearby(Interchange interchange)
    {
        float configured = TweaksAndThingsPlugin.Instance?.settings?.CabeeseSearchRadiusFtInMeters ?? 0;
        float radius = configured > 0 ? configured : 152.4f; // 500 feet when the legacy setting is unset.
        return TrainController.Shared.Cars.Any(c => c.IsCaboose() && !c.IsInBardo &&
            (interchange.TrackSpans.Any(s => s.Contains(c.OpsLocation)) ||
                Vector3.Distance(c.OpsLocation.GetPosition(), interchange.CenterPoint) <= radius));
    }

    private static void CollectOutbound(NpcServiceRecord service, Interchange interchange, string carId)
    {
        Collecting = service;
        CollectingCarId = carId;
        try { interchange.ServeInterchange(new NpcInterchangeContext(interchange.CreateContext(TimeWeather.Now, 0f), false)); }
        finally { Collecting = null; CollectingCarId = null; }
    }

    private static bool Pickup(NpcServiceRecord service, int count)
    {
        foreach (var pickup in service.Outbound.Skip(service.PickupDone).Take(count).ToList())
        {
            var expected = service.PickupSnapshot.FirstOrDefault(p => p.CarId == pickup.CarId);
            if (expected != null && !NpcPickupTracking.Available(expected, pickup.Queued))
            {
                NpcPickupTracking.Missed(service, expected);
                pickup.Missed = true;
                service.PickupDone++;
                continue;
            }
            if (!TrainController.Shared.TryGetCarForId(pickup.CarId, out var car)) return false;
            var tail = NpcTrainOperations.Tail(service);
            var location = Graph.Shared.LocationByMoving(tail.LocationB, -1f);
            if (!NpcTrainOperations.CanPlaceAt(location, car.carLength + 2)) return false;
            var interchange = OpsController.Shared.AllInterchanges.First(i => i.Identifier == service.InterchangeId);
            if (!pickup.Queued) CollectOutbound(service, interchange, pickup.CarId);
            if (!pickup.Queued) { service.WaitingReason = "Native backend has not accepted pickup " + pickup.CarId; return false; }
            NpcTrainOperations.Trusted(() =>
            {
                NpcTrainOperations.Marker(car, true);
                NpcTimetableRegistry.SetCrew(car, service.CrewId);
                TrainController.Shared.MoveCarCoupleTo(car, tail.LocationB, tail);
                car.SetHandbrake(false);
            });
            service.PickupDone++;
            NpcPickupTracking.Collected(pickup.CarId);
        }
        NpcTrainOperations.SyncPlacement(TrainController.Shared.CarForId(service.LeadId).EnumerateCoupled());
        return true;
    }

    private static bool Setout(NpcServiceRecord service, Interchange interchange, int count)
    {
        TrimArrival(service, interchange);
        var ids = service.Inbound.Skip(service.SetoutDone).Take(count).ToList();
        if (ids.Count == 0) return true;
        var cars = ids.Select(id => TrainController.Shared.CarForId(id)).ToList();
        if (cars.Any(c => c == null)) throw new InvalidOperationException("Inbound car disappeared");
        var descriptors = cars.Select(c =>
        {
            var descriptor = c.Descriptor();
            var properties = new Dictionary<string, Value>(descriptor.Properties) { [ThroughTrafficGuard.MarkerKey] = Value.Bool(false) };
            return new CarDescriptor(descriptor.DefinitionInfo, descriptor.Ident, descriptor.Bardo, null, descriptor.Flipped, properties);
        }).ToList();
        var locations = NpcSetoutPlacement.Plan(service, interchange, descriptors, ids);
        if (locations == null) return false;
        NpcTrainOperations.Trusted(() =>
        {
            int offset = 0;
            foreach (var cut in locations) {
                var cutIds = ids.Skip(offset).Take(cut.descriptors.Count).ToList();
                TrainController.Shared.PlaceTrain(cut.location, cut.descriptors, cutIds);
                NpcSetoutPlacement.JoinNearby(cutIds.Select(id => TrainController.Shared.CarForId(id)).ToList(), interchange);
                offset += cut.descriptors.Count;
            }
        });
        foreach (var car in cars) ReleaseSetout(car);
        service.SetoutDone += ids.Count;
        ModDiagnosticLog.Write("SETOUT RELEASE", $"service={service.Id}; interchange={interchange.Identifier}; cars={string.Join(",", ids)}; NPC marker and AI crew cleared after native placement.");
        // Removing an inbound group from the middle breaks the remaining train's coupler chain.
        // Rejoin its own cars through native movement/coupling, without changing their identities or waybills.
        var remaining = service.PowerIds.Concat(service.Inbound.Skip(service.SetoutDone))
            .Concat(service.Outbound.Take(service.PickupDone).Where(p => !p.Missed).Select(p => p.CarId))
            .Select(id => TrainController.Shared.CarForId(id)).ToList();
        NpcDeparturePlacement.MoveExisting(remaining[0].LocationA, remaining);
        NpcTrainOperations.SyncPlacement(cars.Concat(TrainController.Shared.CarForId(service.LeadId).EnumerateCoupled()));
        return true;
    }

    private static void ReleaseSetout(Car car)
    {
        NpcTrainOperations.Marker(car, false);
        NpcTimetableRegistry.SetCrew(car, null);
        ThroughTrafficGuard.ForgetNotice(car.id);
    }

    internal static void ReleaseCompletedSetouts(NpcServiceRecord service)
    {
        foreach (string id in service.Inbound.Take(service.SetoutDone))
        {
            if (!TrainController.Shared.TryGetCarForId(id, out var car) || car.IsInBardo) continue;
            // A delivered car may legitimately have joined a later NPC pickup.
            bool active = NpcServiceStore.State.Services.Any(s => s.PowerIds.Contains(id) ||
                s.Inbound.Skip(s.SetoutDone).Contains(id) || s.Outbound.Take(s.PickupDone).Any(p => !p.Missed && p.CarId == id));
            if (active || (!ThroughTrafficGuard.IsGenerated(car) && !NpcTimetableRegistry.IsAiCrew(car.trainCrewId))) continue;
            ReleaseSetout(car);
            ModDiagnosticLog.Write("SETOUT RELEASE", $"Recovered normal service for previously delivered car {id} from service {service.Id}.");
        }
    }

    private static void TrimArrival(NpcServiceRecord service, Interchange interchange)
    {
        var remaining = service.Inbound.Skip(service.SetoutDone).Select(id => TrainController.Shared.CarForId(id)).ToList();
        var random = new System.Random();
        int held = NpcServicePolicy.TrimRandomToFit(remaining, cut => cut.Count == 0 ||
            NpcSetoutPlacement.Plan(service, interchange, cut.Select(c => c.Descriptor()).ToList(), cut.Select(c => c.id).ToList()) != null,
            random.Next, car => NpcHeldDeliveryTracking.HoldCar(service, interchange, car));
        if (held == 0) return;
        NpcTrainOperations.Trusted(() =>
        {
            var tail = TrainController.Shared.CarForId(service.PowerIds.Last());
            foreach (var car in remaining.Concat(service.Outbound.Take(service.PickupDone).Where(p => !p.Missed).Select(p => TrainController.Shared.CarForId(p.CarId))))
            { TrainController.Shared.MoveCarCoupleTo(car, tail.LocationB, tail); tail = car; }
        });
        NpcTrafficMessages.Send(service, "arrival-held-" + service.Inbound.Count + "-" + service.SetoutDone, $"{interchange.DisplayName} yard full: {held} setout cars held for the next service; {remaining.Count} cars fit now.");
    }
}
