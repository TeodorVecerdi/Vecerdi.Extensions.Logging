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
  they do on .NET.
- **Auto-configured with [Vecerdi.Extensions.DependencyInjection](https://github.com/TeodorVecerdi/Vecerdi.Extensions.DependencyInjection).**
  When that package builds the container, this one registers itself and binds the `Logging` and
  `UnityLogger` configuration sections. No bootstrap code.
- **Static access for code outside the container.** `UnityLoggerFactory.CreateLogger<T>()` works
  from editor scripts, static classes, and before the container exists; once the container is handed
  to it, new loggers come from the container's `ILoggerFactory`.
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
- [Vecerdi.Extensions.DependencyInjection](https://github.com/TeodorVecerdi/Vecerdi.Extensions.DependencyInjection)
  (the automatic registration hooks into its `ServiceManager`)
- The following NuGet packages (e.g. via NuGetForUnity):
    - Microsoft.Extensions.Logging
    - Microsoft.Extensions.Logging.Configuration
    - Microsoft.Extensions.Options

## Installation

This library is designed to be embedded directly in your project. Add it as a submodule or copy the
source under `Assets/`, next to the DI package:

```
git submodule add https://github.com/TeodorVecerdi/Vecerdi.Extensions.Logging.git Assets/Scripts/Vecerdi.Extensions.Logging
```

The sources use nullable reference types. Add a `csc.rsp` beside the asmdef (it is gitignored here so
each project keeps its own conventions), at minimum:

```
-nullable:enable
```

## Quick start

### With the DI package

There is nothing to wire. At `SubsystemRegistration` this package calls
`ServiceManager.RegisterServices` and adds `ILoggingBuilder.AddUnityLogging()` with the `Logging`
section for filtering and the `UnityLogger` section for output options. Take an `ILogger<T>` anywhere
the container constructs your type:

```csharp
public sealed class MovieImporter(ILogger<MovieImporter> logger) {
    public void Import(string path) {
        logger.LogInformation("Importing {Path}", path);
    }
}
```

`BaseMonoBehaviour` subclasses can inject it the same way:

```csharp
public sealed class PlayerHud : BaseMonoBehaviour {
    [Inject] internal ILogger<PlayerHud> Logger { get; set; } = null!;
}
```

For static and editor code, or anything that runs before the container is built, use the factory:

```csharp
private static readonly ILogger s_Logger = UnityLoggerFactory.CreateLogger<CompilationMenuItems>();
```

Loggers created this way use a sensible fallback (Information and above, trimmed names, colours) until
you hand the factory the built container. Do that once, after the container exists. Loggers the factory
already created keep the fallback settings (they are cached by category), so request configured ones
after this point:

```csharp
ServiceManager.RegisterPostInitializationAction(services => UnityLoggerFactory.Initialize(services));
```

### Without the DI package

`AddUnityLogging` is an ordinary `ILoggingBuilder`/`IServiceCollection` extension, so you can also
register it in any container you assemble yourself:

```csharp
services.AddLogging(builder => builder.AddUnityLogging(options => {
    options.IncludeScopes = true;
}));
```

The automatic registration file (`ServiceConfiguration.cs`) still compiles against the DI package, so
that package remains a requirement even when you register manually.

## Configuration

Filtering uses the standard `Logging` section; the output is shaped by `UnityLogger`:

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

[MIT NON-AI License](LICENSE).
