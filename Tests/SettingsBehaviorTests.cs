using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RMROC451.TweaksAndThings;

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
}
