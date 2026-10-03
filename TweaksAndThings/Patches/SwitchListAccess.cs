using Game.Messages;
using Game.State;
using Model;
using Model.Ops;
using RMROC451.TweaksAndThings.Extensions;
using System.Collections.Generic;
using System.Linq;
using UI.SwitchList;

namespace RMROC451.TweaksAndThings.Patches;

internal static class SwitchListAccess
{
    internal static bool TryAddConsist(Car selectedCar, out int addedCount) => TryAddCars(
        selectedCar.EnumerateCoupled().Where(IsEligible).ToList(), out addedCount);

    internal static bool IsEligible(Car car) =>
        (!car.MotivePower() && car.Archetype != Model.Definition.CarArchetype.Tender) ||
        car.TryGetOverrideDestination(Model.Ops.OverrideDestination.Repair, Model.Ops.OpsController.Shared, out var repair) && repair.HasValue;

    internal static bool TryAddCars(IReadOnlyList<Car> cars, out int addedCount)
    {
        addedCount = 0;
        var crew = StateManager.Shared?.PlayersManager?.MyTrainCrew;
        if (crew == null) return false;
        var ids = cars.Where(c => c != null).Select(c => c.id).Distinct()
            .Where(id => SwitchListPanel.Shared == null || !SwitchListPanel.Shared.SwitchListContains(id)).ToList();
        if (ids.Count == 0) return true;
        // This is the same network-aware command used by the native car inspector.
        StateManager.ApplyLocal(new SwitchListToggleCarIds(crew.Id, ids, true));
        var panel = SwitchListPanel.Shared;
        addedCount = StateManager.IsHost ? ids.Count(id => panel != null && panel.SwitchListContains(id)) : ids.Count;
        TweaksAndThingsPlugin.LogDiagnostic($"Switch list crew={crew.Id}: requested {ids.Count} cars, confirmed {addedCount} locally; host={StateManager.IsHost}. IDs: {string.Join(", ", ids)}");
        if (StateManager.IsHost && addedCount != ids.Count)
        {
            // Refresh from the authoritative list, including when the window was
            // first instantiated by this inspector rather than opened normally.
            Model.Ops.OpsController.Shared.SwitchListController.SendSwitchListUpdate(crew.Id);
            addedCount = ids.Count(id => panel != null && panel.SwitchListContains(id));
        }
        return !StateManager.IsHost || addedCount == ids.Count;
    }
}
