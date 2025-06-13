using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MediaVault.Logging;

public sealed class UnityLogger(string categoryName, Func<LoggerFilterOptions> getCurrentFilterConfig, Func<UnityLoggerOptions> getCurrentUnityConfig) : ILogger {
    private string m_TransformedCategoryName = getCurrentUnityConfig().ProcessCategoryName(categoryName);
    private LogLevel? m_CachedLevel;

    private static readonly Dictionary<LogLevel, string> s_LogLevelColors = new() {
        { LogLevel.Trace, "#A8A8A8" },
        { LogLevel.Debug, "#C7C7C7" },
        { LogLevel.Information, "#62B0D9" },
        { LogLevel.Warning, "#FFA833" },
        { LogLevel.Error, "#FF465F" },
        { LogLevel.Critical, "#E5558C" },
    };

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) {
        if (logLevel == LogLevel.None)
            return false;
        return logLevel >= GetEffectiveLogLevel();
    }

    private LogLevel GetEffectiveLogLevel() {
        if (m_CachedLevel.HasValue)
            return m_CachedLevel.Value;

        var filterConfig = getCurrentFilterConfig();
        var effectiveLevel = GetEffectiveLogLevel(filterConfig, categoryName);
        m_CachedLevel = effectiveLevel;
        return effectiveLevel;
    }

    private static LogLevel GetEffectiveLogLevel(LoggerFilterOptions config, string categoryName) {
        // Find the most specific matching rule
        LoggerFilterRule? bestMatch = null;
        var bestMatchLength = -1;

        foreach (var rule in config.Rules) {
            // Skip rules that don't apply to this provider
            if (rule.ProviderName != null && rule.ProviderName != typeof(UnityLoggerProvider).FullName && rule.ProviderName != nameof(UnityLoggerProvider))
                continue;

            // Check if category matches
            if (rule.CategoryName == null || categoryName.StartsWith(rule.CategoryName)) {
                var matchLength = rule.CategoryName?.Length ?? 0;
                if (matchLength > bestMatchLength) {
                    bestMatch = rule;
                    bestMatchLength = matchLength;
                }
            }
        }

        // Return the matched rule's level, or fall back to MinLevel
        return bestMatch?.LogLevel ?? config.MinLevel;
    }

    internal void OnConfigurationChanged() {
        m_CachedLevel = null;
        m_TransformedCategoryName = getCurrentUnityConfig().ProcessCategoryName(categoryName);
    }

    [HideInCallstack]
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var logMessage = GetLogMessage(message, m_TransformedCategoryName, logLevel);

        if (exception != null) {
            DoLogging(() => {
                Debug.LogError(logMessage);
                Debug.LogException(exception);
            });
        } else {
            var logMethod = GetUnityLogMethod(logLevel);
            DoLogging(() => logMethod(logMessage));
        }
    }

    private static Action<string> GetUnityLogMethod(LogLevel logLevel) {
        return logLevel switch {
            LogLevel.Trace or LogLevel.Debug or LogLevel.Information => Debug.Log,
            LogLevel.Warning => Debug.LogWarning,
            LogLevel.Error or LogLevel.Critical => Debug.LogError,
            LogLevel.None => throw new ArgumentException("Log level cannot be None.", nameof(logLevel)),
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null),
        };
    }

    [HideInCallstack]
    private static void DoLogging(Action logAction) {
        logAction();
    }

    private string GetLogMessage(string message, string category, LogLevel logLevel) {
        var logLevelString = logLevel.ToString();

        if (!Application.isEditor || !getCurrentUnityConfig().EnableColoredOutput) {
            return $"[{logLevelString}, {category}] {message}";
        }

        var color = s_LogLevelColors[logLevel];
        return $"<b><color={color}>[{logLevelString}, {category}]</color></b> {message}";
    }

    private sealed class NullScope : IDisposable {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
