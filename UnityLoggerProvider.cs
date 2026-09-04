using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vecerdi.Extensions.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> for the Unity console. Aliased <c>Unity</c>, so provider-specific level
/// rules and the output options both live under <c>Logging:Unity</c> in configuration.
/// </summary>
[ProviderAlias("Unity")]
public sealed class UnityLoggerProvider : ILoggerProvider, ISupportExternalScope {
    private readonly IOptionsMonitor<UnityLoggerOptions> m_Options;
    private readonly IDisposable? m_OptionsReloadToken;
    private readonly ConcurrentDictionary<string, UnityLogger> m_Loggers = new();
    private IExternalScopeProvider? m_ScopeProvider;

    public UnityLoggerProvider(IOptionsMonitor<UnityLoggerOptions> options) {
        m_Options = options;
        m_OptionsReloadToken = options.OnChange(_ => {
            foreach (var logger in m_Loggers.Values) {
                logger.OnConfigurationChanged();
            }
        });
    }

    public ILogger CreateLogger(string categoryName) {
        return m_Loggers.GetOrAdd(categoryName, static (name, provider) => {
            var logger = new UnityLogger(name, provider.GetOptions);
            if (provider.m_ScopeProvider is { } scopeProvider) {
                logger.SetScopeProvider(scopeProvider);
            }

            return logger;
        }, this);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) {
        m_ScopeProvider = scopeProvider;
        foreach (var logger in m_Loggers.Values) {
            logger.SetScopeProvider(scopeProvider);
        }
    }

    private UnityLoggerOptions GetOptions() => m_Options.CurrentValue;

    public void Dispose() {
        m_OptionsReloadToken?.Dispose();
        m_Loggers.Clear();
    }
}
