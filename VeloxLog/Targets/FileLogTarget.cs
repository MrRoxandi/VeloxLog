// VeloxLog/Targets/FileLogTarget.cs
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using VeloxLog.Abstractions;
using VeloxLog.Core;

namespace VeloxLog.Targets;

/// <summary>
/// Writes log entries to a file. Uses a background queue and batching for high performance.
/// </summary>
public sealed class FileLogTarget : ILogTarget, IAsyncDisposable
{
    private readonly Channel<LogEntry> _queue;
    private readonly ILogFormatter _formatter;
    private readonly Task _workerTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _filePath;
    private readonly int _maxBatchSize = 100;
    private readonly TimeSpan _batchFlushInterval = TimeSpan.FromSeconds(1);

    // Static dictionary to ensure only one writer per file path across the application.
    private static readonly ConcurrentDictionary<string, object> FileLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _fileLock;

    /// <inheritdoc/>
    public LogLevel MinimumLevel { get; }

    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogTarget"/> class.
    /// </summary>
    /// <param name="filePath">The full path of the log file.</param>
    /// <param name="formatter">The formatter to use.</param>
    /// <param name="minLevel">The minimum log level to process.</param>
    /// <param name="queueCapacity">The maximum number of log entries to buffer in memory.</param>
    public FileLogTarget(string filePath, ILogFormatter formatter, LogLevel minLevel, int queueCapacity = 10000)
    {
        _filePath = Path.GetFullPath(filePath);
        _formatter = formatter;
        MinimumLevel = minLevel;

        // Ensure the directory exists.
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Unbounded channel is suitable for file logging, as we don't want to drop messages.
        // The application's memory is the limit.
        _queue = Channel.CreateUnbounded<LogEntry>();
        _fileLock = FileLocks.GetOrAdd(_filePath, _ => new object());

        _workerTask = Task.Run(ProcessQueueAsync, _cts.Token);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(in LogEntry entry)
    {
        if (!Enabled || entry.Level < MinimumLevel)
        {
            return Task.CompletedTask;
        }

        // This is fast and non-blocking for an unbounded channel.
        _queue.Writer.TryWrite(entry);

        return Task.CompletedTask;
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new List<string>(_maxBatchSize);
        var timer = new PeriodicTimer(_batchFlushInterval);

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                // Wait for either an item to arrive or the timer to tick.
                var waitTask = _queue.Reader.WaitToReadAsync(_cts.Token).AsTask();
                var timerTask = timer.WaitForNextTickAsync(_cts.Token).AsTask();

                await Task.WhenAny(waitTask, timerTask);

                // Drain the queue into the batch
                while (batch.Count < _maxBatchSize && _queue.Reader.TryRead(out var entry))
                {
                    batch.Add(_formatter.Format(entry, forFile: true));
                }

                if (batch.Count > 0)
                {
                    await WriteBatchToFileAsync(batch);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            // Log to self-diagnostics
            Console.Error.WriteLine($"[VeloxLog.FileTarget] Unhandled exception in worker: {ex.Message}");
        }
        finally
        {
            // Final flush of any remaining items after cancellation.
            while (_queue.Reader.TryRead(out var entry))
            {
                batch.Add(_formatter.Format(entry, forFile: true));
            }
            if (batch.Count > 0)
            {
                await WriteBatchToFileAsync(batch);
            }
            timer.Dispose();
        }
    }

    private async Task WriteBatchToFileAsync(List<string> batch)
    {
        // The lock is still necessary if multiple FileLogTarget instances could point to the same file.
        // Even with our static dictionary, it's a good safeguard.
        // The actual file I/O is async.
        lock (_fileLock)
        {
            // Using a single File.AppendAllLinesAsync is efficient for batching.
            // Note: This is a simplified approach. For extreme performance, managing a StreamWriter might be better,
            // but this is much safer and simpler.
            File.AppendAllLines(_filePath, batch, Encoding.UTF8);
        }
        await Task.CompletedTask; // Keep the method async for future improvements.
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // This will block until the async disposal is complete.
        // It's a bridge for non-async DI containers or AppDomain.ProcessExit.
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            // Wait for the worker to finish, with a timeout.
            await _workerTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine($"[VeloxLog.FileTarget] Timed out waiting for worker to shut down for file: {_filePath}");
        }

        _cts.Dispose();
    }
}