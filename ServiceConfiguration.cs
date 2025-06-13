using MediaVault.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnityEngine;

namespace MediaVault.Logging;

internal static class ServiceConfiguration {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConfigureServices() {
        ServiceManager.RegisterServices((services, configuration) => {
            services.ConfigureWithoutInterceptors<UnityLoggerOptions>(configuration.GetSection("UnityLogger"));
            services.AddLogging(builder => {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddUnityLogging();
            });
        });
    }

    // TODO: Move this to the DI library
    public static IServiceCollection ConfigureWithoutInterceptors<TOptions>(this IServiceCollection services, IConfiguration configuration) where TOptions : class {
        services.AddOptions();
        services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(new ConfigurationChangeTokenSource<TOptions>("", configuration));
        services.AddSingleton<IConfigureOptions<TOptions>>(new NamedConfigureFromConfigurationOptions<TOptions>("", configuration, null));
        return services;
    }
}
