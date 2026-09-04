using NUnit.Framework;

namespace Vecerdi.Extensions.Logging.Tests;

[TestFixture]
public sealed class UnityLoggerOptionsTests {
    [Test]
    public void ProcessCategoryName_TrimmingDisabled_ReturnsInput() {
        var options = new UnityLoggerOptions { TrimNamespaces = false };

        Assert.That(options.ProcessCategoryName("My.Game.Audio.Mixer"), Is.EqualTo("My.Game.Audio.Mixer"));
    }

    [Test]
    public void ProcessCategoryName_KeepZeroSegments_ReturnsClassName() {
        var options = new UnityLoggerOptions { TrimNamespaces = true, NamespaceSegmentsToKeep = 0 };

        Assert.That(options.ProcessCategoryName("My.Game.Audio.Mixer"), Is.EqualTo("Mixer"));
    }

    [Test]
    public void ProcessCategoryName_KeepOneSegment_ReturnsLastNamespaceAndClass() {
        var options = new UnityLoggerOptions { TrimNamespaces = true, NamespaceSegmentsToKeep = 1 };

        Assert.That(options.ProcessCategoryName("My.Game.Audio.Mixer"), Is.EqualTo("Audio.Mixer"));
    }

    [Test]
    public void ProcessCategoryName_KeepMoreSegmentsThanExist_ReturnsInput() {
        var options = new UnityLoggerOptions { TrimNamespaces = true, NamespaceSegmentsToKeep = 10 };

        Assert.That(options.ProcessCategoryName("My.Game.Audio.Mixer"), Is.EqualTo("My.Game.Audio.Mixer"));
    }

    [Test]
    public void ProcessCategoryName_NegativeSegments_ReturnsInput() {
        var options = new UnityLoggerOptions { TrimNamespaces = true, NamespaceSegmentsToKeep = -1 };

        Assert.That(options.ProcessCategoryName("My.Game.Audio.Mixer"), Is.EqualTo("My.Game.Audio.Mixer"));
    }

    [Test]
    public void ProcessCategoryName_NoNamespace_ReturnsInput() {
        var options = new UnityLoggerOptions { TrimNamespaces = true, NamespaceSegmentsToKeep = 0 };

        Assert.That(options.ProcessCategoryName("Mixer"), Is.EqualTo("Mixer"));
    }

    [Test]
    public void ProcessCategoryName_TrailingDot_ReturnsInput() {
        var options = new UnityLoggerOptions { TrimNamespaces = true, NamespaceSegmentsToKeep = 0 };

        Assert.That(options.ProcessCategoryName("My.Game."), Is.EqualTo("My.Game."));
    }

    [Test]
    public void ProcessCategoryName_GenericCategory_KeepsGenericSuffix() {
        var options = new UnityLoggerOptions { TrimNamespaces = true, NamespaceSegmentsToKeep = 0 };

        Assert.That(options.ProcessCategoryName("My.Game.Repository`1"), Is.EqualTo("Repository`1"));
    }
}
