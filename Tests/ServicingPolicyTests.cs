using NUnit.Framework;
using RMROC451.TweaksAndThings;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class ServicingPolicyTests
{
    [Test]
    public void OverdraftFee_IsTwentyPercentOnlyWhenServiceCostIsUnaffordable()
    {
        Assert.That(FeaturePolicies.CalculateServicingOverdraftFee(100, canAfford: true), Is.Zero);
        Assert.That(FeaturePolicies.CalculateServicingOverdraftFee(100, canAfford: false), Is.EqualTo(20));
        Assert.That(FeaturePolicies.CalculateServicingOverdraftFee(1, canAfford: false), Is.EqualTo(1));
        Assert.That(FeaturePolicies.CalculateServicingOverdraftFee(0, canAfford: false), Is.Zero);
    }
}
