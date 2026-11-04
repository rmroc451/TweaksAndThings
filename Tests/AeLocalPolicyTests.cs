using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class AeLocalPolicyTests
{
    [TestCase(16, new[] { 16, 8, 4, 2, 1 })]
    [TestCase(7, new[] { 7, 4, 2, 1 })]
    [TestCase(1, new[] { 1 })]
    [TestCase(0, new int[] { })]
    public void OversizedWorkingCutsAreHalvedWithoutDroppingTheSingleCarAttempt(int count, int[] expected) =>
        Assert.That(AeLocalPolicy.BinaryCuts(count), Is.EqualTo(expected));
    [Test]
    public void RetrievingOneDeferredStringPreservesTheOtherStrings()
    {
        Assert.That(AeLocalPolicy.DeferredCars(new[] { "a", "b", "c", "d" }, new[] { "e", "e" }, new[] { "a", "b" }),
            Is.EqualTo(new[] { "c", "d", "e" }));
    }
    [Test]
    public void SplittingSeveralTimesKeepsEveryUnworkedCarOnce()
    {
        var deferred = AeLocalPolicy.DeferredCars(new[] { "old" }, new[] { "tail1", "tail2" }, System.Array.Empty<string>());
        deferred = AeLocalPolicy.DeferredCars(deferred, new[] { "tail2", "tail3" }, System.Array.Empty<string>());
        Assert.That(deferred, Is.EqualTo(new[] { "old", "tail1", "tail2", "tail3" }));
    }
    [TestCase(false, false, false, true)]
    [TestCase(true, true, false, true)]
    [TestCase(true, false, true, true)]
    [TestCase(true, false, false, false)]
    public void CompanyWaybillsDeliverWithoutAnIndustryCheckboxWhileUnselectedReturnPickupsStillGoHome(bool pickup, bool direct, bool selected, bool expected) =>
        Assert.That(AeLocalPolicy.DeliverToWaybill(pickup, direct, selected), Is.EqualTo(expected));
    [TestCase(35, false, false, 0)]
    [TestCase(20, false, true, 20)]
    [TestCase(35, true, true, 3)]
    [TestCase(2, true, true, 2)]
    [TestCase(0, false, true, 0)]
    [TestCase(70, false, true, 35)]
    public void SpeedChangesKeepSetupHeldAndCouplingSlow(int requested, bool coupling, bool moving, int expected) =>
        Assert.That(AeLocalPolicy.MoveSpeed(requested, coupling, moving), Is.EqualTo(expected));
    [Test]
    public void SortingNeverExtractsAnInaccessibleCarFromTheMiddle()
    {
        Assert.That(AeLocalPolicy.LeadingCut(new[] { ("a", "mill"), ("b", "mill"), ("c", "paper"), ("d", "mill") }), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(AeLocalPolicy.LeadingCut(new[] { ("retained", "yard"), ("delivery", "mill") }), Is.EqualTo(new[] { "retained" }));
        Assert.That(AeLocalPolicy.LeadingCut(System.Array.Empty<(string, string)>()), Is.Empty);
    }
    [TestCase("mill.logs", "mill.logs", false, false)]
    [TestCase("mill.logs", "yard", false, true)]
    [TestCase("mill.logs", "yard", true, false)]
    [TestCase("", "yard", false, false)]
    public void PickupsRequireAnUncompletedOutboundWaybill(string position, string destination, bool completed, bool expected) =>
        Assert.That(AeLocalPolicy.ReadyPickup(position, destination, completed), Is.EqualTo(expected));
    [TestCase(0, 0, 0)]
    [TestCase(1, 4, 25)]
    [TestCase(4, 4, 100)]
    [TestCase(5, 4, 100)]
    public void ProgressTracksCompletedFreightTasks(int completed, int total, int expected) =>
        Assert.That(AeLocalPolicy.Progress(completed, total), Is.EqualTo(expected));
}
