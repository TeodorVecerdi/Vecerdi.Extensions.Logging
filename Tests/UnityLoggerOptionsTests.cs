using System.Collections.Generic;
using NUnit.Framework;

namespace Vecerdi.Extensions.Logging.Tests;

[TestFixture]
public sealed class UnityLoggerOptionsTests {
    [TestCase(null, "My.Game.Audio.Mixer", "My.Game.Audio.Mixer")]
    [TestCase(1, "My.Game.Audio.Mixer", "Mixer")]
    [TestCase(2, "My.Game.Audio.Mixer", "Audio.Mixer")]
    [TestCase(10, "My.Game.Audio.Mixer", "My.Game.Audio.Mixer")]
    [TestCase(0, "My.Game.Audio.Mixer", "Mixer")]
    [TestCase(-3, "My.Game.Audio.Mixer", "Mixer")]
    [TestCase(1, "Mixer", "Mixer")]
    [TestCase(1, "My.Game.", "My.Game.")]
    [TestCase(1, "", "")]
    public void FormatCategory_KeepsRequestedTrailingSegments(int? segments, string category, string expected) {
        var options = new UnityLoggerOptions { CategorySegments = segments };

        Assert.That(options.FormatCategory(category), Is.EqualTo(expected));
    }

    [Test]
    public void CategoryName_PlainType_IsFullName() {
        Assert.That(UnityLoggers.CategoryName(typeof(UnityLoggerOptions)), Is.EqualTo("Vecerdi.Extensions.Logging.UnityLoggerOptions"));
    }

    [Test]
    public void CategoryName_GenericType_DropsArityAndArguments() {
        Assert.That(UnityLoggers.CategoryName(typeof(List<int>)), Is.EqualTo("System.Collections.Generic.List"));
        Assert.That(UnityLoggers.CategoryName(typeof(Dictionary<,>)), Is.EqualTo("System.Collections.Generic.Dictionary"));
    }

    [Test]
    public void CategoryName_NestedType_UsesDots() {
        Assert.That(UnityLoggers.CategoryName(typeof(Outer.Inner)), Is.EqualTo("Vecerdi.Extensions.Logging.Tests.UnityLoggerOptionsTests.Outer.Inner"));
    }

    private static class Outer {
        internal sealed class Inner;
    }
}
