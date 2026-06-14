using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using MicrosoftLogger = Microsoft.Extensions.Logging.ILogger;

namespace Coralph;

internal sealed class CopilotSdkLogger : MicrosoftLogger
{
    internal static readonly MicrosoftLogger Instance = new CopilotSdkLogger();

    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext("SourceContext", "GitHub.Copilot.SDK");

    private CopilotSdkLogger()
    {
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel)
    {
        var serilogLevel = MapLevel(logLevel);
        return serilogLevel.HasValue && _logger.IsEnabled(serilogLevel.Value);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        var serilogLevel = MapLevel(logLevel);
        if (!serilogLevel.HasValue)
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        _logger
            .ForContext("CopilotSdkEventId", eventId.Id)
            .ForContext("CopilotSdkEventName", eventId.Name)
            .Write(serilogLevel.Value, exception, "Copilot SDK: {CopilotSdkMessage}", message);
    }

    private static LogEventLevel? MapLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            LogLevel.None => null,
            _ => LogEventLevel.Information
        };
    }

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
