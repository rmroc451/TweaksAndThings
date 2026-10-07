namespace RMROC451.TweaksAndThings;

// Runtime state only: saved schedules must not reroll when a save is loaded.
internal sealed class NpcIndustryPlanningGate
{
    private int readyDay = int.MinValue;
    private bool evaluating;
    internal void Begin() => evaluating = true;
    internal void Complete(int day) { readyDay = day; evaluating = false; }
    internal bool CanPlan(int day, bool restored, bool hasIndustries, bool initialContractsPending)
    {
        if (!restored || !hasIndustries || evaluating || initialContractsPending) return false;
        // A loaded save (or enabling the mod mid-session) need not wait for tomorrow.
        if (readyDay == int.MinValue && !initialContractsPending) readyDay = day;
        return readyDay == day;
    }
}
