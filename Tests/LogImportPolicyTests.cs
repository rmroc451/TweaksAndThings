using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public sealed class LogImportPolicyTests
{
    [TestCase(1000, 500)]
    [TestCase(101, 51)]
    [TestCase(0, 0)]
    [TestCase(-20, 0)]
    public void DeductHalfNormalDeliveryPayout(int payout, int fee) => Assert.That(LogImportPolicy.Fee(payout), Is.EqualTo(fee));
    [Test]
    public void NoChargeUntilDeliveryDayEndsAndNoRepeatedSettlement()
    {
        Assert.That(LogImportPolicy.Due(3, 3, false), Is.False);
        Assert.That(LogImportPolicy.Due(3, 4, false), Is.True);
        Assert.That(LogImportPolicy.Due(3, 5, true), Is.False);
    }
    [Test]
    public void BillIdentityDistinguishesSeparateLoadsOfSameCar()
    {
        Assert.That(LogImportPolicy.BillKey("car", 10, "sawmill"), Is.Not.EqualTo(LogImportPolicy.BillKey("car", 11, "sawmill")));
    }
}
