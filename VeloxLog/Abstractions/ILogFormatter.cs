// VeloxLog/Abstractions/ILogFormatter.cs
using VeloxLog.Core;

namespace VeloxLog.Abstractions;

/// <summary>
/// Responsible for converting a <see cref="LogEntry"/> into a string representation.
/// </summary>
public interface ILogFormatter
{
    /// <summary>
    /// Formats the log entry into a string.
    /// </summary>
    /// <param name="entry">The log entry to format.</param>
    /// <param name="forFile">True if the output is intended for a file, which may influence formatting (e.g., multiline stack traces).</param>
    /// <returns>The formatted log message.</returns>
    string Format(in LogEntry entry, bool forFile);
}