// VeloxLog/Factories/VeloxLoggerFactory.cs
using System.Collections.Concurrent;
using VeloxLog.Abstractions;
using VeloxLog.Configuration;
using VeloxLog.Core;

namespace VeloxLog.Factories;

/// <summary>
/// A factory for creating <see cref="ILogger"/> instances.
/// </summary>
public sealed class VeloxLoggerFactory : ILoggerFactory
{
    private readonly VeloxLoggerConfiguration _options;
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VeloxLoggerFactory"/> class.
    /// </summary>
    /// <param name="options">The configuration to use for created loggers.</param>
    public VeloxLoggerFactory(VeloxLoggerConfiguration options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return _loggers.GetOrAdd(source, s => new VeloxLogger(s, _options, this));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose all configured targets
        foreach (var target in _options.Targets)
        {
            try
            {
                target.Dispose();
            }
            catch
            {
                // Suppress exceptions during disposal to ensure all targets are attempted.
            }
        }
        _loggers.Clear();
    }
}