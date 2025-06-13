using MediaVault.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace MediaVault.Logging;

internal static class ServiceConfiguration {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConfigureServices() {
        ServiceManager.RegisterServices((services, configuration) => {
            services.AddLogging(builder => {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.ClearProviders();
                builder.AddUnity();
            });
        });
    }
}
