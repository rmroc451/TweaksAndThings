using System;
using System.Collections.Generic;
using System.Linq;
using KeyValue.Runtime;
using Model;
using Model.Definition;
using Model.Definition.Data;

namespace RMROC451.TweaksAndThings;

internal static class NpcServiceCaboose
{
    internal const string Marker = "RMROC451.TweaksAndThings.NpcCaboose";
    internal static bool IsServiceCar(Car car) => car != null && !car.KeyValueObject[Marker].IsNull && car.KeyValueObject[Marker].BoolValue;
    internal static CarDescriptor? Catalog()
    {
        var info = TrainController.Shared.PrefabStore.AllCarDefinitionInfos.Where(i =>
            i.Definition.VisibleInPlacer && i.Definition.Archetype == CarArchetype.Caboose)
            .OrderBy(_ => UnityEngine.Random.value).FirstOrDefault();
        if (info == null) return null;
        return new CarDescriptor(info, new CarIdent(NpcEquipmentIdentity.ReportingMark, null), string.Empty, null, false,
            new Dictionary<string, Value> { [Marker] = Value.Bool(true), [ThroughTrafficGuard.MarkerKey] = Value.Bool(true) });
    }
    internal static List<Car> TravelOrder(NpcServiceRecord service, IEnumerable<Car> cars)
    {
        return NpcServicePolicy.ConsistOrder(cars, service.PowerIds, c => c.id, IsServiceCar);
    }
}
