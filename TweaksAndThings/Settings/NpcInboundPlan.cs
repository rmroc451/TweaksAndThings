using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Model;
using Model.Ops;

namespace RMROC451.TweaksAndThings;

// Choose native order descriptors once and reuse them when the native backend
// loads the service, so power is sized for the cars that will actually arrive.
internal sealed class NpcInboundPlan
{
    [ThreadStatic] internal static NpcInboundPlan? Active;
    private readonly Dictionary<string, Queue<CarDescriptor>> cars = new();
    private readonly List<(int orderIndex, CarDescriptor descriptor, string? carId, float weight)> selected = new();
    private readonly Random random = new();
    private readonly List<CarDescriptor> deferredDescriptors = new();
    internal float Weight;
    internal int Count;
    internal int Deferred;
    internal List<CarDescriptor> Descriptors => selected.Select(c => c.descriptor).ToList();
    private static string Key(Order order) => order.Destination.Identifier + "|" + order.CarTypeFilter.queryString + "|" + order.Load?.id + "|" + order.Tag;

    internal static NpcInboundPlan Build(Interchange interchange, GameDateTime due)
    {
        var result = new NpcInboundPlan();
        var context = (IndustryContext)interchange.CreateContext(due, 0);
        var rnd = new Random();
        int capacity = interchange.CalculateCapacity();
        for (int orderIndex = 0; orderIndex < interchange.Orders.Count; orderIndex++)
        {
            var order = interchange.Orders[orderIndex];
            for (int i = 0; i < order.CarCount; i++)
            {
                if (order is ReturnFromBardoOrder returning)
                {
                    var car = TrainController.Shared.CarForId(returning.CarId);
                    if (car != null) result.selected.Add((orderIndex, car.Descriptor(), returning.CarId, car.Weight));
                    continue;
                }
                if (!(order is Order native)) continue;
                var descriptor = context.CreateCarDescriptorForOrder(native, TrainController.Shared.PrefabStore, context._sizePreference, rnd);
                var definition = descriptor.DefinitionInfo.Definition;
                result.selected.Add((orderIndex, descriptor, null, definition.WeightEmpty + (native.Load == null ? 0 : native.Load.Pounds(definition.LoadSlots[0].MaximumCapacity))));
            }
        }
        // Preview the native fitter against the real yard, removing random cars rather
        // than always starving the last industry's orders. Removed orders remain pending.
        result.Deferred += NpcServicePolicy.TrimRandomToFit(result.selected, cut => cut.Count <= capacity &&
            TrainPlacementHelper.FindLocationsForCars(TrainController.Shared, interchange.TrackSpans.ToList(),
                result.Descriptors, result.selected.Select(c => c.carId!).ToList()) != null,
            result.random.Next, entry => result.deferredDescriptors.Add(entry.descriptor));
        result.Refresh(interchange.Orders);
        NpcTrafficDiagnostics.Detail("plan:" + interchange.Identifier,
            $"Plan {interchange.Identifier}: native orders={interchange.Orders.Sum(o => o.CarCount)}, selected={result.Count}, deferred={result.Deferred}, tons={result.Weight / 2000:F1}; yard capacity={interchange.CalculateCapacity()}; cars={string.Join(",", result.selected.Select(c => c.descriptor.DefinitionInfo.Identifier))}");
        return result;
    }

    internal void TrimForSpawn(Interchange interchange, Track.Location spawn, List<CarDescriptor> power)
    {
        Deferred += NpcServicePolicy.TrimRandomToFit(selected,
            _ => NpcTrainOperations.CanPlaceAt(spawn, TrainController.ApproximateLength(power.Concat(Descriptors))),
            random.Next, entry => deferredDescriptors.Add(entry.descriptor));
        Refresh(interchange.Orders);
    }

    internal void TrimToReservation(Interchange interchange, int maxCars, float maxWeight)
    {
        Deferred += NpcServicePolicy.TrimRandomToFit(selected, cut => cut.Count <= maxCars && cut.Sum(c => c.weight) <= maxWeight,
            random.Next, entry => deferredDescriptors.Add(entry.descriptor));
        Refresh(interchange.Orders);
    }

    internal void TrimToLength(Interchange interchange, float maximum, List<CarDescriptor> power)
    {
        Deferred += NpcServicePolicy.TrimRandomToFit(selected,
            _ => NpcPassingSidings.Length(power.Concat(Descriptors)) <= maximum,
            random.Next, entry => deferredDescriptors.Add(entry.descriptor));
        Refresh(interchange.Orders);
    }

    internal (string description, int payment) DescribeDeferred(Order order)
    {
        foreach (var descriptor in deferredDescriptors)
        {
            if (!descriptor.Properties.TryGetValue("ops.waybill", out var value)) continue;
            var wb = Waybill.FromPropertyValue(value, OpsController.Shared);
            if (wb.HasValue && wb.Value.Destination.Identifier == order.Destination.Identifier &&
                order.CarTypeFilter.Matches(descriptor.DefinitionInfo.Definition.CarType))
                return (descriptor.DefinitionInfo.Identifier, wb.Value.PaymentOnArrival);
        }
        return (order.CarTypeFilter.queryString, -1);
    }

    private void Refresh(List<IOrder> orders)
    {
        Count = selected.Count;
        Weight = selected.Sum(c => c.weight);
        cars.Clear();
        foreach (var entry in selected)
        {
            if (!(orders[entry.orderIndex] is Order native)) continue;
            if (!cars.TryGetValue(Key(native), out var queue)) cars[Key(native)] = queue = new Queue<CarDescriptor>();
            queue.Enqueue(entry.descriptor);
        }
    }

    internal void AddOrderedCars(IIndustryContext inner, List<IOrder> orders, int maximum)
    {
        var groups = selected.GroupBy(c => c.orderIndex).OrderBy(g => g.Key).ToList();
        var batch = new List<IOrder>();
        foreach (var group in groups)
        {
            // Native orders are boxed structs. Make new boxes so deferred demand stays intact.
            IOrder copy;
            if (orders[group.Key] is Order native) copy = native;
            else if (orders[group.Key] is ReturnFromBardoOrder returning) copy = returning;
            else continue;
            copy.CarCount = group.Count();
            batch.Add(copy);
        }
        try { inner.AddOrderedCars(batch, Math.Min(maximum, Count)); }
        finally
        {
            for (int i = 0; i < batch.Count; i++)
            {
                int index = groups[i].Key;
                int consumed = groups[i].Count() - batch[i].CarCount;
                var original = orders[index];
                original.CarCount = Math.Max(0, original.CarCount - consumed);
                orders[index] = original;
            }
        }
    }

    internal bool TryTake(Order order, out CarDescriptor descriptor)
    {
        if (cars.TryGetValue(Key(order), out var queue) && queue.Count > 0)
        { descriptor = queue.Dequeue(); return true; }
        descriptor = default;
        return false;
    }
}
