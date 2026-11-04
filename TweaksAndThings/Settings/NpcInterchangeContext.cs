using System.Collections.Generic;
using System.Linq;
using Game;
using Model.Ops;
using Model.Ops.Definition;

namespace RMROC451.TweaksAndThings;

/// <summary>Delegates native business rules, deferring physical outbound removal until the NPC leaves the map.</summary>
internal sealed class NpcInterchangeContext : IIndustryContext
{
    private readonly IIndustryContext inner;
    private readonly bool loading;
    internal NpcInterchangeContext(IIndustryContext inner, bool loading) { this.inner = inner; this.loading = loading; }
    public GameDateTime Now => inner.Now;
    public float DeltaTime => inner.DeltaTime;
    public float PortionOfDayUntilNextRegularService => inner.PortionOfDayUntilNextRegularService;
    public Core.Random.IRandom Random => inner.Random;
    // Loading takes place at the remote service-train spawn, not in the destination yard.
    // Existing yard cars must not consume the backend's incoming-train capacity.
    // The native fitter still checks real yard occupancy when we set cars out on arrival.
    public IEnumerable<IOpsCar> CarsAtPosition() => loading ? Enumerable.Empty<IOpsCar>() : inner.CarsAtPosition().Where(c =>
        !ThroughTrafficGuard.IsGeneratedCarId(c.Id) && (SimulatedInterchangeService.Collecting == null ||
        (c.Id == SimulatedInterchangeService.CollectingCarId && SimulatedInterchangeService.Collecting.PickupSnapshot.Any(p => p.CarId == c.Id && NpcPickupTracking.Available(p)))));
    public void AddOrderedCars(List<IOrder> orders, int maxToOrder)
    {
        if (!loading) return;
        if (NpcInboundPlan.Active != null) NpcInboundPlan.Active.AddOrderedCars(inner, orders, maxToOrder);
        else throw new System.InvalidOperationException("NPC loading requires a capacity-checked inbound plan");
    }
    public void RemoveCar(IOpsCar car) { if (!SimulatedInterchangeService.QueuePickup(car.Id)) inner.RemoveCar(car); }
    public void MoveToBardo(IOpsCar car) => inner.MoveToBardo(car);
    public void OrderAwayEmpty(IOpsCar car, string orderTag, bool noPayment) => inner.OrderAwayEmpty(car, orderTag, noPayment);
    public void OrderAwayLoaded(IOpsCar car, string orderTag, bool noPayment) => inner.OrderAwayLoaded(car, orderTag, noPayment);
    public bool OrderLoad(CarTypeFilter carTypeFilter, Load load, OrderQuantity quantity, string orderTag, bool noPayment) => inner.OrderLoad(carTypeFilter, load, quantity, orderTag, noPayment);
    public bool OrderEmpty(CarTypeFilter carTypeFilter, Load load, OrderQuantity quantity, string orderTag, bool noPayment) => inner.OrderEmpty(carTypeFilter, load, quantity, orderTag, noPayment);
    public float QuantityOnOrder(Load load) => inner.QuantityOnOrder(load);
    public int NumberOfCarsOnOrderForTag(string tag) => inner.NumberOfCarsOnOrderForTag(tag);
    public float AvailableCapacityInCars(Load load) => inner.AvailableCapacityInCars(load);
    public float AvailableToDraw(Load load) => inner.AvailableToDraw(load);
    public void FulfillOrders(List<IOrder> orders, int maxToOrder, out OrderFulfillmentReport report) => inner.FulfillOrders(orders, maxToOrder, out report);
    public void AddToStorage(Load load, float quantity, float maxQuantity) => inner.AddToStorage(load, quantity, maxQuantity);
    public void RemoveFromStorage(Load load, float quantity) => inner.RemoveFromStorage(load, quantity);
    public float QuantityInStorage(Load load) => inner.QuantityInStorage(load);
    public void PayWaybill(IOpsCar car, Waybill waybill) => inner.PayWaybill(car, waybill);
    public void PayAccruedFreight(Load load, float quantity, float basis, float contractMultiplier) => inner.PayAccruedFreight(load, quantity, basis, contractMultiplier);
    public FreightLane? BillableDeliveryBasis(string originId) => inner.BillableDeliveryBasis(originId);
    public void RequestIndustriesOrderCars() => inner.RequestIndustriesOrderCars();
    public GameDateTime GetDateTime(string key, GameDateTime defaultValue) => inner.GetDateTime(key, defaultValue);
    public void SetDateTime(string key, GameDateTime dateTime) => inner.SetDateTime(key, dateTime);
    public float CounterIncrement(string key, float value) => inner.CounterIncrement(key, value);
    public float CounterClear(string key) => inner.CounterClear(key);
    public float GetFloat(string key, float defaultValue) => inner.GetFloat(key, defaultValue);
    public void SetFloat(string key, float value) => inner.SetFloat(key, value);
}
