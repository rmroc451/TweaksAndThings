using System.Linq;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class RandomConsistPolicyTests
{
    [TestCase(1)]
    [TestCase(9)]
    [TestCase(20)]
    [TestCase(200)]
    public void MixedCarsAndTenderPairsProduceExactCount(int count)
    {
        int[] costs = { 2, 1, 2 };
        Assert.That(RandomConsistPolicy.TryGenerate(costs, count, n => n - 1, out var choices), Is.True);
        Assert.That(choices.Sum(i => costs[i]), Is.EqualTo(count));
    }

    [Test]
    public void TenderOnlyCategoryRequiresEvenCount()
    {
        Assert.That(RandomConsistPolicy.TryGenerate(new[] { 2 }, 5, _ => 0, out _), Is.False);
        Assert.That(RandomConsistPolicy.TryGenerate(new[] { 2 }, 6, _ => 0, out var choices), Is.True);
        Assert.That(choices, Has.Count.EqualTo(3));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(201)]
    public void InvalidCountIsRejected(int count) =>
        Assert.That(RandomConsistPolicy.TryGenerate(new[] { 1 }, count, _ => 0, out _), Is.False);

    [Test]
    public void EmptyCategoryIsRejected() =>
        Assert.That(RandomConsistPolicy.TryGenerate(new int[0], 10, _ => 0, out _), Is.False);
}
