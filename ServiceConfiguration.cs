using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection;

namespace Vecerdi.Extensions.Logging;

internal static class ServiceConfiguration {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConfigureServices() {
        ServiceManager.RegisterServices((services, configuration) => {
            services.Configure<UnityLoggerOptions>(configuration.GetSection("UnityLogger"));
            services.AddLogging(builder => {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddUnityLogging();
            });
        });
    }
}
