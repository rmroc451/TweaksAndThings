using Model.Definition;
using Model.Definition.Data;
using NUnit.Framework;
using RMROC451.TweaksAndThings.Patches;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class CabooseCrewLoadSlotTests
{
    [Test]
    public void SetupAddsOneCrewSlotAndPreservesOtherLoads()
    {
        var definition = new CarDefinition { Archetype = CarArchetype.Caboose };
        var other = new LoadSlot(LoadUnits.Pounds, 100, "custom");
        definition.LoadSlots.Add(other);
        CabooseCrewLoadSlot_Patch.EnsureCrewLoadSlot(definition);
        CabooseCrewLoadSlot_Patch.EnsureCrewLoadSlot(definition);
        Assert.That(definition.LoadSlots, Has.Count.EqualTo(2));
        Assert.That(definition.LoadSlots[0], Is.SameAs(other));
        Assert.That(definition.LoadSlots[1].RequiredLoadIdentifier, Is.EqualTo("crew-hours"));
        Assert.That(definition.LoadSlots[1].MaximumCapacity, Is.EqualTo(8));
    }

    [Test]
    public void ExistingCustomizedCrewCapacityIsRetained()
    {
        var definition = new CarDefinition { Archetype = CarArchetype.Caboose };
        definition.LoadSlots.Add(new LoadSlot(LoadUnits.Quantity, 24, "crew-hours"));
        CabooseCrewLoadSlot_Patch.EnsureCrewLoadSlot(definition);
        Assert.That(definition.LoadSlots, Has.Count.EqualTo(1));
        Assert.That(definition.LoadSlots[0].MaximumCapacity, Is.EqualTo(24));
    }

    [Test]
    public void FreightDoesNotReceiveCrewLoad()
    {
        var definition = new CarDefinition { Archetype = CarArchetype.Freight };
        CabooseCrewLoadSlot_Patch.EnsureCrewLoadSlot(definition);
        Assert.That(definition.LoadSlots, Is.Empty);
    }
}
