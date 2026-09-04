using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vecerdi.Extensions.Logging;

/// <summary>
/// Static access to loggers for code that lives outside the container: static classes, editor
/// scripts, and anything that runs before the container is built. Loggers handed out before
/// <see cref="Initialize"/> use a built-in fallback configuration and switch to the container's
/// <see cref="ILoggerFactory"/> the moment one is provided, so it is safe to keep them in static
/// fields.
/// </summary>
public static class UnityLoggerFactory {
    private static readonly ConcurrentDictionary<string, ForwardingLogger> s_Loggers = new();
    private static IServiceProvider? s_ServiceProvider;
    private static int s_Generation;

    /// <summary>
    /// Points the factory at a built container, or at nothing (<c>null</c>) to fall back to the
    /// built-in configuration again, e.g. when leaving play mode. Every logger created so far
    /// re-resolves its target on its next use.
    /// </summary>
    public static void Initialize(IServiceProvider? serviceProvider) {
        s_ServiceProvider = serviceProvider;
        Interlocked.Increment(ref s_Generation);
    }

    public static ILogger<T> CreateLogger<T>() {
        return CreateLogger<T>(typeof(T).FullName ?? typeof(T).Name);
    }

    public static ILogger<T> CreateLogger<T>(string categoryName) {
        return new GenericLogger<T>(CreateLogger(categoryName));
    }

    public static ILogger CreateLogger(string categoryName) {
        return s_Loggers.GetOrAdd(categoryName, static name => new ForwardingLogger(name));
    }

    private static ILogger ResolveLogger(string categoryName) {
        if (s_ServiceProvider?.GetService<ILoggerFactory>() is { } factory) {
            return factory.CreateLogger(categoryName);
        }

        return CreateFallbackLogger(categoryName);
    }

    private static ILogger CreateFallbackLogger(string categoryName) {
        var options = new UnityLoggerOptions {
            EnableColoredOutput = true,
            TrimNamespaces = true,
            NamespaceSegmentsToKeep = 0,
        };

        var filterOptions = new LoggerFilterOptions { MinLevel = LogLevel.Information };

        return new UnityLogger(categoryName, () => filterOptions, () => options);
    }

    /// <summary>
    /// The logger the factory hands out. It resolves its real target lazily and again whenever
    /// <see cref="Initialize"/> has been called since, so callers can cache it indefinitely.
    /// </summary>
    private sealed class ForwardingLogger(string categoryName) : ILogger {
        private ILogger? m_Target;
        private int m_TargetGeneration = -1;

        private ILogger Target {
            get {
                var generation = Volatile.Read(ref s_Generation);
                if (m_Target is null || m_TargetGeneration != generation) {
                    m_Target = ResolveLogger(categoryName);
                    m_TargetGeneration = generation;
                }

                return m_Target;
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Target.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => Target.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Target.Log(logLevel, eventId, state, exception, formatter);
    }

    // Helper wrapper for ILogger<T>
    private sealed class GenericLogger<T>(ILogger logger) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => logger.Log(logLevel, eventId, state, exception, formatter);
    }
}
