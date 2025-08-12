// VeloxLog/Configuration/VeloxLoggerConfiguration.cs
using VeloxLog.Abstractions;
using VeloxLog.Core;
using VeloxLog.Formatters;

namespace VeloxLog.Configuration;

/// <summary>
/// Holds the complete configuration for the logging system.
/// </summary>
public sealed class VeloxLoggerConfiguration
{
    /// <summary>
    /// The minimum level of messages to be processed.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Configuration for capturing caller information (method name, file path, line number).
    /// </summary>
    public (LogLevel Level, bool IncludeDebug) CaptureCallerInfoFor { get; set; } = (LogLevel.Error, false);

    /// <summary>
    /// The list of targets where log entries will be sent.
    /// </summary>
    public List<ILogTarget> Targets { get; } = [];

    /// <summary>
    /// The default formatter to be used by targets if they don't have one specified.
    /// </summary>
    public ILogFormatter DefaultFormatter { get; set; } = new DefaultLogFormatter();
}