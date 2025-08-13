// VeloxLog/Formatters/DefaultLogFormatter.cs
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using VeloxLog.Abstractions;
using VeloxLog.Core;

namespace VeloxLog.Formatters;

/// <summary>
/// A high-performance default log formatter with robust support for both named and positional placeholders.
/// It intelligently handles various argument styles for a flexible and intuitive logging experience.
/// </summary>
public sealed class DefaultLogFormatter : ILogFormatter
{
    private readonly bool _includeCallerInfo;
    private readonly string _timeFormat;
    private readonly string[] _levelStrings;

    // A thread-safe cache for PropertyInfo arrays to minimize reflection overhead for anonymous types.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultLogFormatter"/> class.
    /// </summary>
    /// <param name="includeCallerInfoWhenAvailable">If true, includes caller member, file, and line number in the output if available.</param>
    /// <param name="timeFormat">The format string for the log entry timestamp. Defaults to "HH:mm:ss.fff".</param>
    public DefaultLogFormatter(bool includeCallerInfoWhenAvailable = true, string timeFormat = "HH:mm:ss.fff")
    {
        _includeCallerInfo = includeCallerInfoWhenAvailable;
        _timeFormat = timeFormat;
        _levelStrings = ["DBG", "INF", "WRN", "ERR", "CRT", "???"]; // Indexed by LogLevel byte value
    }

    /// <inheritdoc/>
    public string Format(in LogEntry entry, bool forFile)
    {
        // Using a StringBuilder with a reasonable initial capacity is a good balance
        // of performance and simplicity for most logging scenarios.
        var sb = new StringBuilder(256);

        // Append standard header: [Timestamp] [Level] [Source]
        AppendHeader(sb, entry);

        // Render the main message by parsing the template and its arguments.
        RenderMessage(sb, entry);

        // Append supplementary details if they exist.
        if (entry.Exception is not null)
        {
            AppendException(sb, entry.Exception, forFile);
        }

        if (_includeCallerInfo && !string.IsNullOrEmpty(entry.CallerMember))
        {
            AppendCallerInfo(sb, entry);
        }

        return sb.ToString();
    }

    private void AppendHeader(StringBuilder sb, in LogEntry entry)
    {
        sb.Append('[')
          .Append(entry.TimeStamp.ToLocalTime().ToString(_timeFormat))
          .Append("] [")
          .Append(GetLevelString(entry.Level))
          .Append("] [")
          .Append(entry.Source)
          .Append("] ");
    }

    private void RenderMessage(StringBuilder sb, in LogEntry entry)
    {
        var template = entry.MessageTemplate;
        if (string.IsNullOrEmpty(template)) return;

        var args = entry.Args;
        if (args is null || args.Length == 0)
        {
            sb.Append(template);
            return;
        }

        // Separate arguments into positional ones and a potential source for named ones.
        ReadOnlySpan<object?> positionalArgs = args;
        Dictionary<string, object?>? namedArgs = null;

        if (args.Length > 0 && args[^1] is { } lastArg)
        {
            var lastArgType = lastArg.GetType();
            if (!lastArgType.IsPrimitive && lastArg is not string)
            {
                namedArgs = ExtractNamedArgs(lastArg);
                if (namedArgs.Count > 0)
                {
                    positionalArgs = args.AsSpan(0, args.Length - 1);
                }
            }
        }

        var templateSpan = template.AsSpan();
        int lastIndex = 0;
        int positionalArgIndex = 0;
        var usedNamedArgs = namedArgs is not null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;

        while (lastIndex < templateSpan.Length)
        {
            int openBrace = templateSpan[lastIndex..].IndexOf('{');
            if (openBrace == -1)
            {
                sb.Append(templateSpan[lastIndex..]);
                break;
            }

            sb.Append(templateSpan.Slice(lastIndex, openBrace));
            openBrace += lastIndex;

            if (openBrace + 1 < templateSpan.Length && templateSpan[openBrace + 1] == '{')
            {
                sb.Append('{'); // Escaped brace
                lastIndex = openBrace + 2;
                continue;
            }

            int closeBrace = templateSpan[openBrace..].IndexOf('}');
            if (closeBrace == -1)
            {
                sb.Append(templateSpan[openBrace..]); // No closing brace
                break;
            }
            closeBrace += openBrace;

            var placeholder = templateSpan.Slice(openBrace + 1, closeBrace - openBrace - 1);

            if (!IsPositionalPlaceholder(placeholder))
            {
                string name = placeholder.ToString();
                if (namedArgs is not null && namedArgs.TryGetValue(name, out var value))
                {
                    sb.Append(value ?? "<null>");
                    usedNamedArgs?.Add(name); // Mark as used
                }
                else if (positionalArgIndex < positionalArgs.Length)
                {
                    sb.Append(positionalArgs[positionalArgIndex++] ?? "<null>");
                }
                else
                {
                    sb.Append('{').Append(placeholder).Append('}');
                }
            }
            else
            {
                if (positionalArgIndex < positionalArgs.Length)
                {
                    sb.Append(positionalArgs[positionalArgIndex++] ?? "<null>");
                }
                else
                {
                    sb.Append('{').Append(placeholder).Append('}');
                }
            }

            lastIndex = closeBrace + 1;
        }

        bool hasAppendedExtra = false;

        // Append remaining positional arguments
        if (positionalArgIndex < positionalArgs.Length)
        {
            sb.Append(" (Extra args: ");
            hasAppendedExtra = true;
            for (int i = positionalArgIndex; i < positionalArgs.Length; i++)
            {
                sb.Append(positionalArgs[i] ?? "<null>");
                if (i < positionalArgs.Length - 1) sb.Append(", ");
            }
        }

        // Append remaining named arguments
        if (namedArgs is not null && usedNamedArgs is not null && usedNamedArgs.Count < namedArgs.Count)
        {
            if (!hasAppendedExtra)
            {
                sb.Append(" (Extra args: ");
                hasAppendedExtra = true;
            }
            else
            {
                sb.Append(", ");
            }

            var remainingNamed = namedArgs.Where(kvp => !usedNamedArgs.Contains(kvp.Key)).ToArray();
            for (int i = 0; i < remainingNamed.Length; i++)
            {
                var kvp = remainingNamed[i];
                sb.Append(kvp.Key).Append('=').Append(kvp.Value ?? "<null>");
                if (i < remainingNamed.Length - 1) sb.Append(", ");
            }
        }

        if (hasAppendedExtra)
        {
            sb.Append(')');
        }
    }

    private static Dictionary<string, object?> ExtractNamedArgs(object source)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry kv in dictionary)
            {
                if (kv.Key is string key) dict[key] = kv.Value;
            }
            return dict;
        }

        var type = source.GetType();
        var properties = _propertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        foreach (var prop in properties)
        {
            dict[prop.Name] = prop.GetValue(source);
        }

        return dict;
    }

    private void AppendException(StringBuilder sb, Exception ex, bool forFile)
    {
        sb.Append(forFile ? Environment.NewLine : " | ").Append("Exception: ").Append(ex.Message);
        if (forFile)
        {
            sb.AppendLine().AppendLine("--- STACK TRACE ---").AppendLine(ex.ToString()).AppendLine("-------------------");
        }
    }

    private void AppendCallerInfo(StringBuilder sb, in LogEntry entry)
    {
        sb.Append(" (at ").Append(entry.CallerMember);
        if (!string.IsNullOrEmpty(entry.CallerFile))
        {
            sb.Append(" in ").Append(Path.GetFileName(entry.CallerFile)).Append(':').Append(entry.CallerLine);
        }
        sb.Append(')');
    }

    private static bool IsPositionalPlaceholder(ReadOnlySpan<char> placeholder)
    {
        if (placeholder.IsEmpty) return true; // {} is positional
        foreach (char c in placeholder)
        {
            if (!char.IsDigit(c)) return false;
        }
        return true;
    }

    private string GetLevelString(LogLevel level)
    {
        byte index = (byte)level;
        return index < _levelStrings.Length ? _levelStrings[index] : _levelStrings[^1];
    }
}