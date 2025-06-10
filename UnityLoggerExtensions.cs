using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vecerdi.Logging.Unity;

namespace MediaVault.Logging;

public static class UnityLoggerExtensions {
    public static ILoggingBuilder AddUnity(this ILoggingBuilder builder) {
        var settings = LoggingSettings.GetOrCreateSettings();
        var logLevel = (LogLevel)settings.GlobalLogLevel;

        builder.Services.AddSingleton<ILoggerProvider, UnityLoggerProvider>();
        builder.SetMinimumLevel(logLevel);
        return builder;
    }
    public static ILoggingBuilder AddUnity(this ILoggingBuilder builder, LogLevel logLevel) {
        builder.Services.AddSingleton<ILoggerProvider, UnityLoggerProvider>();
        builder.SetMinimumLevel(logLevel);
        return builder;
    }
}
