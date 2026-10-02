using System.Collections.Generic;
using System.Linq;
using Game.Messages;
using Game.State;
using NUnit.Framework;
using RMROC451.TweaksAndThings;
using RMROC451.TweaksAndThings.Patches;
using UnityEngine;
using UnityModManagerNet;

namespace RMROC451.TweaksAndThings.Tests;

[TestFixture]
public sealed class SettingsBehaviorTests
{
    [Test]
    public void SanitizeEmptySettings_NullList_ReturnsOneEditableBlankRow()
    {
        var result = SettingsExtensions.SanitizeEmptySettings(null);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].WebhookUrl, Is.Empty);
        Assert.That(result[0].RailroadMark, Is.Empty);
        Assert.That(result[0].WebhookEnabled, Is.False);
    }

    [Test]
    public void SanitizeEmptySettings_KeepsConfiguredRowsInOrderAndAppendsBlankRow()
    {
        var first = new WebhookSettings(true, "ABC", "https://example.test/one");
        var second = new WebhookSettings(false, "XYZ", "https://example.test/two");
        var result = SettingsExtensions.SanitizeEmptySettings(
            new[] { first, new WebhookSettings(), second });

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.SameAs(first));
        Assert.That(result[1], Is.SameAs(second));
        Assert.That(result[2].WebhookUrl, Is.Empty);
    }

    [Test]
    public void AddAnotherRow_AddsBlankOnlyWhenEveryExistingUrlIsFilled()
    {
        var settings = new Settings
        {
            WebhookSettingsList = new List<WebhookSettings>
            {
                new(true, "ABC", "https://example.test/one"),
                new(false, "XYZ", "https://example.test/two")
            }
        };

        settings.AddAnotherRow();

        Assert.That(settings.WebhookSettingsList, Has.Count.EqualTo(3));
        Assert.That(settings.WebhookSettingsList![2].WebhookUrl, Is.Empty);
    }

    [Test]
    public void AddAnotherRow_DoesNotAddDuplicateBlankRow()
    {
        var settings = new Settings
        {
            WebhookSettingsList = new List<WebhookSettings>
            {
                new(true, "ABC", "https://example.test/one"),
                new()
            }
        };

        settings.AddAnotherRow();

        Assert.That(settings.WebhookSettingsList, Has.Count.EqualTo(2));
    }

    [Test]
    public void DefaultSettings_ContainOneBlankWebhookAndRosterFuelOptions()
    {
        var settings = new Settings();

        Assert.That(settings.WebhookSettingsList, Has.Count.EqualTo(1));
        Assert.That(settings.WebhookSettingsList!.Single().WebhookUrl, Is.Empty);
        Assert.That(settings.EngineRosterFuelColumnSettings, Is.Not.Null);
    }

    [Test]
    public void DefaultHotkeyBindings_PreserveOriginalAltControlAndShiftModifiers()
    {
        var settings = new Settings();

        Assert.That(settings.ClickAltBinding.keyCode, Is.EqualTo(KeyCode.LeftAlt));
        Assert.That(settings.ClickControlBinding.keyCode, Is.EqualTo(KeyCode.LeftControl));
        Assert.That(settings.ClickShiftBinding.keyCode, Is.EqualTo(KeyCode.LeftShift));
    }

    [Test]
    public void DefaultSettings_AllowRepairWithoutWorkOrderLikeStockRailroader()
    {
        var settings = new Settings();

        Assert.That(settings.AllowRepairsWithoutWaybill, Is.True);
    }

    [Test]
    public void DefaultSettings_ShowWaypointSetNotifications()
    {
        Assert.That(new Settings().ShowWaypointSetNotifications, Is.True);
    }

    [Test]
    public void RepairPolicy_AllowsWorkOrderCarsRegardlessOfSetting()
    {
        var settings = new Settings { AllowRepairsWithoutWaybill = false };

        Assert.That(FeaturePolicies.IsRepairEligible(hasRepairWorkOrder: true, settings), Is.True);
    }

    [Test]
    public void RepairPolicy_AllowsUnwaybilledCarsByDefault()
    {
        Assert.That(FeaturePolicies.IsRepairEligible(hasRepairWorkOrder: false, new Settings()), Is.True);
    }

    [Test]
    public void RepairPolicy_CanRequireWorkOrderForRepair()
    {
        var settings = new Settings { AllowRepairsWithoutWaybill = false };

        Assert.That(FeaturePolicies.IsRepairEligible(hasRepairWorkOrder: false, settings), Is.False);
    }

    [Test]
    public void WaypointSetNotificationPolicy_CanHideNotifications()
    {
        Assert.That(FeaturePolicies.ShouldShowWaypointSetNotification(new Settings()), Is.True);
        Assert.That(FeaturePolicies.ShouldShowWaypointSetNotification(
            new Settings { ShowWaypointSetNotifications = false }), Is.False);
    }

    [Test]
    public void WaypointDestinationCache_IsInvalidatedWhenSelectedLocomotiveChanges()
    {
        Assert.That(FeaturePolicies.IsWaypointCacheForDifferentLocomotive("loco-a", "loco-a"), Is.False);
        Assert.That(FeaturePolicies.IsWaypointCacheForDifferentLocomotive("loco-a", "loco-b"), Is.True);
        Assert.That(FeaturePolicies.IsWaypointCacheForDifferentLocomotive(null, "loco-a"), Is.True);
    }

    [TestCase(true, "destination", "Mill", true)]
    [TestCase(false, "destination", "Mill", false)]
    [TestCase(true, "", "Mill", false)]
    [TestCase(true, "destination", " ", false)]
    public void WaypointDestinationPolicy_OnlyExposesResolvedNamedDestinations(
        bool locationResolved, string destinationId, string destinationName, bool expected)
    {
        Assert.That(FeaturePolicies.IsWaypointDestinationSelectable(locationResolved, destinationId, destinationName), Is.EqualTo(expected));
    }

    [TestCase(true, false, true, true)]
    [TestCase(true, true, true, false)]
    [TestCase(false, false, true, false)]
    [TestCase(true, false, false, false)]
    public void WaypointPicker_OnlyAcceptsClickedNonCancelledNewLocation(
        bool clicked, bool escaped, bool locationChanged, bool expected)
    {
        Assert.That(FeaturePolicies.ShouldAcceptWaypointPickerHit(clicked, escaped, locationChanged), Is.EqualTo(expected));
    }

    [Test]
    public void SwitchListAccess_SendsEveryCarToTheCurrentCrewSwitchList()
    {
        StateManager.LocalMessages.Clear();
        var cars = new[] { new Model.Car { id = "A" }, new Model.Car { id = "B" } };
        cars[0].Consist = cars;

        var succeeded = SwitchListAccess.TryAddConsist(cars[0], "crew-1", out var addedCount);

        Assert.That(succeeded, Is.True);
        Assert.That(addedCount, Is.EqualTo(2));
        var message = (SwitchListToggleCarIds)StateManager.LocalMessages.Single();
        Assert.That(message.TrainCrewId, Is.EqualTo("crew-1"));
        Assert.That(message.CarIds, Is.EqualTo(new[] { "A", "B" }));
        Assert.That(message.On, Is.True);
    }

    [Test]
    public void SwitchListAccess_AddsRollingStockAndSkipsLocomotivesAndTenders()
    {
        StateManager.LocalMessages.Clear();
        var locomotive = new Model.Car { id = "LOCO", IsMotivePower = true };
        var tender = new Model.Car { id = "TENDER", Archetype = Model.Definition.CarArchetype.Tender };
        var firstCar = new Model.Car { id = "A" };
        var duplicate = new Model.Car { id = "A" };
        locomotive.Consist = new[] { locomotive, tender, firstCar, duplicate };

        var succeeded = SwitchListAccess.TryAddConsist(locomotive, "crew-1", out var addedCount);

        Assert.That(succeeded, Is.True);
        Assert.That(addedCount, Is.EqualTo(1));
        var message = (SwitchListToggleCarIds)StateManager.LocalMessages.Single();
        Assert.That(message.CarIds, Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public void SwitchListAccess_AddsCarsWhenSelectedConsistHasNoLocomotive()
    {
        StateManager.LocalMessages.Clear();
        var firstCar = new Model.Car { id = "A" };
        var secondCar = new Model.Car { id = "B" };
        firstCar.Consist = new[] { firstCar, secondCar };

        var succeeded = SwitchListAccess.TryAddConsist(firstCar, "crew-1", out var addedCount);

        Assert.That(succeeded, Is.True);
        Assert.That(addedCount, Is.EqualTo(2));
        var message = (SwitchListToggleCarIds)StateManager.LocalMessages.Single();
        Assert.That(message.CarIds, Is.EqualTo(new[] { "A", "B" }));
    }

    [Test]
    public void SwitchListAccess_DoesNotSendRequestWithoutCrewOrRollingStock()
    {
        StateManager.LocalMessages.Clear();
        var locomotive = new Model.Car { id = "LOCO", IsMotivePower = true };

        Assert.That(SwitchListAccess.TryAddConsist(locomotive, "crew-1", out var emptyCount), Is.False);
        Assert.That(emptyCount, Is.Zero);
        Assert.That(SwitchListAccess.TryAddConsist(new Model.Car { id = "A" }, "", out var carCount), Is.False);
        Assert.That(carCount, Is.EqualTo(1));
        Assert.That(StateManager.LocalMessages, Is.Empty);
    }

    [Test]
    public void HotkeyBindings_KeepModifierCombinationsComposable()
    {
        KeyBinding.ControlHeld = true;
        KeyBinding.AltHeld = true;
        KeyBinding.ShiftHeld = false;
        try
        {
            Assert.That(HotkeyBindings.IsHeld(new KeyBinding { keyCode = KeyCode.LeftAlt }), Is.True);
            Assert.That(HotkeyBindings.IsHeld(new KeyBinding { keyCode = KeyCode.LeftControl }), Is.True);
            Assert.That(HotkeyBindings.IsHeld(new KeyBinding { keyCode = KeyCode.LeftShift }), Is.False);
        }
        finally
        {
            KeyBinding.ControlHeld = false;
            KeyBinding.AltHeld = false;
            KeyBinding.ShiftHeld = false;
            KeyBinding.KeyHeld = false;
        }
    }

    [Test]
    public void HotkeyBindings_SupportsRemappingAnActionToAnotherKey()
    {
        KeyBinding.KeyHeld = true;
        try
        {
            Assert.That(HotkeyBindings.IsHeld(new KeyBinding { keyCode = KeyCode.A }), Is.True);
        }
        finally
        {
            KeyBinding.KeyHeld = false;
        }
    }

    [Test]
    public void HotkeyBindings_RequiresConfiguredModifiers()
    {
        var binding = new KeyBinding { keyCode = KeyCode.A, modifiers = 1 };
        KeyBinding.KeyHeld = true;
        KeyBinding.ControlHeld = false;
        try
        {
            Assert.That(HotkeyBindings.IsHeld(binding), Is.False);
            KeyBinding.ControlHeld = true;
            Assert.That(HotkeyBindings.IsHeld(binding), Is.True);
        }
        finally
        {
            KeyBinding.KeyHeld = false;
            KeyBinding.ControlHeld = false;
        }
    }
}
