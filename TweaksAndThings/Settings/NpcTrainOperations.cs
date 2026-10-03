using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Game.Messages;
using Game.State;
using KeyValue.Runtime;
using Model;
using Model.Definition;
using Model.Ops;
using Track;
using Track.Search;
using UI.EngineControls;
using Model.AI;
using Model.Definition.Data;
using Model.Physics;
using RMROC451.TweaksAndThings.Patches;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal static class NpcTrainOperations
{
    [ThreadStatic] private static int trackAccessDepth;
    internal static bool HasTrackAccess => trackAccessDepth > 0;
    internal static bool CanPlaceAt(Location location, float length)
    {
        trackAccessDepth++;
        try { return TrainController.Shared.CanPlaceAt(location, length); }
        finally { trackAccessDepth--; }
    }
    private sealed class Approach
    {
        internal Graph Graph = null!;
        internal int SegmentCount;
        internal string? SpawnOverride, TargetOverride;
        internal Location Spawn, Target;
        internal float Meters;
        internal double TravelSeconds;
        internal float Expires;
        internal bool Found;
        internal NpcSpawnMode Mode;
        internal IEnumerator? Work;
    }
    private static readonly Dictionary<string, Approach> approaches = new Dictionary<string, Approach>();
    internal static int SpeedMph => AutoEngineerMode.Waypoint.MaxSpeedMph();

    internal static string ApproachStatus(string id)
    {
        if (!approaches.TryGetValue(id, out var plan)) return "route not requested";
        if (plan.Work != null) return "calculating approach route";
        if (!plan.Found) return "no reachable approach; check enabled tracks and location overrides";
        return $"route {plan.Meters:N0} m, travel {plan.TravelSeconds / 60:N1} game minutes, spawn={Graph.Shared.LocationToString(plan.Spawn)}, target={Graph.Shared.LocationToString(plan.Target)}";
    }

    internal static bool IsApproachPlanning(string id) => approaches.TryGetValue(id, out var plan) && plan.Work != null;

    internal static string ApproachSummary(string id)
    {
        if (!approaches.TryGetValue(id, out var plan) || plan.Work != null) return "Calculating route…";
        if (!plan.Found) return "No reachable route; check enabled tracks and location overrides.";
        return $"{plan.Meters / 1609.344f:N1} mi · estimated travel {TimeSpan.FromSeconds(plan.TravelSeconds):hh\\:mm}";
    }

    internal static int PlanningSpeedLimit(bool firstClass, bool freight = true, bool hasCaboose = false)
    {
        var settings = TweaksAndThingsPlugin.Instance?.settings;
        return NpcServicePolicy.SpeedLimit(SpeedMph, settings?.SafetyFirst == true,
            settings?.RequireConsistCabooseForOilerAndHotboxSpotter == true, firstClass, freight, hasCaboose, npc: true);
    }

    internal static double TravelSeconds(IEnumerable<RouteSearch.Step> steps, bool firstClass)
    {
        int cap = PlanningSpeedLimit(firstClass);
        return steps.Sum(s => s.Distance / (Math.Max(1, Math.Min(cap, s.Location.segment.GetExpectedSpeedLimit())) * 0.44704));
    }

    internal static void Trusted(Action action)
    {
        ThroughTrafficGuard.BeginTrustedMutation();
        trackAccessDepth++;
        try { action(); } finally { trackAccessDepth--; ThroughTrafficGuard.EndTrustedMutation(); }
    }

    internal static void SyncPlacement(IEnumerable<Car> cars)
    {
        // Local movement helpers do not send the AddCars snapshots used by
        // native placement. Republish positions/culling state before set deltas.
        if (Network.Multiplayer.IsClientActive)
            StateManager.ApplyLocal(new AddCars(cars.Where(c => c != null && !c.IsInBardo).Distinct().Select(c => c.Snapshot()).ToList()));
        TrainController.Shared._integrationSets.SendDelta();
    }

    internal static bool Route(Location from, Location to, out List<RouteSearch.Step> steps, out float meters)
    {
        steps = new List<RouteSearch.Step>();
        meters = 0;
        if (!from.IsValid || !to.IsValid || !Graph.Shared.FindRoute(from, to, HeuristicCosts.AutoEngineer,
                steps, out var metrics, checkForCars: false) || steps.Count == 0) return false;
        meters = metrics.Distance;
        return steps.All(s => s.Location.segment.GroupEnabled);
    }

    internal static Location FacingRoute(Location from, Location target)
    {
        // The native geometric "toward" helper selects the opposite facing on
        // these placement locations. Use the search's actual departure direction.
        if (!Route(from, target, out var route, out _) || route[0].Location.segment != from.segment)
            throw new InvalidOperationException("No forward departure route from NPC placement location");
        return from.WithEnd(route[0].Location.end);
    }

    internal static bool TryApproach(Interchange interchange, Settings settings, out Location spawn, out Location target, out double travelSeconds)
    {
        var custom = settings.InterchangeApproaches?.FirstOrDefault(o => o.InterchangeId == interchange.Identifier);
        int count = Graph.Shared.Segments.Count();
        if (!approaches.TryGetValue(interchange.Identifier, out var plan) ||
            plan.Graph != Graph.Shared || plan.SegmentCount != count ||
            plan.SpawnOverride != custom?.SpawnLocation || plan.TargetOverride != custom?.ServiceLocation ||
            plan.Mode != settings.InterchangeSpawnMode ||
            Time.realtimeSinceStartup >= plan.Expires ||
            (plan.Found && (!plan.Spawn.segment.GroupEnabled || !plan.Target.segment.GroupEnabled)))
        {
            plan = new Approach { Graph = Graph.Shared, SegmentCount = count,
                Mode = settings.InterchangeSpawnMode,
                SpawnOverride = custom?.SpawnLocation, TargetOverride = custom?.ServiceLocation };
            plan.Expires = float.MaxValue;
            plan.Work = PlanApproach(interchange, settings, plan);
            approaches[interchange.Identifier] = plan;
        }
        spawn = plan.Spawn;
        target = plan.Target;
        travelSeconds = plan.TravelSeconds;
        return plan.Found;
    }

    internal static void AdvancePlanning()
    {
        if (Graph.Shared == null) return;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (var plan in approaches.Values.Where(p => p.Work != null).ToList())
        {
            if (plan.Graph != Graph.Shared) { plan.Work = null; continue; }
            try { plan.Work!.MoveNext(); }
            catch (Exception ex)
            {
                plan.Work = null;
                plan.Found = false;
                plan.Expires = Time.realtimeSinceStartup + 30;
                TweaksAndThingsPlugin.LogException("NPC approach planning failed", ex);
            }
            if ((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency >= 2) break;
        }
    }

    private static IEnumerator PlanApproach(Interchange interchange, Settings settings, Approach plan)
    {
        foreach (var step in FindApproach(interchange, settings, plan)) yield return step;
        if (plan.Spawn.IsValid && Route(plan.Spawn, plan.Target, out var route, out _))
        {
            plan.TravelSeconds = TravelSeconds(route, firstClass: false);
            plan.Found = true;
        }
        plan.Expires = Time.realtimeSinceStartup + (plan.Found ? 60f : 10f);
        plan.Work = null;
    }

    private static IEnumerable<int> FindApproach(Interchange interchange, Settings settings, Approach plan)
    {
        Location spawn = Location.Invalid, target = Location.Invalid;
        var custom = settings.InterchangeApproaches?.FirstOrDefault(o => o.InterchangeId == interchange.Identifier);
        if (!string.IsNullOrWhiteSpace(custom?.ServiceLocation)) target = Graph.Shared.ResolveLocationString(custom!.ServiceLocation);
        else
        {
            float nearest = float.MaxValue;
            int examined = 0;
            foreach (var segment in Graph.Shared.Segments.Where(s => s.GroupEnabled).ToList())
            {
                if (++examined % 16 == 0) yield return 0;
                if (!Graph.Shared.TryGetLocationFromPoint(segment, interchange.CenterPoint, 1000f, out var point)) continue;
                float distance = Vector3.Distance(point.GetPosition(), interchange.CenterPoint);
                if (distance < nearest) { nearest = distance; target = point; }
            }
            if (!target.IsValid) yield break;
        }
        plan.Target = target;
        if (!string.IsNullOrWhiteSpace(custom?.SpawnLocation))
        {
            spawn = Graph.Shared.ResolveLocationString(custom!.SpawnLocation);
            if (Route(spawn, target, out _, out var meters)) { plan.Spawn = spawn; plan.Meters = meters; }
            yield break;
        }
        var segments = Graph.Shared.Segments.Where(s => s.GroupEnabled).ToList();
        var connections = segments.SelectMany(s => new[] { s.a, s.b }).GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
        float best = float.MaxValue;
        // Try the geographically nearest terminals first instead of routing from every terminal.
        var distances = new Dictionary<object, double>();
        foreach (var step in GraphDistances(segments, target, distances)) yield return step;
        var boundaryPoints = segments.SelectMany(s => new[] {
            new Location(s, 0, TrackSegment.End.A).GetPosition(),
            new Location(s, 0, TrackSegment.End.B).GetPosition() }).ToList();
        float minX = boundaryPoints.Min(p => p.x), maxX = boundaryPoints.Max(p => p.x);
        float minZ = boundaryPoints.Min(p => p.z), maxZ = boundaryPoints.Max(p => p.z);
        float EdgeDistance(Location p)
        {
            var end = new Location(p.segment, 0, p.end).GetPosition();
            return Math.Min(Math.Min(end.x - minX, maxX - end.x), Math.Min(end.z - minZ, maxZ - end.z));
        }
        var candidates = segments.Where(s => connections[s.a] == 1 || connections[s.b] == 1)
            .Select(s => new Location(s, Math.Min(s.GetLength() * 0.8f, 800f),
                connections[s.a] == 1 ? TrackSegment.End.A : TrackSegment.End.B))
            .Where(p => distances.ContainsKey(p.segment.a) && distances.ContainsKey(p.segment.b));
        var ordered = settings.InterchangeSpawnMode == NpcSpawnMode.FarthestReachable
            ? candidates.OrderByDescending(p => NpcServicePolicy.DistanceToPoint(distances[p.segment.a], distances[p.segment.b],
                p.segment.GetLength(), p.distance, p.end == TrackSegment.End.A))
            : candidates.OrderBy(p => EdgeDistance(p)).ThenBy(p => Vector3.SqrMagnitude(p.GetPosition() - interchange.CenterPoint));
        foreach (var terminal in ordered)
        {
            yield return 0;
            var candidate = terminal;
            if (!Route(candidate, target, out _, out var distance) || distance < 200f || distance >= best) continue;
            candidate = FacingRoute(candidate, target);
            if (!CanPlaceAt(candidate, 100f)) continue;
            best = plan.Meters = distance;
            spawn = candidate;
            break;
        }
        plan.Spawn = spawn;
    }

    private static IEnumerable<int> GraphDistances(List<TrackSegment> segments, Location origin, Dictionary<object, double> result)
    {
        var edges = new Dictionary<object, List<(object Node, float Length)>>();
        int examined = 0;
        foreach (var s in segments)
        {
            if (++examined % 64 == 0) yield return 0;
            if (!edges.ContainsKey(s.a)) edges[s.a] = new List<(object, float)>();
            if (!edges.ContainsKey(s.b)) edges[s.b] = new List<(object, float)>();
            edges[s.a].Add((s.b, s.GetLength()));
            edges[s.b].Add((s.a, s.GetLength()));
        }
        var pending = new SortedDictionary<double, Queue<object>>();
        void Enqueue(object node, double distance)
        {
            if (!pending.TryGetValue(distance, out var queue)) pending[distance] = queue = new Queue<object>();
            queue.Enqueue(node);
        }
        float aDistance = origin.end == TrackSegment.End.A ? origin.distance : origin.segment.GetLength() - origin.distance;
        Enqueue(origin.segment.a, aDistance);
        Enqueue(origin.segment.b, origin.segment.GetLength() - aDistance);
        while (pending.Count > 0)
        {
            if (++examined % 64 == 0) yield return 0;
            var pair = pending.First();
            object node = pair.Value.Dequeue();
            if (pair.Value.Count == 0) pending.Remove(pair.Key);
            if (result.ContainsKey(node)) continue;
            result[node] = pair.Key;
            if (edges.TryGetValue(node, out var neighbors))
                foreach (var edge in neighbors)
                    if (!result.ContainsKey(edge.Node)) Enqueue(edge.Node, pair.Key + edge.Length);
        }
    }

    internal static bool TryCatalogPower(float weight, List<RouteSearch.Step> route, string? crewId, out List<CarDescriptor> power,
        List<RouteSearch.Step>? returnRoute = null, float returnWeight = 0, IReadOnlyList<Car>? installedPower = null, double utilization = 1, Action<float>? reportCapacity = null,
        float trainLength = 100, float returnTrainLength = 100)
    {
        power = new List<CarDescriptor>();
        var requirements = PowerRequirements(route, trainLength).Select(r => (r.speed, r.demand, weight)).ToList();
        if (returnRoute != null) requirements.AddRange(PowerRequirements(returnRoute, returnTrainLength).Select(r => (r.speed, r.demand, weight: returnWeight)));
        var candidates = new List<(List<Model.Definition.TypedContainerItem<CarDefinition>> definitions, double[] capacity)>();
        var store = TrainController.Shared.PrefabStore;
        foreach (var info in store.AllCarDefinitionInfos.Where(i => i.Definition.VisibleInPlacer &&
            (i.Definition is DieselLocomotiveDefinition || i.Definition is SteamLocomotiveDefinition) &&
            (installedPower == null || installedPower.Any(c => c.IsLocomotive && c.DefinitionIdentifier == i.Identifier)))
            .OrderBy(_ => UnityEngine.Random.value))
        {
            var definitions = new List<Model.Definition.TypedContainerItem<CarDefinition>> { info };
            var calculator = new GameObject("NPC power calculator");
            calculator.SetActive(false);
            try
            {
                Func<int, float> effort;
                if (info.Definition is DieselLocomotiveDefinition diesel)
                {
                    var mover = calculator.AddComponent<PrimeMover>();
                    mover.startingTractiveEffort = diesel.StartingTractiveEffort;
                    effort = speed => mover.MaxTractiveEffort(speed);
                }
                else
                {
                    var steam = (SteamLocomotiveDefinition)info.Definition;
                    if (steam.Wheelsets == null || steam.MainDriverIndex < 0 || steam.Wheelsets.Count <= steam.MainDriverIndex) continue;
                    if (info.Definition.TryGetTenderIdentifier(out var tenderIdentifier))
                    {
                        var tender = store.CarDefinitionInfoForIdentifier(tenderIdentifier);
                        if (tender.Definition.Archetype != CarArchetype.Tender)
                            throw new InvalidOperationException("Steam tender definition is not a tender: " + tenderIdentifier);
                        definitions.Add(tender);
                    }
                    var engine = calculator.AddComponent<SteamEngine>();
                    engine.OverrideStartingTractiveEffort = steam.PublishedTractiveEffort == 0 ? (float?)null : steam.PublishedTractiveEffort;
                    engine.driverDiameterInches = steam.Wheelsets[steam.MainDriverIndex].Diameter * 39.37008f;
                    engine.numberOfCylinders = 2;
                    engine.maximumBoilerPressure = steam.MaximumBoilerPressure;
                    engine.pistonStrokeInches = steam.PistonStrokeInches;
                    engine.pistonDiameterInches = steam.PistonDiameterInches;
                    engine.weightOnDrivers = steam.WeightOnDrivers;
                    engine.totalHeatingSurface = steam.TotalHeatingSurface;
                    engine.UpdateMaximumTractiveEffort();
                    effort = speed => engine.MaximumTractiveEffortAtVelocity(speed);
                }
                double unitWeight = NpcServicePolicy.Tonnes(definitions.Sum(d => d.Definition.WeightEmpty + d.Definition.LoadSlots.Sum(slot =>
                    string.IsNullOrEmpty(slot.RequiredLoadIdentifier) ? 0 : (CarPrototypeLibrary.instance.LoadForId(slot.RequiredLoadIdentifier)?.Pounds(slot.MaximumCapacity) ?? 0))));
                candidates.Add((definitions, requirements.Select(r => utilization * NpcServicePolicy.Horsepower(effort(r.speed), r.speed) - r.demand * unitWeight).ToArray()));
            }
            catch (Exception ex) { TweaksAndThingsPlugin.LogException("Unable to evaluate NPC locomotive " + info.Identifier, ex); }
            finally { UnityEngine.Object.Destroy(calculator); }
        }
        if (installedPower != null)
        {
            double tonnes = requirements.Select((r, p) => r.demand <= 0 ? double.PositiveInfinity :
                candidates.Sum(c => c.capacity[p] * installedPower.Count(car => car.IsLocomotive && car.DefinitionIdentifier == c.definitions[0].Identifier)) / r.demand).Min();
            reportCapacity?.Invoke((float)Math.Max(0, tonnes * 2204.62262));
            return requirements.Select((r, p) => candidates.Sum(c => c.capacity[p] * installedPower.Count(car => car.IsLocomotive &&
                car.DefinitionIdentifier == c.definitions[0].Identifier)) >= r.demand * NpcServicePolicy.Tonnes(r.weight)).All(enough => enough);
        }
        var selected = NpcPowerSelection.Select(candidates.Select(c => c.capacity).ToList(),
            requirements.Select(r => r.demand * NpcServicePolicy.Tonnes(r.weight)).ToArray());
        if (selected == null)
        {
            NpcTrafficDiagnostics.Report("power", $"No feasible power combination: {candidates.Count} catalog candidates, {weight / 2000:N0} tons, peak demand {requirements.Max(r => r.demand):N2} HP/tonne");
            return false;
        }
        foreach (int index in selected)
        {
            bool trailing = power.Count > 0;
            bool flipped = trailing && UnityEngine.Random.value < 0.5f;
            // Reversed steam units have the tender before the engine, so the tender stays on its physical rear.
            var unit = candidates[index].definitions;
            foreach (var definition in flipped ? unit.AsEnumerable().Reverse() : unit)
            {
                power.Add(new CarDescriptor(definition, new CarIdent("NPC", null),
                    string.Empty, crewId, flipped, new Dictionary<string, Value>
                    {
                        [ThroughTrafficGuard.MarkerKey] = Value.Bool(true),
                        [PropertyChange.KeyForControl(PropertyChange.Control.Mu)] = Value.Bool(trailing),
                        [PropertyChange.KeyForControl(PropertyChange.Control.CutOut)] = Value.Bool(trailing)
                    }));
            }
        }
        TweaksAndThingsPlugin.LogDiagnostic($"NPC power: {selected.Count} engines ({string.Join(", ", selected.Select(i => candidates[i].definitions[0].Identifier))}), {weight / 2000:N0} tons; lead controls, trailing engines MU and cut out.");
        ModDiagnosticLog.Write("POWER CHECK", $"tons={weight / 2000:N0}; return tons={returnWeight / 2000:N0}; averaging length={trainLength:N0}/{returnTrainLength:N0} m; constraints=" +
            string.Join(", ", requirements.Select((r, p) => $"{r.speed} mph: {r.demand:F2} wheel HP/tonne, demand {r.demand * NpcServicePolicy.Tonnes(r.weight):F0} HP, available {selected.Sum(i => candidates[i].capacity[p]):F0} HP after power weight")));
        return true;
    }

    internal static CarDescriptor Clone(Car template, string? crewId = null)
    {
        var source = template.Descriptor();
        var properties = new Dictionary<string, Value>(source.Properties);
        properties.Remove(Car.KeyOwned);
        properties.Remove(Car.KeyOpsWaybill);
        properties.Remove(Car.KeyOpsPassengerMarker);
        properties.Remove(Car.KeyOpsRepairDestination);
        properties[ThroughTrafficGuard.MarkerKey] = Value.Bool(true);
        return new CarDescriptor(source.DefinitionInfo,
            new CarIdent(source.Ident.ReportingMark, null),
            string.Empty, crewId, source.Flipped, properties);
    }

    private static List<(int speed, double demand)> PowerRequirements(IEnumerable<RouteSearch.Step> steps, float trainLength)
    {
        var samples = new List<NpcRouteGradePolicy.Sample>();
        int speedCap = PlanningSpeedLimit(false);
        foreach (var step in steps)
        {
            int speed = Math.Max(1, Math.Min(speedCap, step.Location.segment.GetExpectedSpeedLimit()));
            float start = Math.Max(0, step.Location.distance - step.Distance);
            for (float d = start; d < step.Location.distance; d += 10)
            {
                float length = Math.Min(10, step.Location.distance - d);
                float grade = -Graph.Shared.GradeAtLocation(new Location(step.Location.segment, d + length / 2, step.Location.end));
                samples.Add(new NpcRouteGradePolicy.Sample(length, grade, speed));
            }
        }
        var grades = NpcRouteGradePolicy.Requirements(samples, trainLength);
        if (grades.Count == 0) grades[1] = 0;
        // Catalog effort is already force at the wheels: do not subtract drivetrain losses twice.
        return grades.Select(r => (r.Key, ThroughTrafficPolicy.HorsepowerPerTonneForGrade(r.Value + Car.RollingResistance * 100, r.Key, drivetrainEfficiency: 1))).ToList();
    }
    internal static void Order(BaseLocomotive lead, Location target, bool run = true)
    {
        ConfigurePower(lead);
        bool forward = FacingRoute(lead.LocationF, target).end == lead.LocationF.end;
        if (run && !forward) throw new InvalidOperationException("NPC departure would push its consist; reposition power before issuing orders");
        Trusted(() => new AutoEngineerOrdersHelper(lead, new AutoEngineerPersistence(lead.KeyValueObject))
            .SetOrdersValue(mode: AutoEngineerMode.Waypoint, forward: forward, maxSpeedMph: run ? SpeedMph : 0,
                maybeWaypoint: (target, (string)null!)));
    }

    internal static void PlacePower(Location spawn, List<CarDescriptor> descriptors, List<string> ids)
    {
        try
        {
            Trusted(() => TrainController.Shared.PlaceTrain(spawn, descriptors, ids));
            if (ids.Any(id => !TrainController.Shared.TryGetCarForId(id, out _)))
                throw new InvalidOperationException("NPC power placement did not create every engine/tender");
            var coupled = new HashSet<string>(TrainController.Shared.CarForId(ids[0]).EnumerateCoupled().Select(c => c.id));
            if (ids.Any(id => !coupled.Contains(id))) throw new InvalidOperationException("NPC placement created disconnected power/consist vehicles");
            for (int i = 0; i < descriptors.Count; i++)
            {
                if (!descriptors[i].DefinitionInfo.Definition.TryGetTenderIdentifier(out var tender)) continue;
                int tenderIndex = descriptors[i].Flipped ? i - 1 : i + 1;
                if (tenderIndex < 0 || tenderIndex >= descriptors.Count || descriptors[tenderIndex].DefinitionInfo.Identifier != tender)
                    throw new InvalidOperationException("NPC steam engine missing its assigned tender: " + tender);
                var engine = TrainController.Shared.CarForId(ids[i]);
                if (!(engine is SteamLocomotive steam) || !steam.TryGetTender(out var coupledTender) || coupledTender.id != ids[tenderIndex])
                    throw new InvalidOperationException("NPC tender is not coupled to its engine");
            }
        }
        catch
        {
            // These IDs are generated for this attempt; never remove existing cars.
            Trusted(() =>
            {
                foreach (string id in ids)
                    if (TrainController.Shared.TryGetCarForId(id, out _)) TrainController.Shared.RemoveCarSmart(id);
            });
            throw;
        }
        TweaksAndThingsPlugin.LogDiagnostic($"NPC placement verified: {ids.Count} vehicles, {descriptors.Count(d => d.DefinitionInfo.Definition.Archetype == CarArchetype.Tender)} tenders.");
        ModDiagnosticLog.Write("CONSIST", "Initial placement: " + string.Join(",", ids.Select((id, index) => id + ":" + descriptors[index].DefinitionInfo.Identifier)));
        var lead = TrainController.Shared.CarForId(ids[0]);
        ModDiagnosticLog.Write("PLACEMENT", $"lead={lead.id}; physical front={Graph.Shared.LocationToString(lead.LocationF)}; logical front={Graph.Shared.LocationToString(lead.LocationA)}; flipped={descriptors[0].Flipped}; speed={lead.VelocityMphAbs:F2} mph.");
    }

    private static void ConfigurePower(BaseLocomotive lead)
    {
        Trusted(() =>
        {
            foreach (var car in lead.EnumerateCoupled().Where(ThroughTrafficGuard.IsGenerated))
            {
                CarInspector_PopulateCarPanel_Patch.CarEndAirUpdate(car);
                car.SetHandbrake(false);
                if (!(car is BaseLocomotive engine)) continue;
                bool trailing = engine != lead;
                if (engine.IsMuEnabled != trailing)
                    StateManager.ApplyLocal(new PropertyChange(engine.id, PropertyChange.Control.Mu, trailing));
                if (engine.locomotiveControl.air.IsCutOut != trailing)
                    StateManager.ApplyLocal(new PropertyChange(engine.id, PropertyChange.Control.CutOut, trailing));
            }
        });
    }

    internal static bool Arrived(BaseLocomotive lead, Location target) => lead.VelocityMphAbs < 0.5f &&
        Graph.Shared.CheckSameRoute(lead.LocationF, target, 60f);

    internal static void Marker(Car car, bool npc) => Trusted(() => car.KeyValueObject[ThroughTrafficGuard.MarkerKey] = Value.Bool(npc));

    internal static Car Tail(NpcServiceRecord service) => TrainController.Shared.CarForId(service.LeadId).EnumerateCoupled().Last();

    internal static bool Append(NpcServiceRecord service, List<CarDescriptor> descriptors, List<string> ids)
    {
        if (descriptors.Count == 0) return true;
        var tail = Tail(service);
        var existing = TrainController.Shared.CarForId(service.LeadId).EnumerateCoupled().ToList();
        var front = existing[0].LocationA;
        var allDescriptors = existing.Select(c => c.Descriptor()).Concat(descriptors).ToList();
        var allIds = existing.Select(c => c.id).Concat(ids).ToList();
        if (!NpcDeparturePlacement.Fits(front, TrainController.ApproximateLength(allDescriptors), new HashSet<string>(allIds))) return false;
        // Stage clear of the existing train, then position and reconnect the whole cut atomically.
        var position = Graph.Shared.LocationByMoving(tail.LocationB, -10f);
        float length = TrainController.ApproximateLength(descriptors);
        if (!CanPlaceAt(position, length))
        {
            NpcTrafficDiagnostics.Detail("append-blocked:" + service.Id, $"Append blocked service={service.Id}; tail={tail.id}, position={Graph.Shared.LocationToString(position)}, length={length:F1}m, cars={descriptors.Count}; nearby IDs={string.Join(",", TrainController.Shared.CarIdsInRadius(position.GetPosition(), 50))}");
            return false;
        }
        var previous = ids.Where(id => TrainController.Shared.TryGetCarForId(id, out _))
            .ToDictionary(id => id, id => TrainController.Shared.CarForId(id).Descriptor());
        try
        {
            Trusted(() =>
            {
                TrainController.Shared.PlaceTrain(position, descriptors, ids);
                // MoveCarCoupleTo attaches the physical front, which reverses the
                // logical chain for flipped cars and can overlap subsequent cars.
                // Native whole-cut positioning respects every descriptor's flip.
                var moved = TrainController.Shared.HandleCreateCarsAsTrain(front, allDescriptors, allIds, null);
                TrainController.ConnectCars(moved);
                TrainController.FillAir(moved);
                foreach (var car in moved) { Marker(car, true); car.SetHandbrake(false); }
                moved[0].set.SetVelocity(0f, moved);
                SyncPlacement(moved);
            });
            var coupled = new HashSet<string>(TrainController.Shared.CarForId(service.LeadId).EnumerateCoupled().Select(c => c.id));
            if (ids.Any(id => !coupled.Contains(id))) throw new InvalidOperationException("NPC inbound cars are not coupled to the lead");
            ModDiagnosticLog.Write("CONSIST", $"Verified append service={service.Id}; attached={ids.Count}, coupled total={coupled.Count}; types={string.Join(",", descriptors.Select(d => d.DefinitionInfo.Identifier))}");
            return true;
        }
        catch (Exception ex)
        {
            TweaksAndThingsPlugin.LogException("NPC append rollback " + service.Id, ex);
            Trusted(() =>
            {
                foreach (var id in ids)
                {
                    if (!TrainController.Shared.TryGetCarForId(id, out var car)) continue;
                    if (previous.TryGetValue(id, out var original) && original.Bardo != null)
                    { Marker(car, false); TrainController.Shared.MoveToBardo(id, original.Bardo); }
                    else if (!previous.ContainsKey(id)) TrainController.Shared.RemoveCarSmart(id);
                }
            });
            return false;
        }
    }
}
