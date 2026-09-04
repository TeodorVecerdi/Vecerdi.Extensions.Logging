using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace Vecerdi.Extensions.Logging;

public static class UnityLoggerExtensions {
    /// <summary>
    /// Adds the Unity console as a logging provider. When the builder also has configuration attached
    /// (<c>builder.AddConfiguration(configuration.GetSection("Logging"))</c>), the provider's
    /// <see cref="UnityLoggerOptions"/> bind from <c>Logging:Unity</c> and follow reloads, the same way
    /// the built-in console provider binds from <c>Logging:Console</c>. <paramref name="configure"/>
    /// runs after that binding and wins.
    /// </summary>
    public static ILoggingBuilder AddUnityLogging(this ILoggingBuilder builder, Action<UnityLoggerOptions>? configure = null) {
        builder.AddConfiguration();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, UnityLoggerProvider>());
        LoggerProviderOptions.RegisterProviderOptions<UnityLoggerOptions, UnityLoggerProvider>(builder.Services);

        if (configure is not null) {
            builder.Services.Configure(configure);
        }

        return builder;
    }
}
