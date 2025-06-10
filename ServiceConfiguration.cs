using MediaVault.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace MediaVault.Logging;

internal static class ServiceConfiguration {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConfigureServices() {
        ServiceManager.ConfigureServices((services, configuration) => {
            services.AddLogging(builder => {
                builder.ClearProviders();
                builder.AddUnity();
                builder.AddConfiguration(configuration.GetSection("Logging"));
            });
        });
    }
}
