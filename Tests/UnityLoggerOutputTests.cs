using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Vecerdi.Extensions.Logging.Tests;

/// <summary>
/// Drives a real <see cref="LoggerFactory"/> with the provider and captures what reaches Unity's log
/// pipeline, so these cover formatting, level mapping, stack-trace options and scopes end to end.
/// </summary>
[TestFixture]
public sealed class UnityLoggerOutputTests {
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

    private static ILogger CreateLogger(Action<UnityLoggerOptions>? configure = null, string category = "My.Game.Audio.Mixer") {
        var factory = LoggerFactory.Create(builder => {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddUnityLogging(options => {
                options.EnableColoredOutput = false;
                configure?.Invoke(options);
            });
        });

        return factory.CreateLogger(category);
    }

    [Test]
    public void Information_IsPlainLogEntry_WithLevelAndTrimmedCategoryHeader() {
        CreateLogger().LogInformation("hello {Name}", "world");

        Assert.That(m_Entries, Has.Count.EqualTo(1));
        Assert.That(m_Entries[0].Type, Is.EqualTo(LogType.Log));
        Assert.That(m_Entries[0].Message, Is.EqualTo("[Information, Mixer] hello world"));
    }

    [Test]
    public void Warning_MapsToWarningEntry() {
        CreateLogger().LogWarning("careful");

        Assert.That(m_Entries[0].Type, Is.EqualTo(LogType.Warning));
        Assert.That(m_Entries[0].Message, Does.StartWith("[Warning, Mixer]"));
    }

    [Test]
    public void ErrorWithoutException_MapsToErrorEntry() {
        LogAssert.Expect(LogType.Error, new Regex(@"^\[Error, Mixer\] broken$"));

        CreateLogger().LogError("broken");

        Assert.That(m_Entries[0].Type, Is.EqualTo(LogType.Error));
    }

    [Test]
    public void ErrorWithException_IsOneExceptionEntry_WithTheExceptionsTraceInTheTracePane() {
        LogAssert.Expect(LogType.Exception, new Regex(@"^LoggedException: \[Error, Mixer\] failed to load"));
        Exception thrown;
        try {
            throw new InvalidOperationException("disk on fire");
        } catch (InvalidOperationException e) {
            thrown = e;
        }

        CreateLogger().LogError(thrown, "failed to load");

        Assert.That(m_Entries, Has.Count.EqualTo(1), "exception logs must not split into two console entries");
        Assert.That(m_Entries[0].Type, Is.EqualTo(LogType.Exception));
        Assert.That(m_Entries[0].Message, Is.EqualTo("LoggedException: [Error, Mixer] failed to load\nSystem.InvalidOperationException: disk on fire"));
        Assert.That(m_Entries[0].StackTrace, Does.Contain(nameof(ErrorWithException_IsOneExceptionEntry_WithTheExceptionsTraceInTheTracePane)), "the trace pane shows where the exception was thrown");
    }

    [Test]
    public void ErrorWithNestedExceptions_RendersTheChainInnermostFirst() {
        LogAssert.Expect(LogType.Exception, new Regex("wrapped"));
        var inner = new TimeoutException("inner cause");
        var outer = new InvalidOperationException("outer", inner);

        CreateLogger().LogError(outer, "wrapped");

        Assert.That(m_Entries[0].Message, Does.EndWith("System.InvalidOperationException: outer"));
        Assert.That(m_Entries[0].StackTrace, Does.Contain("Rethrow as InvalidOperationException: outer"));
    }

    [Test]
    public void WarningWithException_StaysAWarning_WithExceptionAppended() {
        CreateLogger().LogWarning(new TimeoutException("slow"), "retrying");

        Assert.That(m_Entries[0].Type, Is.EqualTo(LogType.Warning));
        Assert.That(m_Entries[0].Message, Does.Contain("TimeoutException: slow"));
    }

    [Test]
    public void StackTraces_Always_CapturesForInformation() {
        CreateLogger(o => o.StackTraces = StackTraceMode.Always).LogInformation("traced");

        Assert.That(m_Entries[0].StackTrace, Is.Not.Empty);
    }

    [Test]
    public void StackTraces_WarningsAndErrors_SkipsInformation_KeepsWarning() {
        var logger = CreateLogger(o => o.StackTraces = StackTraceMode.WarningsAndErrors);

        logger.LogInformation("quiet");
        logger.LogWarning("loud");

        Assert.That(m_Entries[0].StackTrace, Is.Empty);
        Assert.That(m_Entries[1].StackTrace, Is.Not.Empty);
    }

    [Test]
    public void StackTraces_Never_SkipsEverything() {
        LogAssert.Expect(LogType.Error, new Regex("silent"));
        var logger = CreateLogger(o => o.StackTraces = StackTraceMode.Never);

        logger.LogInformation("silent");
        logger.LogError("silent");

        Assert.That(m_Entries[0].StackTrace, Is.Empty);
        Assert.That(m_Entries[1].StackTrace, Is.Empty);
    }

    [Test]
    public void Scopes_RenderedInline_WhenEnabled() {
        var logger = CreateLogger(o => o.IncludeScopes = true);

        using (logger.BeginScope("RequestId={RequestId}", 42))
        using (logger.BeginScope(new Dictionary<string, object?> { ["User"] = "teodor", ["Missing"] = null })) {
            logger.LogInformation("scoped");
        }

        Assert.That(m_Entries[0].Message, Is.EqualTo("[Information, Mixer] [RequestId=42 User=teodor Missing=<null>] scoped"));
    }

    [Test]
    public void Scopes_NotRendered_WhenDisabled() {
        var logger = CreateLogger(o => o.IncludeScopes = false);

        using (logger.BeginScope("RequestId={RequestId}", 42)) {
            logger.LogInformation("scoped");
        }

        Assert.That(m_Entries[0].Message, Is.EqualTo("[Information, Mixer] scoped"));
    }

    [Test]
    public void UnityObjectScope_IsNotRenderedAsText() {
        var logger = CreateLogger(o => o.IncludeScopes = true);
        var go = new GameObject("scope-context");
        try {
            using (logger.BeginScope(go)) {
                logger.LogInformation("with context");
            }

            Assert.That(m_Entries[0].Message, Is.EqualTo("[Information, Mixer] with context"));
        } finally {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void CategorySegments_Null_ShowsFullCategory() {
        CreateLogger(o => o.CategorySegments = null).LogInformation("full");

        Assert.That(m_Entries[0].Message, Is.EqualTo("[Information, My.Game.Audio.Mixer] full"));
    }

    [Test]
    public void ProviderRules_UnderUnityAlias_AreHonoured() {
        var factory = LoggerFactory.Create(builder => {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddFilter<UnityLoggerProvider>("My.Game", LogLevel.Warning);
            builder.AddUnityLogging(o => o.EnableColoredOutput = false);
        });

        var logger = factory.CreateLogger("My.Game.Audio.Mixer");
        logger.LogInformation("filtered out");
        logger.LogWarning("kept");

        Assert.That(m_Entries, Has.Count.EqualTo(1));
        Assert.That(m_Entries[0].Message, Does.EndWith("kept"));
    }

    [Test]
    public void UnityLoggers_SurviveTheFactoryBeingDisposed_ByFallingBack() {
        var logger = UnityLoggers.For("Test.Static.Disposed");
        var factory = LoggerFactory.Create(builder => builder.AddUnityLogging(o => o.EnableColoredOutput = false));

        try {
            UnityLoggers.Initialize(factory);
            logger.LogInformation("through the container");
            factory.Dispose();

            // The host disposed its container without calling Initialize(null) first (MediaVault does exactly
            // this while play mode is exiting). Logging must not throw, and must land via the fallback.
            Assert.DoesNotThrow(() => UnityLoggers.For("Test.Static.Disposed.Fresh").LogInformation("after dispose"));
            Assert.DoesNotThrow(() => logger.LogWarning("after dispose, cached logger"));
        } finally {
            UnityLoggers.Initialize(null);
        }

        Assert.That(m_Entries, Has.Count.EqualTo(3));
        Assert.That(m_Entries[1].Message, Does.EndWith("after dispose"));
        Assert.That(m_Entries[2].Message, Does.EndWith("after dispose, cached logger"));
    }

    [Test]
    public void UnityLoggers_FollowInitialize_ForAlreadyCreatedLoggers() {
        var logger = UnityLoggers.For("Test.Static.Category");
        var factory = LoggerFactory.Create(builder => {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddUnityLogging(o => {
                o.EnableColoredOutput = false;
                o.CategorySegments = null;
            });
        });

        try {
            UnityLoggers.Initialize(factory);
            logger.LogDebug("routed");

            Assert.That(m_Entries, Has.Count.EqualTo(1), "Debug is below the fallback's Information floor, so this proves the container factory is in use");
            Assert.That(m_Entries[0].Message, Is.EqualTo("[Debug, Test.Static.Category] routed"));
        } finally {
            UnityLoggers.Initialize(null);
        }

        logger.LogDebug("dropped by the fallback");
        Assert.That(m_Entries, Has.Count.EqualTo(1));
    }
}
