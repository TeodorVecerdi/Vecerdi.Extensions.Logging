using System.Text;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Vecerdi.Extensions.Logging;

public sealed class UnityLogger(string categoryName, Func<LoggerFilterOptions> getCurrentFilterConfig, Func<UnityLoggerOptions> getCurrentUnityConfig) : ILogger {
    private string m_TransformedCategoryName = getCurrentUnityConfig().ProcessCategoryName(categoryName);
    private LogLevel? m_CachedLevel;
    private IExternalScopeProvider? m_ScopeProvider;

    private static readonly Dictionary<LogLevel, string> s_LogLevelColors = new() {
        { LogLevel.Trace, "#A8A8A8" },
        { LogLevel.Debug, "#C7C7C7" },
        { LogLevel.Information, "#62B0D9" },
        { LogLevel.Warning, "#FFA833" },
        { LogLevel.Error, "#FF465F" },
        { LogLevel.Critical, "#E5558C" },
    };

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => m_ScopeProvider?.Push(state) ?? NullScope.Instance;

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => m_ScopeProvider = scopeProvider;

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

        var options = getCurrentUnityConfig();
        var message = formatter(state, exception);

        string? scopesText = null;
        if (options.IncludeScopes && m_ScopeProvider != null) {
            var sb = new StringBuilder();
            m_ScopeProvider.ForEachScope((scope, builder) => {
                if (scope is IEnumerable<KeyValuePair<string, object?>> kvps) {
                    foreach (var kv in kvps) {
                        AppendScopePair(builder, kv.Key, kv.Value);
                    }
                } else if (scope is IEnumerable<KeyValuePair<string, object>> kvpsNonNull) {
                    foreach (var kv in kvpsNonNull) {
                        AppendScopePair(builder, kv.Key, kv.Value);
                    }
                } else if (scope is IReadOnlyList<KeyValuePair<string, object?>> listKv) {
                    foreach (var kv in listKv) {
                        AppendScopePair(builder, kv.Key, kv.Value);
                    }
                } else {
                    AppendScopeValue(builder, scope);
                }
            }, sb);

            if (sb.Length > 0) {
                sb.Insert(0, '[');
                sb.Append(']');
                scopesText = sb.ToString();
            }
        }

        var logMessage = GetLogMessage(message, m_TransformedCategoryName, logLevel, scopesText);

        if (exception is null || logLevel < LogLevel.Error) {
            var logMethod = GetUnityLogMethod(logLevel);
            DoLogging(() => logMethod(exception is null ? logMessage : $"{logMessage}\n{exception}"));
        } else {
            DoLogging(() => {
                Debug.LogError($"{logMessage}\n[See exception in the next message]");
                Debug.LogException(exception);
            });
        }
    }

    private static void AppendScopePair(StringBuilder builder, string key, object? value) {
        if (builder.Length > 0) builder.Append(' ');
        builder.Append(key).Append('=');
        builder.Append(value is null ? "<null>" : value);
    }

    private static void AppendScopeValue(StringBuilder builder, object? value) {
        if (builder.Length > 0) builder.Append(' ');
        builder.Append(value is null ? "<null>" : value);
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

    private string GetLogMessage(string message, string category, LogLevel logLevel, string? scopes = null) {
        var logLevelString = logLevel.ToString();
        var header = $"[{logLevelString}, {category}]" + (scopes != null ? $" {scopes}" : string.Empty);

        if (!Application.isEditor || !getCurrentUnityConfig().EnableColoredOutput) {
            return $"{header} {message}";
        }

        var color = s_LogLevelColors[logLevel];
        return $"<b><color={color}>{header}</color></b> {message}";
    }

    private sealed class NullScope : IDisposable {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
