// VeloxLog/Targets/ConsoleLogTarget.cs
using System.Threading.Channels;
using VeloxLog.Abstractions;
using VeloxLog.Core;

namespace VeloxLog.Targets;

/// <summary>
/// Writes log entries to the standard console output.
/// Uses a background queue to avoid blocking the calling thread.
/// </summary>
public sealed class ConsoleLogTarget : ILogTarget
{
    private readonly Channel<LogEntry> _queue;
    private readonly ILogFormatter _formatter;
    private readonly Task _workerTask;
    private readonly CancellationTokenSource _cts = new();

    /// <inheritdoc/>
    public LogLevel MinimumLevel { get; }

    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogTarget"/> class.
    /// </summary>
    /// <param name="formatter">The formatter to use for converting log entries to strings.</param>
    /// <param name="minLevel">The minimum log level this target will process.</param>
    /// <param name="queueCapacity">The maximum number of log entries to buffer in memory.</param>
    public ConsoleLogTarget(ILogFormatter formatter, LogLevel minLevel, int queueCapacity = 1000)
    {
        _formatter = formatter;
        MinimumLevel = minLevel;

        // A bounded channel will drop new messages if the consumer (console) is too slow.
        // This prevents unbounded memory growth.
        var options = new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        };
        _queue = Channel.CreateBounded<LogEntry>(options);

        _workerTask = Task.Run(ProcessQueueAsync, _cts.Token);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(in LogEntry entry)
    {
        if (!Enabled || entry.Level < MinimumLevel)
        {
            return Task.CompletedTask;
        }

        // TryWrite is non-blocking and will succeed if there's space in the queue.
        _queue.Writer.TryWrite(entry);

        return Task.CompletedTask;
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var entry in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                var text = _formatter.Format(entry, forFile: false);
                var originalColor = Console.ForegroundColor;
                try
                {
                    Console.ForegroundColor = GetColorForLevel(entry.Level);
                    await Console.Out.WriteLineAsync(text);
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down.
        }
        catch (Exception ex)
        {
            // Log to self-diagnostics to avoid crashing the app.
            // In a real-world library, this might write to a fallback location.
            Console.Error.WriteLine($"[VeloxLog.ConsoleTarget] Unhandled exception in worker: {ex.Message}");
        }
    }

    private static ConsoleColor GetColorForLevel(LogLevel level) => level switch
    {
        LogLevel.Debug => ConsoleColor.DarkGray,
        LogLevel.Info => ConsoleColor.White,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Critical => ConsoleColor.Magenta,
        _ => ConsoleColor.Gray
    };

    /// <inheritdoc/>
    public void Dispose()
    {
        // Signal the writer that no more items will be added.
        _queue.Writer.TryComplete();
        _cts.Cancel();

        // Wait for the worker to finish processing remaining items.
        // A timeout is crucial to prevent the application from hanging on shutdown.
        _workerTask.Wait(TimeSpan.FromSeconds(2));

        _cts.Dispose();
    }
}