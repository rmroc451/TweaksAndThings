using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcTimewarpGateTests
{
    [Test] public void SuccessfulSpawnCancelsCurrentWait()
    {
        var gate = new NpcTimewarpGate();
        long wait = gate.Start();
        Assert.That(gate.IsCurrent(wait), Is.True);
        gate.Cancel();
        Assert.That(gate.IsCurrent(wait), Is.False);
    }

    [Test] public void CanceledWaitCannotResumeWhenAnotherWaitStarts()
    {
        var gate = new NpcTimewarpGate();
        long first = gate.Start();
        gate.Cancel();
        long next = gate.Start();
        Assert.That(gate.IsCurrent(first), Is.False);
        Assert.That(gate.IsCurrent(next), Is.True);
    }

    [Test] public void SeveralSimultaneousSpawnsKeepWaitCanceled()
    {
        var gate = new NpcTimewarpGate();
        long wait = gate.Start();
        gate.Cancel();
        gate.Cancel();
        Assert.That(gate.IsCurrent(wait), Is.False);
    }
}
