using System;
using System.Collections.Generic;
using System.Linq;
using Model;
using Model.Definition.Data;

namespace RMROC451.TweaksAndThings;

internal static class NpcEquipmentIdentity
{
    internal static string ReportingMark => string.IsNullOrWhiteSpace(TweaksAndThingsPlugin.Instance?.settings?.NpcReportingMark) ?
        "NPC" : TweaksAndThingsPlugin.Instance!.settings!.NpcReportingMark.Trim().ToUpperInvariant();

    internal static void Assign(List<CarDescriptor> descriptors)
    {
        var numbers = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        bool overridden = !string.IsNullOrWhiteSpace(TweaksAndThingsPlugin.Instance?.settings?.NpcReportingMark);
        for (int i = 0; i < descriptors.Count; i++)
        {
            var source = descriptors[i];
            string mark = overridden ? ReportingMark : source.Ident.ReportingMark;
            string number = source.Ident.RoadNumber;
            if (source.DefinitionInfo.Definition is DieselLocomotiveDefinition || source.DefinitionInfo.Definition is SteamLocomotiveDefinition)
            {
                if (!numbers.TryGetValue(mark, out var occupied)) numbers[mark] = occupied = TrainController.Shared.Cars
                    .Where(c => string.Equals(c.Ident.ReportingMark, mark, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Ident.RoadNumber).ToHashSet();
                number = NpcIdentityPolicy.LocomotiveNumber(occupied, UnityEngine.Random.Range(0, 100)) ??
                    throw new InvalidOperationException("NPC dispatch deferred: locomotive numbering unavailable for reporting mark " + mark);
            }
            descriptors[i] = new CarDescriptor(source.DefinitionInfo, new CarIdent(mark, number), source.Bardo,
                source.TrainCrewId, source.Flipped, source.Properties);
        }
    }
}
