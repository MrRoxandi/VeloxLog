// VeloxLog/Abstractions/ILogTarget.cs
using VeloxLog.Core;

namespace VeloxLog.Abstractions;

/// <summary>
/// Represents a destination for log entries, such as the console, a file, or memory.
/// </summary>
public interface ILogTarget : IDisposable
{
    /// <summary>
    /// Gets the minimum level of messages that this target will process.
    /// </summary>
    LogLevel MinimumLevel { get; }

    /// <summary>
    /// Gets or sets whether this target is enabled.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Asynchronously enqueues a log entry to be processed by the target.
    /// </summary>
    /// <param name="entry">The log entry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnqueueAsync(in LogEntry entry);
}