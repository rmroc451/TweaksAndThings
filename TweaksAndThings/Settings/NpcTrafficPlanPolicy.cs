using System;
using System.Linq;

namespace RMROC451.TweaksAndThings;

internal static class NpcTrafficPlanPolicy
{
    internal static string ServiceKey(string id, double due) => id + ":" + Math.Round(due).ToString(System.Globalization.CultureInfo.InvariantCulture);
    internal static bool Owns(NpcServiceState state, string symbol) =>
        state.Services.Any(s => s.TrainSymbol == symbol || s.ReturnTrainSymbol == symbol) ||
        state.TrafficPlans.Any(p => p.Symbol == symbol || p.ReturnSymbol.Length > 0 && p.ReturnSymbol == symbol) ||
        state.RandomDepartures.Any(d => d.Symbol == symbol);

    internal static NpcTrafficPlan? ForInterchange(NpcServiceState state, string id, double due) =>
        state.TrafficPlans.FirstOrDefault(p => !p.Dispatched && p.InterchangeId == id && Math.Abs(p.ServiceTime - due) <= 2);

    internal static bool InPlanningDay(double dispatch, double service, double now)
    {
        double start = Math.Floor(now / 86400) * 86400;
        // Include next morning's service if its train has to depart tonight.
        return service >= start && dispatch < start + 86400;
    }
}
