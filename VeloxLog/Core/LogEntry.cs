namespace VeloxLog.Core;

/// <summary>
/// Represents a single log event. This is a struct to minimize GC pressure.
/// </summary>
public readonly struct LogEntry(LogLevel level, string source, string messageTemplate, object?[]? args, Exception? ex,
    string? callerMember, string? callerFile, int callerLine)
{
    /// <inheritdoc/>
    public DateTime TimeStamp { get; } = DateTime.UtcNow;
    public LogLevel Level { get; } = level;
    public string Source { get; } = source ?? "<Unknown>";
    public string MessageTemplate { get; } = messageTemplate ?? string.Empty;
    public object?[]? Args { get; } = args;
    public Exception? Exception { get; } = ex;
    public string? CallerMember { get; } = callerMember;
    public string? CallerFile { get; } = callerFile;
    public int CallerLine { get; } = callerLine;
}
