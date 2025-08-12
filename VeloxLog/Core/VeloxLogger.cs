// VeloxLog/Core/VeloxLogger.cs
using System.Runtime.CompilerServices;
using VeloxLog.Abstractions;
using VeloxLog.Configuration;
using VeloxLog.Factories;

namespace VeloxLog.Core;

/// <summary>
/// The main implementation of the ILogger interface.
/// </summary>
internal sealed class VeloxLogger(string source, VeloxLoggerConfiguration options, VeloxLoggerFactory factory) : ILogger
{
    public string Source { get; } = source;
    private readonly VeloxLoggerConfiguration _options = options;
    private readonly VeloxLoggerFactory _factory = factory;

    public void Log(LogLevel level, string messageTemplate, params object?[] args)
    {
        // Check if an exception is the last argument, which is a common convention
        if (args.Length > 0 && args[^1] is Exception ex)
        {
            LogInternal(level, ex, messageTemplate, args[..^1], null, null, 0);
        }
        else
        {
            LogInternal(level, null, messageTemplate, args, null, null, 0);
        }
    }

    public void Log(LogLevel level, Exception? ex, string messageTemplate, params object?[] args)
    {
        LogInternal(level, ex, messageTemplate, args, null, null, 0);
    }

    // Internal method that accepts caller info
    private void LogInternal(LogLevel level, Exception? ex, string messageTemplate, object?[]? args,
        string? member, string? file, int line)
    {
        if (level < _options.MinimumLevel) return;

        bool includeCaller = level >= _options.CaptureCallerInfoFor.Level ||
                             (_options.CaptureCallerInfoFor.IncludeDebug && level == LogLevel.Debug);

        var entry = new LogEntry(level, Source, messageTemplate, args, ex,
            includeCaller ? member : null,
            includeCaller ? file : null,
            includeCaller ? line : 0);

        foreach (var target in _options.Targets)
        {
            // Fire-and-forget; targets are responsible for their own queuing and error handling.
            _ = target.EnqueueAsync(entry);
        }
    }

    // Convenience Methods with Caller Info
    public void Debug(string messageTemplate, params object?[] args)
    {
        LogInternal(LogLevel.Debug, null, messageTemplate, args,
            GetMember(), GetFile(), GetLine());
    }

    public void Info(string messageTemplate, params object?[] args)
    {
        LogInternal(LogLevel.Info, null, messageTemplate, args,
           GetMember(), GetFile(), GetLine());
    }

    public void Warning(string messageTemplate, params object?[] args)
    {
        LogInternal(LogLevel.Warning, null, messageTemplate, args,
            GetMember(), GetFile(), GetLine());
    }

    public void Warning(Exception ex, string messageTemplate, params object?[] args)
    {
        LogInternal(LogLevel.Warning, ex, messageTemplate, args,
            GetMember(), GetFile(), GetLine());
    }

    public void Error(Exception ex, string messageTemplate, params object?[] args)
    {
        LogInternal(LogLevel.Error, ex, messageTemplate, args,
            GetMember(), GetFile(), GetLine());
    }

    public void Critical(Exception ex, string messageTemplate, params object?[] args)
    {
        LogInternal(LogLevel.Critical, ex, messageTemplate, args,
            GetMember(), GetFile(), GetLine());
    }

    public ILogger CreateChild(string childSource)
    {
        var newSource = string.IsNullOrEmpty(Source) || Source == "Default"
            ? childSource
            : $"{Source}.{childSource}";
        return _factory.CreateLogger(newSource);
    }

    // Helper methods to capture caller info only when needed, avoiding parameter boilerplate on public methods.
    // NOTE: These helpers MUST be called directly from the public-facing log methods to capture the correct stack frame.
    [MethodImpl(MethodImplOptions.NoInlining)] private static string? GetMember([CallerMemberName] string? n = null) => n;
    [MethodImpl(MethodImplOptions.NoInlining)] private static string? GetFile([CallerFilePath] string? n = null) => n;
    [MethodImpl(MethodImplOptions.NoInlining)] private static int GetLine([CallerLineNumber] int n = 0) => n;
}