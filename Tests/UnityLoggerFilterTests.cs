using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Vecerdi.Extensions.Logging.Tests;

[TestFixture]
public sealed class UnityLoggerFilterTests {
    private static LoggerFilterOptions Options(LogLevel minLevel, params LoggerFilterRule[] rules) {
        var options = new LoggerFilterOptions { MinLevel = minLevel };
        foreach (var rule in rules) {
            options.Rules.Add(rule);
        }

        return options;
    }

    private static LoggerFilterRule Rule(string? provider, string? category, LogLevel? level) => new(provider, category, level, null);

    [Test]
    public void NoRules_FallsBackToMinLevel() {
        var options = Options(LogLevel.Warning);

        Assert.That(UnityLogger.GetEffectiveLogLevel(options, "My.Game.Mixer"), Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public void CategoryPrefixRule_AppliesToCategoriesUnderIt() {
        var options = Options(LogLevel.Information, Rule(null, "My.Game", LogLevel.Debug));

        Assert.That(UnityLogger.GetEffectiveLogLevel(options, "My.Game.Mixer"), Is.EqualTo(LogLevel.Debug));
        Assert.That(UnityLogger.GetEffectiveLogLevel(options, "Other.Thing"), Is.EqualTo(LogLevel.Information));
    }

    [Test]
    public void LongestMatchingPrefixWins_RegardlessOfRuleOrder() {
        var options = Options(
            LogLevel.Information,
            Rule(null, "My.Game.Audio", LogLevel.Trace),
            Rule(null, "My", LogLevel.Error),
            Rule(null, "My.Game", LogLevel.Warning)
        );

        Assert.That(UnityLogger.GetEffectiveLogLevel(options, "My.Game.Audio.Mixer"), Is.EqualTo(LogLevel.Trace));
        Assert.That(UnityLogger.GetEffectiveLogLevel(options, "My.Game.Video"), Is.EqualTo(LogLevel.Warning));
        Assert.That(UnityLogger.GetEffectiveLogLevel(options, "My.Other"), Is.EqualTo(LogLevel.Error));
    }

    [Test]
    public void DefaultRule_WithoutCategory_BeatsMinLevel() {
        var options = Options(LogLevel.Error, Rule(null, null, LogLevel.Debug));

        Assert.That(UnityLogger.GetEffectiveLogLevel(options, "Anything"), Is.EqualTo(LogLevel.Debug));
    }

    [Test]
    public void RuleForAnotherProvider_IsIgnored() {
        var options = Options(LogLevel.Information, Rule("Console", "My.Game", LogLevel.Trace));

        Assert.That(UnityLogger.GetEffectiveLogLevel(options, "My.Game.Mixer"), Is.EqualTo(LogLevel.Information));
    }

    [Test]
    public void RuleNamingThisProvider_ByFullNameOrShortName_Applies() {
        var byFullName = Options(LogLevel.Information, Rule(typeof(UnityLoggerProvider).FullName, "My.Game", LogLevel.Trace));
        var byShortName = Options(LogLevel.Information, Rule(nameof(UnityLoggerProvider), "My.Game", LogLevel.Debug));

        Assert.That(UnityLogger.GetEffectiveLogLevel(byFullName, "My.Game.Mixer"), Is.EqualTo(LogLevel.Trace));
        Assert.That(UnityLogger.GetEffectiveLogLevel(byShortName, "My.Game.Mixer"), Is.EqualTo(LogLevel.Debug));
    }

    [Test]
    public void RuleWithoutLevel_FallsBackToMinLevel() {
        var options = Options(LogLevel.Warning, Rule(null, "My.Game", null));

        Assert.That(UnityLogger.GetEffectiveLogLevel(options, "My.Game.Mixer"), Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public void IsEnabled_NeverForNone_AndRespectsLevel() {
        var logger = new UnityLogger("My.Game.Mixer", () => Options(LogLevel.Warning), () => new UnityLoggerOptions());

        Assert.That(logger.IsEnabled(LogLevel.None), Is.False);
        Assert.That(logger.IsEnabled(LogLevel.Information), Is.False);
        Assert.That(logger.IsEnabled(LogLevel.Warning), Is.True);
        Assert.That(logger.IsEnabled(LogLevel.Critical), Is.True);
    }

    [Test]
    public void IsEnabled_CachesLevel_UntilConfigurationChanges() {
        var current = Options(LogLevel.Warning);
        var logger = new UnityLogger("My.Game.Mixer", () => current, () => new UnityLoggerOptions());

        Assert.That(logger.IsEnabled(LogLevel.Information), Is.False);

        current = Options(LogLevel.Trace);
        Assert.That(logger.IsEnabled(LogLevel.Information), Is.False, "level is cached until the provider signals a change");

        logger.OnConfigurationChanged();
        Assert.That(logger.IsEnabled(LogLevel.Information), Is.True);
    }
}
