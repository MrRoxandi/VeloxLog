// VeloxLog/Configuration/VeloxLoggerConfigurationBuilder.cs
using VeloxLog.Abstractions;
using VeloxLog.Core;
using VeloxLog.Formatters;
using VeloxLog.Targets;

namespace VeloxLog.Configuration;

/// <summary>
/// A builder for creating <see cref="VeloxLoggerConfiguration"/> instances using a fluent API.
/// </summary>
public sealed class VeloxLoggerConfigurationBuilder
{
    private readonly VeloxLoggerConfiguration _config = new();

    /// <summary>
    /// Sets the minimum logging level. Messages below this level will be ignored.
    /// </summary>
    public VeloxLoggerConfigurationBuilder SetMinimumLevel(LogLevel level)
    {
        _config.MinimumLevel = level;
        return this;
    }

    /// <summary>
    /// Sets the default log formatter for all subsequently added targets.
    /// </summary>
    public VeloxLoggerConfigurationBuilder SetDefaultFormatter(ILogFormatter formatter)
    {
        _config.DefaultFormatter = formatter;
        return this;
    }

    /// <summary>
    /// Configures the logging system to capture caller info (method, file, line) for messages at or above the specified level.
    /// This has a performance impact and should be used for Error/Critical levels in production.
    /// </summary>
    /// <param name="level">The minimum level to capture caller info for.</param>
    /// <param name="includeForDebug">If true, caller info will also be captured for the Debug level.</param>
    public VeloxLoggerConfigurationBuilder CaptureCallerInfoFor(LogLevel level = LogLevel.Error, bool includeForDebug = false)
    {
        _config.CaptureCallerInfoFor = (level, includeForDebug);
        return this;
    }

    /// <summary>
    /// Adds a logging target.
    /// </summary>
    public VeloxLoggerConfigurationBuilder AddTarget(ILogTarget target)
    {
        _config.Targets.Add(target);
        return this;
    }

    /// <summary>
    /// Adds a console logging target.
    /// </summary>
    public VeloxLoggerConfigurationBuilder AddConsole(LogLevel minLevel = LogLevel.Debug, ILogFormatter? formatter = null)
    {
        return AddTarget(new ConsoleLogTarget(formatter ?? _config.DefaultFormatter, minLevel));
    }

    /// <summary>
    /// Adds a file logging target.
    /// </summary>
    public VeloxLoggerConfigurationBuilder AddFile(string filePath, LogLevel minLevel = LogLevel.Debug, ILogFormatter? formatter = null)
    {
        return AddTarget(new FileLogTarget(filePath, formatter ?? _config.DefaultFormatter, minLevel));
    }

    /// <summary>
    /// Adds an in-memory logging target, useful for diagnostics and testing.
    /// </summary>
    public VeloxLoggerConfigurationBuilder AddMemory(int capacity = 512, LogLevel minLevel = LogLevel.Debug)
    {
        return AddTarget(new MemoryLogTarget(capacity, minLevel));
    }

    /// <summary>
    /// Builds the final <see cref="VeloxLoggerConfiguration"/> object.
    /// </summary>
    internal VeloxLoggerConfiguration Build()
    {
        if (_config.Targets.Count == 0)
        {
            // Add a default console logger if no targets are configured.
            AddConsole();
        }
        return _config;
    }
}