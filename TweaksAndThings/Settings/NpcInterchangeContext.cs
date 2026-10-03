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
    public bool OrderLoad(CarTypeFilter carTypeFilter, Load load, string orderTag, bool noPayment, out float quantity) => inner.OrderLoad(carTypeFilter, load, orderTag, noPayment, out quantity);
    public void OrderEmpty(CarTypeFilter carTypeFilter, string orderTag, bool noPayment) => inner.OrderEmpty(carTypeFilter, orderTag, noPayment);
    public float QuantityOnOrder(Load load) => inner.QuantityOnOrder(load);
    public int NumberOfCarsOnOrder(Load load) => inner.NumberOfCarsOnOrder(load);
    public int NumberOfCarsOnOrderForTag(string tag) => inner.NumberOfCarsOnOrderForTag(tag);
    public int NumberOfCarsOnOrderEntireIndustry() => inner.NumberOfCarsOnOrderEntireIndustry();
    public int NumberOfCarsOnOrderEmpties(CarTypeFilter carTypes) => inner.NumberOfCarsOnOrderEmpties(carTypes);
    public float AvailableCapacityInCars(CarTypeFilter carTypeFilter, Load load) => inner.AvailableCapacityInCars(carTypeFilter, load);
    public void AddToStorage(Load load, float quantity, float maxQuantity) => inner.AddToStorage(load, quantity, maxQuantity);
    public void RemoveFromStorage(Load load, float quantity) => inner.RemoveFromStorage(load, quantity);
    public float QuantityInStorage(Load load) => inner.QuantityInStorage(load);
    public void PayWaybill(IOpsCar car, Waybill waybill) => inner.PayWaybill(car, waybill);
    public void PayLoad(Load load, float quantity) => inner.PayLoad(load, quantity);
    public void RequestIndustriesOrderCars() => inner.RequestIndustriesOrderCars();
    public GameDateTime GetDateTime(string key, GameDateTime defaultValue) => inner.GetDateTime(key, defaultValue);
    public void SetDateTime(string key, GameDateTime dateTime) => inner.SetDateTime(key, dateTime);
    public float CounterIncrement(string key, float value) => inner.CounterIncrement(key, value);
    public float CounterClear(string key) => inner.CounterClear(key);
}
