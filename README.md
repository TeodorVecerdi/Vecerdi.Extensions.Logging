# Vecerdi.Extensions.Logging

A `Microsoft.Extensions.Logging` provider for Unity. Anything that takes an `ILogger` — your own
services, or the Microsoft.Extensions / third-party libraries you pull into a Unity project — writes
to the Unity console with the standard category filtering, options, and scopes you get on .NET, and
the output stays readable: a coloured `[Level, Category]` header in the editor, exceptions handed to
`Debug.LogException` so the console keeps their stack trace, and the logger frames hidden from the
call stack.

```
[Information, NavigationManager] Navigated to /movies/the-thing
[Warning, AssetManager] [key=poster:1234] Load took 1.8s
[Error, DatabaseContext] Migration V72 failed
[See exception in the next message]
```

## Features

- **Drop-in `ILoggerProvider`.** Registers through the normal `ILoggingBuilder`, so `ILogger<T>`
  constructor injection, `LoggerMessage` source generators, and category-based filtering all work as
  they do on .NET. No dependency on any particular container or bootstrap.
- **Static access for code outside the container.** `UnityLoggerFactory.CreateLogger<T>()` works
  from editor scripts, static classes, and before any container exists. Loggers it hands out
  forward to the real `ILoggerFactory` as soon as one is provided, so they are safe to keep in
  static fields.
- **Readable console output.** Level-coloured header in the editor (plain text in players),
  namespaces trimmed from category names, optional scopes rendered inline as `[key=value ...]`.
- **Exceptions preserved.** Errors and above with an exception log the message, then the exception
  itself via `Debug.LogException`, so the console shows the real stack rather than a flattened string.
- **Live reconfiguration.** Level rules and options are read through `IOptionsMonitor`, so a
  configuration reload takes effect without recreating loggers.

## Requirements

- One of:
    - **Unity 6.5 or later** with [UnityRoslynUpdater](https://github.com/DaZombieKiller/UnityRoslynUpdater) to
      enable modern C# features (C# 13+) on the Mono runtime
    - **Unity 7 or later**, which runs on CoreCLR and ships the latest C# features out of the box
- The following NuGet packages (e.g. via NuGetForUnity):
    - Microsoft.Extensions.Logging
    - Microsoft.Extensions.Options
    - Microsoft.Extensions.Configuration.Abstractions (for the configuration-bound overload)

## Installation

This library is designed to be embedded directly in your project. Add it as a submodule or copy the
source under `Assets/`:

```
git submodule add https://github.com/TeodorVecerdi/Vecerdi.Extensions.Logging.git Assets/Scripts/Vecerdi.Extensions.Logging
```

The sources use nullable reference types. Add a `csc.rsp` beside the asmdef (it is gitignored here so
each project keeps its own conventions), at minimum:

```
-nullable:enable
```

The `Tests/` folder is an edit-mode NUnit assembly; it only compiles when the project includes tests.

## Quick start

### Register the provider

`AddUnityLogging` is an ordinary `ILoggingBuilder` extension. Register it in whatever container you
assemble, alongside any other providers:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vecerdi.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => {
    builder.SetMinimumLevel(LogLevel.Information);
    builder.AddUnityLogging(options => options.IncludeScopes = true);
});

var provider = services.BuildServiceProvider();
```

To drive both the level filtering and the output options from configuration, use the
`IConfiguration` overload together with the standard `AddConfiguration`:

```csharp
services.AddLogging(builder => {
    builder.AddConfiguration(configuration.GetSection("Logging"));
    builder.AddUnityLogging(configuration.GetSection("UnityLogger"));
});
```

From there, take an `ILogger<T>` anywhere the container constructs your type:

```csharp
public sealed class MovieImporter(ILogger<MovieImporter> logger) {
    public void Import(string path) {
        logger.LogInformation("Importing {Path}", path);
    }
}
```

### Static and editor code

For static classes, editor scripts, or anything that runs before the container is built, use the
factory:

```csharp
private static readonly ILogger s_Logger = UnityLoggerFactory.CreateLogger<CompilationMenuItems>();
```

These loggers start on a built-in fallback (Information and above, trimmed names, colours). Hand the
factory your container once it exists and every logger created so far switches over on its next
use; pass `null` to go back to the fallback, e.g. when leaving play mode:

```csharp
UnityLoggerFactory.Initialize(provider);   // after BuildServiceProvider
UnityLoggerFactory.Initialize(null);       // on teardown
```

### Example: with Vecerdi.Extensions.DependencyInjection

With [Vecerdi.Extensions.DependencyInjection](https://github.com/TeodorVecerdi/Vecerdi.Extensions.DependencyInjection)
the container is built before the first scene loads, and its `ServiceManager` is the natural place
for both the registration and the factory hand-off:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection;
using Vecerdi.Extensions.Logging;

internal static class LoggingConfiguration {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Configure() {
        ServiceManager.RegisterServices((services, configuration) => {
            services.AddLogging(builder => {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddUnityLogging(configuration.GetSection("UnityLogger"));
            });
        });

        ServiceManager.RegisterPostInitializationAction(UnityLoggerFactory.Initialize);
    }
}
```

`BaseMonoBehaviour` subclasses then inject loggers like any other service:

```csharp
public sealed class PlayerHud : BaseMonoBehaviour {
    [Inject] internal ILogger<PlayerHud> Logger { get; set; } = null!;
}
```

## Configuration

Filtering uses the standard `Logging` section; the output is shaped by `UnityLogger` (or by the
`Action<UnityLoggerOptions>` overload when you prefer code):

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "System.Net.Http.HttpClient": "Warning",
            "MyGame.Audio": "Debug"
        }
    },
    "UnityLogger": {
        "EnableColoredOutput": true,
        "TrimNamespaces": true,
        "NamespaceSegmentsToKeep": 0,
        "IncludeScopes": false
    }
}
```

| Option                    | Default | Effect                                                                                            |
|---------------------------|---------|---------------------------------------------------------------------------------------------------|
| `EnableColoredOutput`     | `true`  | Rich-text colours for the header. Editor only; players always get plain text.                     |
| `TrimNamespaces`          | `true`  | Shorten `My.Game.Audio.Mixer` in the header.                                                      |
| `NamespaceSegmentsToKeep` | `0`     | With trimming on: `0` keeps `Mixer`, `1` keeps `Audio.Mixer`, and so on.                          |
| `IncludeScopes`           | `false` | Render active `BeginScope` state after the header, e.g. `[RequestId=42 User=teodor]`.             |

Level rules follow `Microsoft.Extensions.Logging` semantics: the longest matching category prefix wins,
provider-specific rules may name `UnityLoggerProvider`, and `Default`/`MinLevel` is the fallback.

## How levels map to the console

| `LogLevel`                         | Unity call                                         |
|------------------------------------|----------------------------------------------------|
| Trace, Debug, Information          | `Debug.Log`                                        |
| Warning                            | `Debug.LogWarning`                                 |
| Error, Critical                    | `Debug.LogError`, plus `Debug.LogException` when an exception is attached |

## License

[MIT](LICENSE).
