using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Coralph;

/// <summary>
/// Configures and provides access to the Serilog logger.
/// </summary>
internal static class Logging
{
    private const string LogDirectory = "logs";
    private const string LogFilePattern = "coralph-.log";

    /// <summary>
    /// Configures the global Serilog logger with console and file sinks.
    /// Console uses plain text output to avoid interfering with Spectre.Console.
    /// File output uses compact JSON format for machine parsing.
    /// </summary>
    /// <param name="options">Loop options containing configuration</param>
    public static void Configure(LoopOptions options)
    {
        Directory.CreateDirectory(LogDirectory);

        var logConfig = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Coralph")
            .Enrich.WithProperty("Version", Banner.GetVersion())
            .Enrich.WithProperty("Model", options.Model);

        // Write structured JSON logs to file for machine parsing
        var logPath = Path.Combine(LogDirectory, LogFilePattern);
        logConfig.WriteTo.File(
            new CompactJsonFormatter(),
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7);

        Log.Logger = logConfig.CreateLogger();
    }

    /// <summary>
    /// Closes and flushes the logger.
    /// </summary>
    public static void Close()
    {
        Log.CloseAndFlush();
    }
}
