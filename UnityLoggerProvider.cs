using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaVault.Logging;

public sealed class UnityLoggerProvider : ILoggerProvider {
    private readonly IOptionsMonitor<LoggerFilterOptions> m_FilterOptions;
    private readonly IOptionsMonitor<UnityLoggerOptions> m_UnityOptions;
    private readonly IDisposable? m_FilterOptionsReloadToken;
    private readonly IDisposable? m_UnityOptionsReloadToken;
    private readonly ConcurrentDictionary<string, UnityLogger> m_Loggers = new();

    public UnityLoggerProvider(IOptionsMonitor<LoggerFilterOptions> filterOptions, IOptionsMonitor<UnityLoggerOptions> unityOptions) {
        m_FilterOptions = filterOptions;
        m_UnityOptions = unityOptions;
        m_FilterOptionsReloadToken = m_FilterOptions.OnChange(OnConfigurationChanged);
        m_UnityOptionsReloadToken = m_UnityOptions.OnChange(OnConfigurationChanged);
    }

    public ILogger CreateLogger(string categoryName) {
        return m_Loggers.GetOrAdd(categoryName, static (name, @this) => new UnityLogger(name, @this.GetCurrentFilterConfig, @this.GetCurrentUnityConfig), this);
    }

    private LoggerFilterOptions GetCurrentFilterConfig() => m_FilterOptions.CurrentValue;
    private UnityLoggerOptions GetCurrentUnityConfig() => m_UnityOptions.CurrentValue;

    private void OnConfigurationChanged<T>(T options) {
        foreach (var logger in m_Loggers.Values) {
            logger.OnConfigurationChanged();
        }
    }

    public void Dispose() {
        m_FilterOptionsReloadToken?.Dispose();
        m_UnityOptionsReloadToken?.Dispose();
        m_Loggers.Clear();
    }
}
