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
    private readonly Dictionary<string, Queue<(CarDescriptor descriptor, float amount)>> cars = new();
    private readonly List<(int orderIndex, CarDescriptor descriptor, string? carId, float amount, float weight)> selected = new();
    private readonly Random random = new();
    private readonly List<CarDescriptor> deferredDescriptors = new();
    internal float Weight;
    internal int Count;
    internal int Deferred;
    internal List<CarDescriptor> Descriptors => selected.Select(c => c.descriptor).ToList();
    private static string Key(Order order) => order.Destination.Identifier + "|" + order.Criteria.CarTypeFilter.queryString + "|" + order.Criteria.Load?.id + "|" + order.Tag;

    internal static NpcInboundPlan Build(Interchange interchange, GameDateTime due)
    {
        var result = new NpcInboundPlan();
        var context = (IndustryContext)interchange.CreateContext(due, 0);
        var rnd = new Random();
        int capacity = interchange.CalculateCapacity();
        for (int orderIndex = 0; orderIndex < interchange.Orders.Count; orderIndex++)
        {
            var order = interchange.Orders[orderIndex];
            for (int i = 0; i < order.Quantity.Count; i++)
            {
                if (order is ReturnFromBardoOrder returning)
                {
                    string carId = returning.CarIds[i];
                    var car = TrainController.Shared.CarForId(carId);
                    if (car != null) result.selected.Add((orderIndex, car.Descriptor(), carId, 0f, car.Weight));
                    continue;
                }
                if (!(order is Order native)) continue;
                if (!context.TryCreateCarDescriptorForOrder(native, TrainController.Shared.ContentLibrary, context._sizePreference, context.Random,
                    out var descriptor, out float amount)) continue;
                var definition = descriptor.DefinitionInfo.Definition;
                var load = native.Criteria.Load;
                result.selected.Add((orderIndex, descriptor, null, amount, definition.WeightEmpty + (load == null ? 0 : load.Pounds(amount))));
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
            $"Plan {interchange.Identifier}: native orders={interchange.Orders.Sum(o => o.Quantity.Count)}, selected={result.Count}, deferred={result.Deferred}, tons={result.Weight / 2000:F1}; yard capacity={interchange.CalculateCapacity()}; cars={string.Join(",", result.selected.Select(c => c.descriptor.DefinitionInfo.Identifier))}");
        return result;
    }

    internal void TrimForSpawn(Interchange interchange, Track.Location spawn, List<CarDescriptor> power)
    {
        Deferred += NpcServicePolicy.TrimRandomToFit(selected,
            _ => NpcTrainOperations.CanPlaceAt(spawn, TrainController.ApproximateLength(power.Concat(Descriptors).ToList())),
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
                order.Criteria.CarTypeFilter.Matches(descriptor.DefinitionInfo.Definition.CarType))
                return (descriptor.DefinitionInfo.Identifier, wb.Value.PaymentOnArrival);
        }
        return (order.Criteria.CarTypeFilter.queryString, -1);
    }

    private void Refresh(List<IOrder> orders)
    {
        Count = selected.Count;
        Weight = selected.Sum(c => c.weight);
        cars.Clear();
        foreach (var entry in selected)
        {
            if (!(orders[entry.orderIndex] is Order native)) continue;
            if (!cars.TryGetValue(Key(native), out var queue)) cars[Key(native)] = queue = new Queue<(CarDescriptor descriptor, float amount)>();
            queue.Enqueue((entry.descriptor, entry.amount));
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
            int groupCount = group.Count();
            if (orders[group.Key] is Order nativeOrder)
                batch.Add(new Order(nativeOrder.Criteria, OrderQuantity.CarCount(groupCount), nativeOrder.Destination, nativeOrder.Tag, nativeOrder.NoPayment));
            else if (orders[group.Key] is ReturnFromBardoOrder returning)
                batch.Add(new ReturnFromBardoOrder(group.Select(c => c.carId).Where(id => !string.IsNullOrEmpty(id)).Cast<string>().ToList()));
        }
        inner.FulfillOrders(batch, Math.Min(maximum, Count), out _);
    }

    internal bool TryTake(Order order, out CarDescriptor descriptor, out float amount)
    {
        if (cars.TryGetValue(Key(order), out var queue) && queue.Count > 0)
        {
            (descriptor, amount) = queue.Dequeue();
            return true;
        }
        descriptor = default;
        amount = 0f;
        return false;
    }
}

internal static class NpcOrderFactory
{
    internal static Order Create(CarTypeFilter filter, Model.Ops.Definition.Load? load, OpsCarPosition destination,
        int count, string tag, bool noPayment) =>
        new(new OrderCriteria(filter, load!, load == null), OrderQuantity.CarCount(Math.Max(0, count)), destination, tag, noPayment);
}
