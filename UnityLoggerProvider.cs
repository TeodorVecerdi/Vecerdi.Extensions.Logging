using Microsoft.Extensions.Logging;

namespace MediaVault.Logging;

public sealed class UnityLoggerProvider : ILoggerProvider {
    public ILogger CreateLogger(string categoryName) {
        return new UnityLogger(categoryName);
    }

    public void Dispose() { }
}
