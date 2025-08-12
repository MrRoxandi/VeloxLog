// VeloxLog/Log.cs
using System.Runtime.CompilerServices;
using VeloxLog.Abstractions;
using VeloxLog.Configuration;
using VeloxLog.Core;
using VeloxLog.Factories;

namespace VeloxLog;

/// <summary>
/// Provides a static, global entry point for logging.
/// It can be used directly for simple scenarios or configured once at application startup using <see cref="Configure"/>.
/// If not configured, it will default to logging to the console.
/// </summary>
public static class Log
{
    // The explicitly configured factory. Volatile to ensure visibility across threads.
    private static volatile ILoggerFactory? _configuredFactory;

    // Lazy-initialized factory for default (console) logging.
    // This will only be created if no explicit configuration is provided.
    private static readonly Lazy<ILoggerFactory> _defaultFactory = new(() =>
    {
        var defaultConfig = new VeloxLoggerConfigurationBuilder().AddConsole().Build();
        return new VeloxLoggerFactory(defaultConfig);
    });

    // The single lock object for configuration changes.
    private static readonly object _lock = new();

    // The root logger instance. It's retrieved from the appropriate factory.
    private static ILogger _rootLogger;

    static Log()
    {
        // Initialize with a non-functional logger. It will be replaced on the first log call or configure.
        _rootLogger = new NullLogger();

        // Ensure targets are disposed on application exit.
        AppDomain.CurrentDomain.ProcessExit += OnShutdown;
        AppDomain.CurrentDomain.DomainUnload += OnShutdown;
    }

    /// <summary>
    /// Gets the active logger factory. If an explicit configuration was provided, returns that.
    /// Otherwise, returns the default (console) factory.
    /// </summary>
    private static ILoggerFactory Factory
    {
        get
        {
            // Double-check locking pattern for performance.
            // First check is outside the lock.
            var factory = _configuredFactory;
            if (factory != null)
            {
                return factory;
            }

            // If not configured, return the lazy-initialized default factory.
            return _defaultFactory.Value;
        }
    }

    /// <summary>
    /// Configures the static logger instance. This should be called once at application startup.
    /// Calling this after logging has already started with the default configuration is not supported and will throw.
    /// </summary>
    /// <param name="configureAction">An action to configure the logger settings.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if configuration is attempted after the default logger has already been used,
    /// or if configuration is attempted more than once.
    /// </exception>
    public static void Configure(Action<VeloxLoggerConfigurationBuilder> configureAction)
    {
        lock (_lock)
        {
            if (_configuredFactory != null)
            {
                throw new InvalidOperationException("VeloxLog has already been configured explicitly.");
            }

            if (_defaultFactory.IsValueCreated)
            {
                throw new InvalidOperationException("Cannot configure VeloxLog after the default logger has already been used. Please call Configure() at the very beginning of your application.");
            }

            var builder = new VeloxLoggerConfigurationBuilder();
            configureAction(builder);
            var config = builder.Build();

            var factory = new VeloxLoggerFactory(config);
            _configuredFactory = factory; // Set the configured factory
            _rootLogger = factory.CreateLogger("Default");
        }
    }

    /// <summary>
    /// Creates a new logger instance with the specified source name.
    /// </summary>
    public static ILogger CreateLogger(string source) => Factory.CreateLogger(source);

    /// <summary>
    /// Creates a new logger instance with the type's full name as the source.
    /// </summary>
    public static ILogger CreateLogger<T>() => Factory.CreateLogger(typeof(T).FullName ?? typeof(T).Name);

    // This method ensures the root logger is initialized before the first use.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ILogger GetRootLogger()
    {
        // The _rootLogger is initially NullLogger. If it's still NullLogger, it means neither
        // Configure() nor any logging method has been called yet.
        // We get the Factory, which initializes either the default or the configured one,
        // and this process will set the _rootLogger.
        // But what if Configure was called but no log yet? _rootLogger is already set.
        // What if no configure and no log yet? _rootLogger is null. We get default factory, which sets root.
        // This is complex. Let's simplify.

        // Simpler logic: The root logger is always derived from the current Factory.
        // The check against NullLogger ensures we only create it once.
        if (_rootLogger is NullLogger)
        {
            lock (_lock)
            {
                // Re-check after acquiring lock
                if (_rootLogger is NullLogger)
                {
                    _rootLogger = Factory.CreateLogger("Default");
                }
            }
        }
        return _rootLogger;
    }

    /// <summary>Logs a message with the Debug level.</summary>
    public static void Debug(string messageTemplate, params object?[] args) => GetRootLogger().Debug(messageTemplate, args);

    /// <summary>Logs a message with the Info level.</summary>
    public static void Info(string messageTemplate, params object?[] args) => GetRootLogger().Info(messageTemplate, args);

    /// <summary>Logs a message with the Warning level.</summary>
    public static void Warning(string messageTemplate, params object?[] args) => GetRootLogger().Warning(messageTemplate, args);

    /// <summary>Logs a message and an exception with the Warning level.</summary>
    public static void Warning(Exception ex, string messageTemplate, params object?[] args) => GetRootLogger().Warning(ex, messageTemplate, args);

    /// <summary>Logs a message and an exception with the Error level.</summary>
    public static void Error(Exception ex, string messageTemplate, params object?[] args) => GetRootLogger().Error(ex, messageTemplate, args);

    /// <summary>Logs a message and an exception with the Critical level.</summary>
    public static void Critical(Exception ex, string messageTemplate, params object?[] args) => GetRootLogger().Critical(ex, messageTemplate, args);

    private static void OnShutdown(object? sender, EventArgs e)
    {
        // Dispose the factory that was actually used.
        if (_configuredFactory != null)
        {
            _configuredFactory.Dispose();
        }
        else if (_defaultFactory.IsValueCreated)
        {
            _defaultFactory.Value.Dispose();
        }
    }

    /// <summary>
    /// A logger that does nothing. Used as a default before any configuration or first use.
    /// </summary>
    private sealed class NullLogger : ILogger
    {
        public string Source => "Null";
        public void Log(LogLevel level, string messageTemplate, params object?[] args) { }
        public void Log(LogLevel level, Exception? ex, string messageTemplate, params object?[] args) { }
        public ILogger CreateChild(string childSource) => this;
        public void Debug(string messageTemplate, params object?[] args) { }
        public void Info(string messageTemplate, params object?[] args) { }
        public void Warning(string messageTemplate, params object?[] args) { }
        public void Warning(Exception ex, string messageTemplate, params object?[] args) { }
        public void Error(Exception ex, string messageTemplate, params object?[] args) { }
        public void Critical(Exception ex, string messageTemplate, params object?[] args) { }
    }
}