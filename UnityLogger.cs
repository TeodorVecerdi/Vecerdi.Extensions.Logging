using System.Text;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace Vecerdi.Extensions.Logging;

/// <summary>
/// Writes one Unity console entry per log call. Level filtering is not done here: the
/// <see cref="ILoggerFactory"/> that wraps this logger applies the configured rules before the call
/// arrives, so this class only formats and forwards.
/// </summary>
internal sealed class UnityLogger(string categoryName, Func<UnityLoggerOptions> getOptions) : ILogger {
    private string m_Category = getOptions().FormatCategory(categoryName);
    private IExternalScopeProvider? m_ScopeProvider;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => m_ScopeProvider?.Push(state) ?? NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    internal void SetScopeProvider(IExternalScopeProvider scopeProvider) => m_ScopeProvider = scopeProvider;

    internal void OnConfigurationChanged() => m_Category = getOptions().FormatCategory(categoryName);

    [HideInCallstack]
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (!IsEnabled(logLevel)) {
            return;
        }

        var options = getOptions();
        var scopes = CollectScopes(options.IncludeScopes);
        var text = BuildText(options, logLevel, formatter(state, exception), scopes.Text, exception);

        if (exception is not null && logLevel >= LogLevel.Error) {
            // One entry, classified as an exception so crash reporting and console filters treat it as one.
            // The exception's own trace is in the text, so Unity's call-site capture would only add noise.
            Debug.LogFormat(LogType.Exception, LogOption.NoStacktrace, scopes.Context, "{0}", text);
            return;
        }

        var logType = logLevel switch {
            LogLevel.Warning => LogType.Warning,
            LogLevel.Error or LogLevel.Critical => LogType.Error,
            _ => LogType.Log,
        };

        var logOption = WantsStackTrace(options.StackTraces, logLevel) ? LogOption.None : LogOption.NoStacktrace;
        Debug.LogFormat(logType, logOption, scopes.Context, "{0}", text);
    }

    private static bool WantsStackTrace(StackTraceMode mode, LogLevel level) => mode switch {
        StackTraceMode.Always => true,
        StackTraceMode.WarningsAndErrors => level >= LogLevel.Warning,
        StackTraceMode.ErrorsOnly => level >= LogLevel.Error,
        _ => false,
    };

    private string BuildText(UnityLoggerOptions options, LogLevel logLevel, string message, string? scopes, Exception? exception) {
        var header = scopes is null ? $"[{logLevel}, {m_Category}]" : $"[{logLevel}, {m_Category}] {scopes}";
        if (Application.isEditor && options.EnableColoredOutput) {
            header = $"<b><color={LevelColor(logLevel)}>{header}</color></b>";
        }

        return exception is null ? $"{header} {message}" : $"{header} {message}\n{exception}";
    }

    private static string LevelColor(LogLevel level) => level switch {
        LogLevel.Trace => "#A8A8A8",
        LogLevel.Debug => "#C7C7C7",
        LogLevel.Information => "#62B0D9",
        LogLevel.Warning => "#FFA833",
        LogLevel.Error => "#FF465F",
        _ => "#E5558C",
    };

    /// <summary>
    /// Walks the active scopes once: a <see cref="UnityEngine.Object"/> pushed as a scope becomes the
    /// entry's context (click-to-select in the console); everything else is rendered as text when
    /// <paramref name="renderText"/> is set.
    /// </summary>
    private ScopeInfo CollectScopes(bool renderText) {
        if (m_ScopeProvider is null) {
            return default;
        }

        var walk = new ScopeWalk(renderText);
        m_ScopeProvider.ForEachScope(static (scope, walk) => walk.Visit(scope), walk);
        return new ScopeInfo(walk.Context, walk.Text);
    }

    private readonly record struct ScopeInfo(Object? Context, string? Text);

    private sealed class ScopeWalk(bool renderText) {
        private StringBuilder? m_Builder;

        public Object? Context { get; private set; }

        public string? Text => m_Builder is { Length: > 0 } builder ? $"[{builder}]" : null;

        public void Visit(object? scope) {
            if (scope is Object unityObject) {
                Context ??= unityObject;
                return;
            }

            if (!renderText) {
                return;
            }

            switch (scope) {
                case IEnumerable<KeyValuePair<string, object?>> pairs:
                    foreach (var (key, value) in pairs) {
                        // Formatted scopes (BeginScope("Id={Id}", id)) carry their template under this key.
                        if (key != "{OriginalFormat}") Append(key, value);
                    }

                    break;
                default:
                    Append(null, scope);
                    break;
            }
        }

        private void Append(string? key, object? value) {
            m_Builder ??= new StringBuilder();
            if (m_Builder.Length > 0) m_Builder.Append(' ');
            if (key is not null) m_Builder.Append(key).Append('=');
            m_Builder.Append(value ?? "<null>");
        }
    }

    private sealed class NullScope : IDisposable {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
