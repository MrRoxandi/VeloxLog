// VeloxLog/Core/LogLevel.cs
namespace VeloxLog.Core;

/// <summary>
/// Defines the severity levels for log messages.
/// </summary>
public enum LogLevel : byte
{
    /// <summary>
    /// Detailed information, typically of interest only when diagnosing problems.
    /// </summary>
    Debug = 0,
    /// <summary>
    /// Informational messages that highlight the progress of the application at a coarse-grained level.
    /// </summary>
    Info = 1,
    /// <summary>
    /// Indicates a potential problem situation.
    /// </summary>
    Warning = 2,
    /// <summary>
    /// A runtime error or unexpected condition.
    /// </summary>
    Error = 3,
    /// <summary>
    /// A severe error that will likely lead to application termination.
    /// </summary>
    Critical = 4
}