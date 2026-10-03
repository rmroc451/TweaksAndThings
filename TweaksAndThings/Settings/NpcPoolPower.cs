using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Messages;
using Game.Notices;
using Game.State;
using KeyValue.Runtime;
using Model;
using Model.AI;
using Model.Ops;
using Track;
using UnityEngine;
using UI.EngineControls;
using Helpers;
using RMROC451.TweaksAndThings.Extensions;

namespace RMROC451.TweaksAndThings;

internal static class NpcPoolPower
{
    internal const string Marker = "RMROC451.TweaksAndThings.PoolPower";
    private static readonly HashSet<string> held = new();
    internal static bool IsPooled(Car? car) => car != null && !car.KeyValueObject[Marker].IsNull && car.KeyValueObject[Marker].StringValue.Length > 0;
    internal static bool SameUnit(Car a, Car b) => IsPooled(a) && IsPooled(b) && a.KeyValueObject[Marker].StringValue == b.KeyValueObject[Marker].StringValue;
    internal static bool ConnectionBlocked(Car car, string key)
    {
        if (!IsPooled(car)) return false;
        // Coupling/cut-lever controls at internal ends remain locked. Air hose
        // and anglecock operations do not split the power unit.
        if (!key.EndsWith(".coupled", StringComparison.OrdinalIgnoreCase) && !key.EndsWith(".cutLever", StringComparison.OrdinalIgnoreCase)) return false;
        var end = key.TrimStart('_').StartsWith("f.", StringComparison.OrdinalIgnoreCase) ? Car.End.F : Car.End.R;
        bool internalEnd = car.set != null && car.set.TryGetCoupledCar(car, end, out var adjacent) && SameUnit(car, adjacent);
        return NpcPoolPowerPolicy.BlocksConnection(true, internalEnd, key, ThroughTrafficGuard.IsTrustedMutation);
    }

    internal static void Park(NpcServiceRecord service)
    {
        var power = service.PowerIds.Select(id => TrainController.Shared.CarForId(id)).Where(c => c != null).ToList();
        if (power.Count == 0) return;
        var area = OpsController.Shared.ClosestArea(power[0]);
        var pool = new NpcPoolUnit { Id = Guid.NewGuid().ToString("N"), InterchangeId = service.InterchangeId,
            ParkingLocation = Graph.Shared.LocationToString(power[0].LocationA), ExitLocation = service.Spawn,
            AreaId = area?.identifier ?? "", PowerIds = power.Select(c => c.id).ToList() };
        NpcTrainOperations.Trusted(() => {
            foreach (var car in power) {
                car.KeyValueObject[Marker] = Value.String(pool.Id);
                car.SetHandbrake(true);
                if (car is BaseLocomotive loco) {
                    new AutoEngineerOrdersHelper(loco, new AutoEngineerPersistence(loco.KeyValueObject)).SetOrdersValue(mode: AutoEngineerMode.Off, maxSpeedMph: 0);
                    StateManager.ApplyLocal(new PropertyChange(loco.id, PropertyChange.Control.Throttle, 0f));
                    StateManager.ApplyLocal(new PropertyChange(loco.id, PropertyChange.Control.Mu, false));
                    StateManager.ApplyLocal(new PropertyChange(loco.id, PropertyChange.Control.CutOut, true));
                }
            }
        });
        NpcServiceStore.State.PoolPower.Add(pool);
        NpcServiceStore.State.Services.Remove(service);
        NpcTimetableRegistry.Remove(service);
        NpcServiceStore.Save();
        NpcTrafficMessages.Send(service, "pool-parked", "Power parked at " + service.InterchangeId + " for the next outbound service.");
        ModDiagnosticLog.Write("POOL POWER", $"Parked unit {pool.Id}; interchange={pool.InterchangeId}; area={pool.AreaId}; power={power.Count}.");
    }

    internal static int Dispatch(Interchange interchange, List<Car> candidates, double now)
    {
        int assigned = 0;
        foreach (var pool in NpcServiceStore.State.PoolPower.Where(p => p.InterchangeId == interchange.Identifier).ToList()) {
            var power = pool.PowerIds.Select(id => TrainController.Shared.CarForId(id)).Where(c => c != null && !c.IsInBardo).ToList();
            if (power.Count != pool.PowerIds.Count || candidates.Count == 0) continue;
            var connected = power[0].EnumerateCoupled().ToList();
            power = connected.Where(c => pool.PowerIds.Contains(c.id)).ToList();
            if (power.Count != pool.PowerIds.Count || connected.Any(c => c.VelocityMphAbs > 0.5f)) {
                NpcTrafficDiagnostics.Report("pool:" + pool.Id, "Dispatch deferred: pool unit is moving or disconnected; waiting to safely assign power.");
                continue;
            }
            var exit = Graph.Shared.ResolveLocationString(pool.ExitLocation);
            if (!NpcTrainOperations.Route(power[0].LocationF, exit, out var route, out _)) continue;
            float capacity = 0;
            NpcTrainOperations.TryCatalogPower(0, route, null, out _, installedPower: power, utilization: 0.9, reportCapacity: p => capacity = p);
            float weight = 0, length = NpcPassingSidings.Length(power.Select(c => c.Descriptor()));
            float limit = NpcPassingSidings.Limit(route);
            var selected = candidates.Where(c => {
                if (weight + c.Weight > capacity || length + c.carLength + 1 > limit) return false;
                weight += c.Weight; length += c.carLength + 1; return true;
            }).ToList();
            while (selected.Count > 0 && !NpcTrainOperations.TryCatalogPower(weight, route, null, out _, installedPower: power, utilization: 0.9, trainLength: length)) {
                var last = selected[selected.Count - 1];
                selected.RemoveAt(selected.Count - 1);
                weight -= last.Weight;
                length -= last.carLength + 1;
            }
            if (selected.Count == 0) continue;
            // Take the smallest complete prefix that can haul this assignment.
            // A steam engine and its actual coupled tender are inseparable for sizing.
            var available = power.ToList();
            for (int count = 1; count < available.Count; count++) {
                var prefix = available.Take(count).ToList();
                if (available.OfType<SteamLocomotive>().Any(s => !s.TryGetTender(out var tender) || prefix.Contains(s) != prefix.Contains(tender))) continue;
                if (!prefix.Any(c => c.IsLocomotive) || !NpcTrainOperations.TryCatalogPower(weight, route, null, out _, installedPower: prefix, utilization: 0.9,
                    trainLength: length)) continue;
                power = prefix;
                break;
            }
            var remainder = available.Except(power).ToList();
            var ordered = LeadingPower(power);
            if (ordered == null) { NpcTrafficDiagnostics.Report("pool:" + pool.Id, "Dispatch deferred: incomplete steam power unit"); continue; }
            var front = power[0].LocationA;
            bool normalize = ordered[0] != power[0] || !ordered[0].FrontIsA;
            if (normalize && !NpcDeparturePlacement.Fits(front, NpcPassingSidings.Length(power.Select(c => c.Descriptor())), new HashSet<string>(available.Select(c => c.id))))
            { NpcTrafficDiagnostics.Report("pool:" + pool.Id, "Dispatch deferred: no room to orient the leading power unit"); continue; }
            // Service is allowed to release the unit from player cars and split
            // surplus engines, even though those internal controls are user-locked.
            NpcTrainOperations.Trusted(() => {
                Disconnect(available[0], Car.LogicalEnd.A);
                Disconnect(available.Last(), Car.LogicalEnd.B);
            });
            if (remainder.Count > 0) {
                NpcTrainOperations.Trusted(() => {
                    power.Last().ApplyEndGearChange(Car.LogicalEnd.B, Car.EndGearStateKey.IsCoupled, false);
                    remainder[0].ApplyEndGearChange(Car.LogicalEnd.A, Car.EndGearStateKey.IsCoupled, false);
                    power.Last().ApplyEndGearChange(Car.LogicalEnd.B, Car.EndGearStateKey.Anglecock, 0f);
                    remainder[0].ApplyEndGearChange(Car.LogicalEnd.A, Car.EndGearStateKey.Anglecock, 0f);
                });
            }
            if (normalize) {
                var leadingIds = new HashSet<string> { ordered[0].id };
                if (ordered[0] is SteamLocomotive steam && steam.TryGetTender(out var tender)) leadingIds.Add(tender.id);
                var descriptors = ordered.Select(c => {
                    var source = c.Descriptor();
                    return new CarDescriptor(source.DefinitionInfo, source.Ident, source.Bardo, source.TrainCrewId,
                        leadingIds.Contains(c.id) ? false : source.Flipped, source.Properties);
                }).ToList();
                NpcTrainOperations.Trusted(() => {
                    var moved = TrainController.Shared.HandleCreateCarsAsTrain(front, descriptors, ordered.Select(c => c.id).ToList(), null);
                    TrainController.ConnectCars(moved);
                    TrainController.FillAir(moved);
                    moved[0].set.SetVelocity(0, moved);
                    NpcTrainOperations.SyncPlacement(moved);
                });
            }
            power = ordered;
            var service = new NpcServiceRecord { Id = Guid.NewGuid().ToString("N"), InterchangeId = interchange.Identifier,
                PoolOutbound = true, PowerIds = power.Select(c => c.id).ToList(), LeadId = power.First(c => c.IsLocomotive).id, Spawn = pool.ExitLocation,
                Target = Graph.Shared.LocationToString(power[0].LocationF), Scheduled = now, Phase = "Picking up", InboundPlanKnown = true,
                OutboundCollected = true, TransferSecondsPerCar = NpcServicePolicy.SecondsPerCar };
            NpcPickupTracking.Snapshot(service, interchange, selected);
            // Delinquents not selected for this unit's capacity stay for another service.
            service.PickupSnapshot.RemoveAll(p => !selected.Any(c => c.id == p.CarId));
            service.Outbound = selected.Select(c => new NpcPickup { CarId = c.id }).ToList();
            service.NextTransfer = NpcServicePolicy.NextTransfer(now, Math.Min(2, selected.Count));
            NpcServiceStore.State.Services.Add(service);
            RMROC451.TweaksAndThings.Patches.InterchangeTimewarp_Patch.CancelForSpawn(interchange.DisplayName);
            if (remainder.Count == 0) NpcServiceStore.State.PoolPower.Remove(pool);
            else pool.PowerIds = remainder.Select(c => c.id).ToList();
            NpcTrainOperations.Trusted(() => {
                foreach (var car in power) { car.KeyValueObject[Marker] = Value.Null(); car.SetHandbrake(false); }
            });
            service.TrainSymbol = NpcTimetableRegistry.NewSymbol("NP");
            NpcTimetableRegistry.RegisterReturn(service);
            assigned += selected.Count;
            ModDiagnosticLog.Write("POOL POWER", $"Assigned unit {pool.Id}; interchange={interchange.Identifier}; engines={power.Count(c => c.IsLocomotive)}; vehicles={power.Count}; cars={selected.Count}; tons={weight / 2000:F1}; surplus vehicles={remainder.Count}.");
            candidates.RemoveAll(c => selected.Contains(c));
            NpcServiceStore.Save();
            NpcTrafficMessages.Send(service, "pool-dispatch", $"Pool power at {interchange.DisplayName} assigned {selected.Count} outbound cars; preparing to leave the railroad.");
        }
        return assigned;
    }

    private static void Disconnect(Car car, Car.LogicalEnd end)
    {
        if (car.set == null || !car.set.TryGetCoupledCar(car, car.LogicalToEnd(end), out var adjacent)) return;
        var otherEnd = end == Car.LogicalEnd.A ? Car.LogicalEnd.B : Car.LogicalEnd.A;
        car.ApplyEndGearChange(end, Car.EndGearStateKey.IsCoupled, false);
        adjacent.ApplyEndGearChange(otherEnd, Car.EndGearStateKey.IsCoupled, false);
        car.ApplyEndGearChange(end, Car.EndGearStateKey.IsAirConnected, false);
        adjacent.ApplyEndGearChange(otherEnd, Car.EndGearStateKey.IsAirConnected, false);
        car.ApplyEndGearChange(end, Car.EndGearStateKey.Anglecock, 0f);
        adjacent.ApplyEndGearChange(otherEnd, Car.EndGearStateKey.Anglecock, 0f);
    }

    private static List<Car>? LeadingPower(List<Car> power)
    {
        var lead = power.FirstOrDefault(c => c.IsLocomotive);
        if (lead == null) return null;
        var ordered = new List<Car> { lead };
        if (lead is SteamLocomotive steam) {
            if (!steam.TryGetTender(out var tender) || !power.Contains(tender)) return null;
            ordered.Add(tender);
        }
        ordered.AddRange(power.Where(c => !ordered.Contains(c)));
        return ordered;
    }

    internal static bool Hold(BaseLocomotive locomotive, float effort)
    {
        if (!StateManager.IsHost || !NpcServiceStore.Load() || OpsController.Shared == null || Graph.Shared == null) return false;
        if (NpcServiceStore.State.PoolPower.Count == 0) {
            if (held.Remove(locomotive.id)) locomotive.PostNotice("tat-pool-limit", "Pool power restriction cleared.");
            return false;
        }
        var coupled = locomotive.EnumerateCoupled().ToList();
        var pools = coupled.Where(IsPooled).ToList();
        if (pools.Count == 0) {
            if (held.Remove(locomotive.id)) locomotive.PostNotice("tat-pool-limit", "Pool power restriction cleared.");
            return false;
        }
        bool blocked = false;
        foreach (var car in pools) {
            var unit = NpcServiceStore.State.PoolPower.FirstOrDefault(p => p.PowerIds.Contains(car.id));
            var area = unit == null ? null : OpsController.Shared.Areas.FirstOrDefault(a => a.identifier == unit.AreaId);
            if (area == null) { blocked = true; break; }
            // Check both ends with a braking allowance; manual throttle and AE
            // share the same traction/physics guard, independent of input method.
            foreach (var end in new[] { car.LocationA, car.LocationB }) {
                float advance = Math.Abs(car.velocity) > 0.05f ? Math.Sign(car.velocity * car.Orientation) : Math.Sign(effort * locomotive.Orientation);
                float margin = Math.Max(10, car.VelocityMphAbs * car.VelocityMphAbs * 0.25f);
                try {
                    var future = Graph.Shared.LocationByMoving(end, advance * margin, checkSwitchAgainstMovement: false, stopAtEndOfTrack: true);
                    bool inside = area.Contains(end.GetPosition());
                    bool futureInside = area.Contains(future.GetPosition());
                    var center = WorldTransformer.WorldToGame(area.transform.position);
                    if (NpcPoolPowerPolicy.BlocksMovement(inside, futureInside, Vector3.Distance(end.GetPosition(), center), Vector3.Distance(future.GetPosition(), center))) blocked = true;
                }
                catch { blocked = true; }
            }
        }
        if (blocked && held.Add(locomotive.id)) locomotive.PostNotice("tat-pool-limit", "Movement inhibited: attached pool power must remain in its parked game area. Uncouple at the unit's outer end to leave.");
        if (!blocked && held.Remove(locomotive.id)) locomotive.PostNotice("tat-pool-limit", "Pool power: movement within its parked game area permitted.");
        NpcTrainOperations.Trusted(() => {
            foreach (var pool in pools) {
                bool brake = blocked || !coupled.Any(c => c.IsLocomotive && !IsPooled(c));
                if (pool.HandbrakeApplied() != brake) pool.SetHandbrake(brake);
            }
        });
        if (blocked) locomotive.set.SetVelocity(0, coupled);
        return blocked;
    }
}
