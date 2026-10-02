using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RMROC451.TweaksAndThings;
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
