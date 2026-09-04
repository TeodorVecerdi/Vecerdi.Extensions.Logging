using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vecerdi.Extensions.Logging;

public static class UnityLoggerExtensions {
    /// <summary>Registers the Unity console as a logging provider, optionally configuring its output options in code.</summary>
    public static IServiceCollection AddUnityLogging(this IServiceCollection services, Action<UnityLoggerOptions>? configure = null) {
        services.AddSingleton<ILoggerProvider, UnityLoggerProvider>();
        if (configure != null) {
            services.Configure(configure);
        }

        return services;
    }

    /// <summary>Registers the Unity console as a logging provider, optionally configuring its output options in code.</summary>
    public static ILoggingBuilder AddUnityLogging(this ILoggingBuilder builder, Action<UnityLoggerOptions>? configure = null) {
        builder.Services.AddSingleton<ILoggerProvider, UnityLoggerProvider>();
        if (configure != null) {
            builder.Services.Configure(configure);
        }

        return builder;
    }

    /// <summary>
    /// Registers the Unity console as a logging provider and binds <see cref="UnityLoggerOptions"/> to
    /// <paramref name="unityLoggerSection"/>, so the output options come from configuration and follow
    /// reloads. Level filtering is not touched here; pair it with
    /// <c>builder.AddConfiguration(configuration.GetSection("Logging"))</c> for that.
    /// </summary>
    public static ILoggingBuilder AddUnityLogging(this ILoggingBuilder builder, IConfiguration unityLoggerSection) {
        builder.Services.AddSingleton<ILoggerProvider, UnityLoggerProvider>();
        builder.Services.Configure<UnityLoggerOptions>(unityLoggerSection);
        return builder;
    }
}
