using VeloxLog.Core;

namespace VeloxLog.Abstractions;

/// <summary>
/// Represents a logging entity.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Gets the source name of the logger.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Writes a log entry.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="messageTemplate">The message template. Can contain placeholders like {0} or {Name}.</param>
    /// <param name="args">Arguments for the message template.</param>
    void Log(LogLevel level, string messageTemplate, params object?[] args);

    /// <summary>
    /// Writes a log entry with an associated exception.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="ex">The exception to log.</param>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="args">Arguments for the message template.</param>
    void Log(LogLevel level, Exception? ex, string messageTemplate, params object?[] args);

    // Convenience methods
    void Debug(string messageTemplate, params object?[] args);
    void Info(string messageTemplate, params object?[] args);
    void Warning(string messageTemplate, params object?[] args);
    void Warning(Exception ex, string messageTemplate, params object?[] args);
    void Error(Exception ex, string messageTemplate, params object?[] args);
    void Critical(Exception ex, string messageTemplate, params object?[] args);

    /// <summary>
    /// Creates a new logger with a hierarchical source name.
    /// </summary>
    /// <param name="childSource">The name of the child logger, which will be appended to the current source.</param>
    /// <returns>A new ILogger instance.</returns>
    ILogger CreateChild(string childSource);
}
