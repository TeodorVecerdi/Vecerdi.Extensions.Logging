using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Vecerdi.Extensions.Logging;

/// <summary>
/// Loggers for code that lives outside a container: static classes, editor scripts, and anything
/// that runs before the container is built. Until <see cref="Initialize"/> hands over an
/// <see cref="ILoggerFactory"/>, loggers come from a built-in factory (Unity console, Information and
/// above); afterwards every logger obtained here, including ones already stored in static fields,
/// forwards to the provided factory. If that factory is disposed before <see cref="Initialize"/> is
/// called with <c>null</c>, the loggers fall back on their own.
/// </summary>
public static class UnityLoggers {
    private static readonly ConcurrentDictionary<string, ForwardingLogger> s_Loggers = new();
    private static readonly Lazy<ILoggerFactory> s_Fallback = new(() => LoggerFactory.Create(builder => {
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddUnityLogging();
    }));

    private static ILoggerFactory? s_Factory;
    private static int s_Generation;

    /// <summary>
    /// Routes all loggers to <paramref name="factory"/>, or back to the built-in fallback when
    /// <c>null</c> (e.g. when leaving play mode with domain reload disabled).
    /// </summary>
    public static void Initialize(ILoggerFactory? factory) {
        s_Factory = factory;
        Interlocked.Increment(ref s_Generation);
    }

    /// <summary>A logger whose category is <typeparamref name="T"/>'s full name, as <c>ILogger&lt;T&gt;</c> would name it.</summary>
    public static ILogger<T> For<T>() => new TypedLogger<T>(For(CategoryName(typeof(T))));

    /// <summary>A logger for an explicit category.</summary>
    public static ILogger For(string categoryName) => s_Loggers.GetOrAdd(categoryName, static name => new ForwardingLogger(name));

    /// <summary>Mirrors how <c>Logger&lt;T&gt;</c> names categories: full name, generic arity and arguments stripped, nested types joined with dots.</summary>
    internal static string CategoryName(Type type) {
        var name = type.FullName ?? type.Name;
        var backtick = name.IndexOf('`');
        if (backtick >= 0) {
            name = name[..backtick];
        }

        return name.Replace('+', '.');
    }

    private static ILogger Resolve(string categoryName) {
        if (s_Factory is { } factory) {
            try {
                return factory.CreateLogger(categoryName);
            } catch (ObjectDisposedException) {
                // The host tore its container down without telling us (or before it could). Behave as if
                // Initialize(null) had been called so every logger drops back to the fallback.
                if (ReferenceEquals(s_Factory, factory)) {
                    Initialize(null);
                }
            }
        }

        return s_Fallback.Value.CreateLogger(categoryName);
    }

    /// <summary>Re-resolves its target whenever <see cref="Initialize"/> has been called since it last looked.</summary>
    private sealed class ForwardingLogger(string categoryName) : ILogger {
        private ILogger? m_Target;
        private int m_TargetGeneration = -1;

        private ILogger Target {
            get {
                var generation = Volatile.Read(ref s_Generation);
                if (m_Target is null || m_TargetGeneration != generation) {
                    m_Target = Resolve(categoryName);
                    m_TargetGeneration = Volatile.Read(ref s_Generation);
                }

                return m_Target;
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Target.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => Target.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Target.Log(logLevel, eventId, state, exception, formatter);
    }

    private sealed class TypedLogger<T>(ILogger logger) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => logger.Log(logLevel, eventId, state, exception, formatter);
    }
}
