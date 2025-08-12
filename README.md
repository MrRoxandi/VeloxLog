# VeloxLog

[![NuGet version](https://img.shields.io/nuget/v/VeloxLog.svg?style=for-the-badge)](https://www.nuget.org/packages/VeloxLog/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](https://github.com/MrRoxandi/VeloxLog?tab=MIT-1-ov-file)

**VeloxLog** is a high-performance, lightweight, and modern logging library for .NET. It's designed with a focus on speed, low allocation, and a simple, intuitive API. Whether you're building a simple console app or a high-traffic web service, VeloxLog provides the flexibility and performance you need.

## Features

- 🚀 **High Performance**: Built with performance in mind, using techniques like background queuing, batch processing, and minimal allocations.
- ✍️ **Structured Logging**: Log messages with templates and arguments (`"Processed order {OrderId}"`) for better machine-readable logs and deferred formatting.
- 🔌 **Extensible Targets**: Out-of-the-box support for Console, File, and in-memory logging. Easily extend with custom targets.
- 💡 **Simple Static API**: Get started instantly with a static `Log` class. No configuration needed for basic console logging.
- 🏗️ **DI Friendly**: Designed to integrate seamlessly with dependency injection containers like `Microsoft.Extensions.DependencyInjection`.
- ✨ **Fluent Configuration**: A clean, fluent builder API for setting up your logging pipeline.
- 🔬 **Caller Info**: Automatically capture method name, file path, and line number for diagnostics, with configurable performance controls.
- 🏢 **Hierarchical Loggers**: Create loggers with hierarchical sources (e.g., `MyApp.Services.PaymentService`) for granular control.

## Installation

Install the package from NuGet Package Manager or via the .NET CLI:

```sh
dotnet add package VeloxLog
```

## Quick Start: Static Logging

The easiest way to start logging is by using the static `Log` class. No configuration is required for default console logging.

```csharp
using VeloxLog;

// These will automatically write to the console.
Log.Info("Application starting up...");
Log.Debug("Connecting to the database at {Host}", "db.example.com");

try
{
    // ... your code ...
    throw new InvalidOperationException("Something went wrong!");
}
catch (Exception ex)
{
    Log.Error(ex, "An unexpected error occurred during operation {OperationName}", "ProcessUserData");
}
```

### Configuring the Static Logger

For more advanced scenarios, like logging to a file, configure the logger once at your application's entry point (`Program.cs`).

```csharp
// In Program.cs
using VeloxLog;
using VeloxLog.Core;

public static class Program
{
    public static void Main(string[] args)
    {
        // Configure VeloxLog once at startup.
        Log.Configure(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug) // Log everything from Debug and above
                .CaptureCallerInfoFor(LogLevel.Error) // Capture caller info for errors
                .AddConsole(minLevel: LogLevel.Info)  // Send Info+ to the console
                .AddFile("logs/app-.log", minLevel: LogLevel.Debug); // Send Debug+ to a file
                // The file path supports date formatting, e.g., "logs/app-{Date}.log"
        });

        Log.Info("Configuration complete. Starting main application logic.");
        // ... rest of your application ...
    }
}
```

## Structured Logging

VeloxLog embraces structured logging. Instead of formatting strings yourself, provide a message template and arguments. This defers string formatting until it's actually needed, saving CPU cycles if the message is filtered out. It also enables log targets to process the data in a structured way (e.g., writing JSON).

```csharp
var userId = 123;
var elapsedTime = 153.4;

// Good: Use structured logging
Log.Info("User {UserId} completed task in {ElapsedTime:F2} ms.", userId, elapsedTime);

// Bad: Avoid manual string interpolation
Log.Warning($"User {userId} is taking a long time to respond.");
```

**Output:**

```sh
[14:35:10.123] [INF] [Default] User 123 completed task in 153.40 ms.
```

## Using with Dependency Injection

VeloxLog is fully compatible with DI containers. This is the recommended approach for libraries and large applications.

### 1. Register VeloxLog Services

In your `Startup.cs` or `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using VeloxLog.Abstractions;
using VeloxLog.Configuration;
using VeloxLog.Factories;

var services = new ServiceCollection();

// 1. Build the configuration
var loggerConfig = new VeloxLoggerConfigurationBuilder()
    .SetMinimumLevel(LogLevel.Info)
    .AddConsole()
    .AddFile("logs/service.log")
    .Build();

// 2. Register the factory as a singleton
services.AddSingleton<ILoggerFactory>(new VeloxLoggerFactory(loggerConfig));

// 3. Register ILogger to be created on demand
// This example provides a logger with the requesting type's name as the source.
services.AddTransient<ILogger>((sp) =>
{
    // This is a placeholder for a more robust resolution mechanism.
    // In ASP.NET Core, you could resolve the consumer's type.
    var factory = sp.GetRequiredService<ILoggerFactory>();
    return factory.CreateLogger("Default"); // Or resolve a specific type name
});

// Register your services
services.AddTransient<MyService>();

var serviceProvider = services.BuildServiceProvider();
```

> **Note:** Remember to call `Dispose()` on the `ILoggerFactory` when your application shuts down to ensure all logs are flushed.

### 2. Inject `ILogger` into Your Services

```csharp
public class MyService
{
    private readonly ILogger _logger;

    public MyService(ILogger logger)
    {
        // Create a hierarchical logger for this specific class
        _logger = logger.CreateChild(nameof(MyService));
    }

    public void DoWork()
    {
        _logger.Info("Starting a complex business operation...");
        // ...
        _logger.Warning("The operation completed, but with minor issues.");
    }
}
```

## Configuration In-Depth

### Log Levels

- `Debug`
- `Info`
- `Warning`
- `Error`
- `Critical`

### Configuration Builder API

```csharp
Log.Configure(builder =>
{
    // Set the global minimum level. Messages below this are discarded immediately.
    builder.SetMinimumLevel(LogLevel.Debug);

    // Customize the default string format for all targets.
    builder.SetDefaultFormatter(new MyCustomFormatter());

    // Capture caller info (method, file, line) for Warning and above.
    // Also enable it for Debug messages. This has a performance cost.
    builder.CaptureCallerInfoFor(LogLevel.Warning, includeForDebug: true);

    // --- Add and configure targets ---

    // Console Target: Colored output to the console.
    builder.AddConsole(minLevel: LogLevel.Info);

    // File Target: Asynchronous, batched file writer.
    builder.AddFile(
        filePath: "logs/app.log",
        minLevel: LogLevel.Debug
    );

    // Memory Target: Stores recent logs in a circular buffer.
    // Useful for diagnostics or displaying logs in a UI.
    builder.AddMemory(capacity: 100, minLevel: LogLevel.Info);

    // Custom Target: Add your own implementation of ILogTarget.
    builder.AddTarget(new MyCustomDatabaseTarget("connection_string"));
});
```

### Targets

- `ConsoleLogTarget`: Writes colored logs to the console. It uses a background queue to prevent blocking your application's main thread.
- `FileLogTarget`: A highly efficient target that writes logs to a file. It uses batching and a background thread to handle high volumes of messages with minimal performance impact.
- `MemoryLogTarget`: Keeps a configurable number of recent log entries in an in-memory circular buffer. You can retrieve them via the `GetRecent()` method, which is perfect for a live log viewer in an admin dashboard.

## Contributing

Contributions are welcome! If you find a bug, have a feature request, or want to improve the code, please feel free to open an issue or submit a pull request.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
