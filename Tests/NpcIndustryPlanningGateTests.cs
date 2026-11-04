using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public sealed class NpcIndustryPlanningGateTests
{
    [Test]
    public void FirstDayWaitsForRequestedInitialContracts()
    {
        var gate = new NpcIndustryPlanningGate();
        Assert.That(gate.CanPlan(0, true, false, false), Is.False);
        Assert.That(gate.CanPlan(0, true, true, true), Is.False);
        gate.Begin();
        Assert.That(gate.CanPlan(0, true, true, false), Is.False);
        gate.Complete(0);
        Assert.That(gate.CanPlan(0, true, true, false), Is.True);
    }

    [Test]
    public void MidnightCannotUsePreviousDaysEvaluation()
    {
        var gate = new NpcIndustryPlanningGate();
        gate.Complete(3);
        Assert.That(gate.CanPlan(4, true, true, false), Is.False);
        gate.Begin();
        Assert.That(gate.CanPlan(4, true, true, false), Is.False);
        gate.Complete(4);
        Assert.That(gate.CanPlan(4, true, true, false), Is.True);
    }

    [Test]
    public void RestoredSaveCanPlanWithoutWaitingForAnotherMidnight()
    {
        var gate = new NpcIndustryPlanningGate();
        Assert.That(gate.CanPlan(5, false, true, false), Is.False);
        Assert.That(gate.CanPlan(5, true, true, false), Is.True);
    }
}
