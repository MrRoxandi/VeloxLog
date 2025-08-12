// VeloxLog/Targets/MemoryLogTarget.cs
using System.Threading.Channels;
using VeloxLog.Abstractions;
using VeloxLog.Core;

namespace VeloxLog.Targets;

/// <summary>
/// Stores recent log entries in an in-memory circular buffer.
/// Useful for displaying recent logs in a UI or for diagnostics.
/// </summary>
public sealed class MemoryLogTarget : ILogTarget
{
    private readonly Channel<LogEntry> _circularBuffer;
    private readonly LogEntry[] _snapshotBuffer;

    /// <inheritdoc/>
    public LogLevel MinimumLevel { get; }

    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets the capacity of the memory buffer.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryLogTarget"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of log entries to store.</param>
    /// <param name="minLevel">The minimum log level to store.</param>
    public MemoryLogTarget(int capacity = 512, LogLevel minLevel = LogLevel.Debug)
    {
        Capacity = Math.Max(1, capacity);
        MinimumLevel = minLevel;

        // Create a bounded channel that drops the oldest items when full.
        // This perfectly simulates a circular buffer.
        var options = new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };
        _circularBuffer = Channel.CreateBounded<LogEntry>(options);
        _snapshotBuffer = new LogEntry[Capacity];
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(in LogEntry entry)
    {
        if (!Enabled || entry.Level < MinimumLevel)
        {
            return Task.CompletedTask;
        }

        // TryWrite is non-blocking and will either add the item or
        // drop the oldest and add the new one if the buffer is full.
        _circularBuffer.Writer.TryWrite(entry);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves a snapshot of the most recent log entries stored in memory.
    /// The number of returned entries will be at most the configured capacity.
    /// </summary>
    /// <returns>An array of recent log entries, ordered from oldest to newest.</returns>
    public LogEntry[] GetRecent()
    {
        // To get a consistent snapshot, we drain the channel into a temporary array.
        // This is thread-safe.
        int count = 0;
        while (count < Capacity && _circularBuffer.Reader.TryRead(out var entry))
        {
            _snapshotBuffer[count++] = entry;
        }

        var result = new LogEntry[count];
        Array.Copy(_snapshotBuffer, result, count);

        // Important: Put the items back into the channel so they are not lost.
        // The order will be preserved.
        for (int i = 0; i < count; i++)
        {
            _circularBuffer.Writer.TryWrite(result[i]);
        }

        return result;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Mark the channel as complete.
        _circularBuffer.Writer.TryComplete();
    }
}