// VeloxLog/Formatters/DefaultLogFormatter.cs
using System.Text;
using VeloxLog.Abstractions;
using VeloxLog.Core;

namespace VeloxLog.Formatters;

/// <summary>
/// The default formatter for log entries.
/// </summary>
public sealed class DefaultLogFormatter : ILogFormatter
{
    private readonly bool _includeCallerInfo;

    public DefaultLogFormatter(bool includeCallerInfoWhenAvailable = true)
    {
        _includeCallerInfo = includeCallerInfoWhenAvailable;
    }

    public string Format(in LogEntry entry, bool forFile)
    {
        // A simple, efficient formatter. For full structured logging (e.g., MessageTemplate spec), a more complex parser would be needed.
        // This implementation uses standard string.Format for simplicity.
        var sb = new StringBuilder(256);
        sb.Append('[').Append(entry.TimeStamp.ToLocalTime().ToString("HH:mm:ss.fff")).Append(']');
        sb.Append(" [").Append(GetLevelString(entry.Level)).Append(']');
        sb.Append(" [").Append(entry.Source).Append(']');
        sb.Append(' ');

        if (entry.Args != null && entry.Args.Length > 0)
        {
            try
            {
                sb.AppendFormat(entry.MessageTemplate, entry.Args);
            }
            catch (FormatException)
            {
                // If formatting fails, append the template and args raw
                sb.Append(entry.MessageTemplate);
                sb.Append(" (Args: ").Append(string.Join(", ", entry.Args)).Append(')');
            }
        }
        else
        {
            sb.Append(entry.MessageTemplate);
        }

        if (entry.Exception is not null)
        {
            sb.Append(forFile ? Environment.NewLine : " | ").Append("Exception: ").Append(entry.Exception.Message);
            if (forFile)
            {
                sb.AppendLine();
                sb.AppendLine("--- STACK TRACE ---");
                sb.AppendLine(entry.Exception.ToString());
                sb.AppendLine("-------------------");
            }
        }

        if (_includeCallerInfo && !string.IsNullOrEmpty(entry.CallerMember))
        {
            sb.Append(" (at ").Append(entry.CallerMember);
            if (!string.IsNullOrEmpty(entry.CallerFile))
            {
                sb.Append(" in ").Append(Path.GetFileName(entry.CallerFile)).Append(':').Append(entry.CallerLine);
            }
            sb.Append(')');
        }

        return sb.ToString();
    }

    private static string GetLevelString(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };
    }
}