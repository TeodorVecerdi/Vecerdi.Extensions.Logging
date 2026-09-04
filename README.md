# Vecerdi.Extensions.Logging

A `Microsoft.Extensions.Logging` provider for Unity. Anything that takes an `ILogger` — your own
services, or the Microsoft.Extensions / third-party libraries you pull into a Unity project — writes
to the Unity console with the standard category filtering, options, and scopes you get on .NET, and
the output stays readable: a coloured `[Level, Category]` header in the editor, one console entry per
log call, exceptions kept in one piece, and the logger's own frames hidden from the call stack.

```
[Information, NavigationManager] Navigated to /movies/the-thing
[Warning, AssetManager] [key=poster:1234] Load took 1.8s
LoggedException: [Error, DatabaseContext] Migration V72 failed
System.IO.IOException: The database is locked
```

## Features

- **Drop-in `ILoggerProvider`.** Registers through the normal `ILoggingBuilder`, so `ILogger<T>`
  constructor injection, `LoggerMessage` source generators, and category filtering all work as they
  do on .NET. Filtering is left to `LoggerFactory`; the provider only formats and writes.
- **Configured like the built-in providers.** The provider alias is `Unity`, so level rules and output
  options both live under `Logging:Unity`, exactly the way `Logging:Console` works.
- **Static access for code outside the container.** `UnityLoggers.For<T>()` works from editor
  scripts, static classes, and before any container exists, and forwards to the real
  `ILoggerFactory` as soon as one is handed over, so the result is safe to keep in a static field.
- **Stack traces on your terms.** Capturing a stack trace is the expensive part of a Unity log call.
  `StackTraces` limits capture to warnings and errors, errors only, or nothing, per provider, without
  touching the project-wide setting.
- **Exceptions in one entry.** An error with an exception is a single console entry classified as an
  exception. The entry text is the header, your message, and the exception's type and message; the
  console's stack-trace pane shows the exception's own trace (nested exceptions included) followed by
  the call site, so click-through and Console Pro parsing work as they do for a thrown exception.
- **Console context.** Push a `UnityEngine.Object` as a scope and the entries logged inside it are
  linked to it, so clicking the entry selects the object.
- **Readable output.** Level-coloured header in the editor (plain text in players), category names
  trimmed to the last segments, optional scopes rendered inline as `[key=value ...]`.
- **Live reconfiguration.** Options are read through `IOptionsMonitor`, so a configuration reload
  takes effect without recreating loggers.

## Requirements

- One of:
    - **Unity 6.5 or later** with [UnityRoslynUpdater](https://github.com/DaZombieKiller/UnityRoslynUpdater) to
      enable modern C# features (C# 13+) on the Mono runtime
    - **Unity 7 or later**, which runs on CoreCLR and ships the latest C# features out of the box
- The following NuGet packages (e.g. via NuGetForUnity):
    - Microsoft.Extensions.Logging
    - Microsoft.Extensions.Logging.Configuration
    - Microsoft.Extensions.Options

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

With configuration attached, both the level rules and the output options come from the `Logging`
section, and the options follow reloads:

```csharp
services.AddLogging(builder => {
    builder.AddConfiguration(configuration.GetSection("Logging"));
    builder.AddUnityLogging();
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

For static classes, editor scripts, or anything that runs before the container is built:

```csharp
private static readonly ILogger s_Logger = UnityLoggers.For<CompilationMenuItems>();
```

These loggers start on a built-in factory (Unity console, Information and above). Hand over your
container's factory once it exists and every logger obtained so far switches on its next use; pass
`null` to go back to the fallback, e.g. when leaving play mode with domain reload disabled:

```csharp
UnityLoggers.Initialize(provider.GetRequiredService<ILoggerFactory>());   // after the container is built
UnityLoggers.Initialize(null);                                             // on teardown
```

### Linking entries to a scene object

Push the object as a scope. Every entry logged while the scope is open gets it as its console context:

```csharp
using (logger.BeginScope(gameObject)) {
    logger.LogWarning("Missing audio source");   // click the entry to select gameObject
}
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
                builder.AddUnityLogging();
            });
        });

        ServiceManager.RegisterPostInitializationAction(provider => UnityLoggers.Initialize(provider.GetRequiredService<ILoggerFactory>()));
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

Everything lives under the standard `Logging` section. Rules under `LogLevel` apply to all providers;
rules and options under `Unity` apply to this one:

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "System.Net.Http.HttpClient": "Warning"
        },
        "Unity": {
            "LogLevel": {
                "MyGame.Audio": "Debug"
            },
            "EnableColoredOutput": true,
            "CategorySegments": 1,
            "IncludeScopes": false,
            "StackTraces": "Always"
        }
    }
}
```

| Option                | Default  | Effect                                                                                                              |
|-----------------------|----------|---------------------------------------------------------------------------------------------------------------------|
| `EnableColoredOutput` | `true`   | Rich-text colours for the header. Editor only; players always get plain text.                                       |
| `CategorySegments`    | `1`      | Trailing segments of the category to show: `1` gives `Mixer`, `2` gives `Audio.Mixer`, `null` the full name.        |
| `IncludeScopes`       | `false`  | Render active `BeginScope` state after the header, e.g. `[RequestId=42 User=teodor]`.                               |
| `StackTraces`         | `Always` | Which levels ask Unity for a stack trace: `Always`, `WarningsAndErrors`, `ErrorsOnly`, `Never`.                     |

`StackTraces` is worth lowering in player builds once the capture cost matters; a per-environment
configuration override is the natural place for that.

## How levels map to the console

| `LogLevel`                        | Unity entry                                                                                  |
|-----------------------------------|----------------------------------------------------------------------------------------------|
| Trace, Debug, Information         | `Log`                                                                                        |
| Warning                           | `Warning`, with the exception text appended when one is attached                             |
| Error, Critical                   | `Error`; with an exception attached, a single `Exception` entry whose trace pane is the exception's |

## License

[MIT](LICENSE).
