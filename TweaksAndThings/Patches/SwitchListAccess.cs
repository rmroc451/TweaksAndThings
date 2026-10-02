using Game.Messages;
using Game.State;
using Model;
using RMROC451.TweaksAndThings.Extensions;
using System;
using System.Linq;

namespace RMROC451.TweaksAndThings.Patches;

/// <summary>
/// Adds consist cars through the same message used by the game's switch-list UI.
/// </summary>
internal static class SwitchListAccess
{
    internal static bool TryAddConsist(Car selectedCar, string trainCrewId, out int addedCount)
    {
        var carIds = selectedCar.EnumerateCoupled()
            .Where(car => !car.MotivePower() && car.Archetype != Model.Definition.CarArchetype.Tender)
            .Select(car => car.id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        addedCount = carIds.Count;
        if (string.IsNullOrWhiteSpace(trainCrewId) || carIds.Count == 0) return false;

        StateManager.ApplyLocal(new SwitchListToggleCarIds(trainCrewId, carIds, on: true));
        return true;
    }
}
