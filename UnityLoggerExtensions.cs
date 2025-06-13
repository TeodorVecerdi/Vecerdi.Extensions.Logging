using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediaVault.Logging;

public static class UnityLoggerExtensions {
    public static IServiceCollection AddUnityLogging(this IServiceCollection services, Action<UnityLoggerOptions>? configure = null) {
        services.AddSingleton<ILoggerProvider, UnityLoggerProvider>();
        if (configure != null) {
            services.Configure(configure);
        }

        return services;
    }

    public static ILoggingBuilder AddUnityLogging(this ILoggingBuilder builder, Action<UnityLoggerOptions>? configure = null) {
        builder.Services.AddSingleton<ILoggerProvider, UnityLoggerProvider>();
        if (configure != null) {
            builder.Services.Configure(configure);
        }

        return builder;
    }
}
