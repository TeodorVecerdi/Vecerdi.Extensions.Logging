using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using UnityEngine;

namespace Vecerdi.Extensions.Logging.Tests;

/// <summary>Binding from the <c>Logging:Unity</c> section, both the provider-specific level rules and the output options.</summary>
[TestFixture]
public sealed class UnityLoggerConfigurationTests {
    private readonly List<(LogType Type, string Message, string StackTrace)> m_Entries = [];

    [SetUp]
    public void SetUp() {
        m_Entries.Clear();
        Application.logMessageReceived += OnLog;
    }

    [TearDown]
    public void TearDown() {
        Application.logMessageReceived -= OnLog;
    }

    private void OnLog(string message, string stackTrace, LogType type) => m_Entries.Add((type, message, stackTrace));

    private static ILoggerFactory FactoryFrom(Dictionary<string, string?> values) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return LoggerFactory.Create(builder => {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddUnityLogging();
        });
    }

    [Test]
    public void OptionsBindFromLoggingUnitySection() {
        var factory = FactoryFrom(new Dictionary<string, string?> {
            ["Logging:LogLevel:Default"] = "Trace",
            ["Logging:Unity:EnableColoredOutput"] = "false",
            ["Logging:Unity:CategorySegments"] = "2",
            ["Logging:Unity:IncludeScopes"] = "true",
            ["Logging:Unity:StackTraces"] = "Never",
        });

        var logger = factory.CreateLogger("My.Game.Audio.Mixer");
        using (logger.BeginScope(new Dictionary<string, object?> { ["Take"] = 3 })) {
            logger.LogInformation("bound");
        }

        Assert.That(m_Entries, Has.Count.EqualTo(1));
        Assert.That(m_Entries[0].Message, Is.EqualTo("[Information, Audio.Mixer] [Take=3] bound"));
        Assert.That(m_Entries[0].StackTrace, Is.Empty);
    }

    [Test]
    public void ProviderLevelRules_UnderTheUnityAlias_Apply() {
        var factory = FactoryFrom(new Dictionary<string, string?> {
            ["Logging:LogLevel:Default"] = "Trace",
            ["Logging:Unity:LogLevel:My.Game"] = "Warning",
            ["Logging:Unity:EnableColoredOutput"] = "false",
        });

        var logger = factory.CreateLogger("My.Game.Audio.Mixer");
        logger.LogInformation("filtered by the alias rule");
        logger.LogWarning("kept");

        Assert.That(m_Entries, Has.Count.EqualTo(1));
        Assert.That(m_Entries[0].Message, Does.EndWith("kept"));
    }

    [Test]
    public void CodeConfiguration_RunsAfterBinding_AndWins() {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Logging:LogLevel:Default"] = "Trace",
            ["Logging:Unity:EnableColoredOutput"] = "false",
            ["Logging:Unity:CategorySegments"] = "2",
        }).Build();

        var factory = LoggerFactory.Create(builder => {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddUnityLogging(options => options.CategorySegments = null);
        });

        factory.CreateLogger("My.Game.Audio.Mixer").LogInformation("full");

        Assert.That(m_Entries[0].Message, Is.EqualTo("[Information, My.Game.Audio.Mixer] full"));
    }
}
