using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Vecerdi.Logging.Unity;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MediaVault.Logging;

public sealed class UnityLogger(string categoryName) : ILogger {
    private readonly string m_TransformedCategoryName = Settings.TransformCategoryName(categoryName);

    [field: AllowNull, MaybeNull]
    private static LoggingSettings Settings => field ??= LoggingSettings.GetOrCreateSettings();

#if UNITY_EDITOR
    private static readonly Dictionary<LogLevel, string> s_LogLevelColors = new() {
        { LogLevel.Trace, "#A8A8A8" },
        { LogLevel.Debug, "#C7C7C7" },
        { LogLevel.Information, "#62B0D9" },
        { LogLevel.Warning, "#FFA833" },
        { LogLevel.Error, "#FF465F" },
        { LogLevel.Critical, "#E5558C" },
    };
#endif

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) {
        var unityLogLevel = (Vecerdi.Logging.LogLevel)logLevel;
        if (Settings.LogCategoriesByName.TryGetValue(categoryName, out var logCategory)) {
            return unityLogLevel >= logCategory.LogLevel;
        }

        return true;
    }

    [HideInCallstack]
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var logMessage = GetLogMessage(message, m_TransformedCategoryName, logLevel);

        if (exception != null) {
            DoLogging(() => Debug.LogError(logMessage), () => Debug.LogException(exception));
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

    private static void DoLogging(Action logAction, Action? exceptionAction = null) {
        if (!PlayerLoopHelper.IsMainThread && Settings.LogMessagesOnMainThread && Application.isPlaying) {
            PlayerLoopHelper.UnitySynchronizationContext.Post(_ => {
                logAction();
                exceptionAction?.Invoke();
            }, null);
            return;
        }

        logAction();
        exceptionAction?.Invoke();
    }

    private static string GetLogMessage(string message, string category, LogLevel logLevel) {
        var logLevelString = logLevel.ToString();

#if !UNITY_EDITOR
            return $"[{logLevelString}, {category}] {message}";
#else
        if (Settings.EnableColoredOutputInEditor) {
            var color = s_LogLevelColors[logLevel];
            return $"<b><color={color}>[{logLevelString}, {category}]</color></b> {message}";
        }

        return $"[{logLevelString}, {category}] {message}";
#endif
    }

    private sealed class NullScope : IDisposable {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
