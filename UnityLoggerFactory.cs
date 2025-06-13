using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vecerdi.Extensions.Logging;

public static class UnityLoggerFactory {
    private static IServiceProvider? s_ServiceProvider;
    private static readonly ConcurrentDictionary<string, ILogger> s_LoggerCache = new();

    public static void Initialize(IServiceProvider serviceProvider) {
        s_ServiceProvider = serviceProvider;
    }

    public static ILogger<T> CreateLogger<T>() {
        return CreateLogger<T>(typeof(T).FullName ?? typeof(T).Name);
    }

    public static ILogger<T> CreateLogger<T>(string categoryName) {
        var logger = CreateLogger(categoryName);
        return new GenericLogger<T>(logger);
    }

    public static ILogger CreateLogger(string categoryName) {
        return s_LoggerCache.GetOrAdd(categoryName, name => {
            // Try to use DI if available
            if (s_ServiceProvider?.GetService<ILoggerFactory>() is { } factory) {
                return factory.CreateLogger(name);
            }

            // Fallback to manual creation
            return CreateFallbackLogger(name);
        });
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

    // Helper wrapper for ILogger<T>
    private class GenericLogger<T>(ILogger logger) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => logger.Log(logLevel, eventId, state, exception, formatter);
    }
}
