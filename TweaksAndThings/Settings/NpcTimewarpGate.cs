namespace RMROC451.TweaksAndThings;

internal sealed class NpcTimewarpGate
{
    private long generation;
    internal long Start() => ++generation;
    internal void Cancel() => generation++;
    internal bool IsCurrent(long ticket) => ticket == generation;
}
