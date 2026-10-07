using System.Collections.Generic;
using NUnit.Framework;

namespace RMROC451.TweaksAndThings.Tests;

public class NpcIdentityPolicyTests
{
    [Test]
    public void NumberingFills18xxThenAdvancesThroughLaterCenturiesWithoutDuplicates()
    {
        var occupied = new HashSet<string> { "1830", "1846", "1889" };
        for (int i = 0; i < 97; i++)
        {
            var number = NpcIdentityPolicy.LocomotiveNumber(occupied, 30);
            Assert.That(number, Is.Not.Null);
            Assert.That(int.Parse(number!), Is.InRange(1800, 1899));
        }
        Assert.That(occupied.Count, Is.EqualTo(100));
        Assert.That(NpcIdentityPolicy.LocomotiveNumber(occupied, 30), Is.EqualTo("1930"));
        for (int i = 0; i < 99; i++) Assert.That(int.Parse(NpcIdentityPolicy.LocomotiveNumber(occupied, 30)!), Is.InRange(1900, 1999));
        Assert.That(NpcIdentityPolicy.LocomotiveNumber(occupied, 30), Is.EqualTo("2030"));
    }
}
