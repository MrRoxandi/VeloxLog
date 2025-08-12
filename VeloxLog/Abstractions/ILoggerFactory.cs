// VeloxLog/Abstractions/ILoggerFactory.cs
namespace VeloxLog.Abstractions;

/// <summary>
/// Represents a type used to configure the logging system and create instances of <see cref="ILogger"/>.
/// </summary>
public interface ILoggerFactory : IDisposable
{
    /// <summary>
    /// Creates a new <see cref="ILogger"/> instance.
    /// </summary>
    /// <param name="source">The category name for messages produced by the logger.</param>
    /// <returns>The <see cref="ILogger"/>.</returns>
    ILogger CreateLogger(string source);
}