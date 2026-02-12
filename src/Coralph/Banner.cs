using System.Reflection;
using Spectre.Console;

namespace Coralph;

internal static class Banner
{
    internal static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(infoVersion))
        {
            // Remove build metadata suffix (e.g., "+abc123") if present
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex >= 0 ? infoVersion[..plusIndex] : infoVersion;
        }
        return assembly.GetName().Version?.ToString() ?? "unknown";
    }
    // ASCII art for "Coralph" - stylized and compact
    private static readonly string[] AsciiLines =
    [
        @"  ____                 _       _     ",
        @" / ___|___  _ __ __ _ | |_ __ | |__  ",
        @"| |   / _ \| '__/ _` || | '_ \| '_ \ ",
        @"| |__| (_) | | | (_| || | |_) | | | |",
        @" \____\___/|_|  \__,_||_| .__/|_| |_|",
        @"                        |_|          ",
    ];

    // Gradient colors for the banner animation
    private static readonly Color[] GradientColors =
    [
        new Color(255, 100, 100),  // Coral/Red
        new Color(255, 150, 100),  // Orange
        new Color(255, 200, 100),  // Yellow-Orange
        new Color(100, 200, 255),  // Light Blue
        new Color(150, 100, 255),  // Purple
    ];

    internal static async Task DisplayAnimatedAsync(IAnsiConsole console, CancellationToken ct = default)
    {
        if (Console.IsOutputRedirected)
        {
            // Fallback to simple text output when redirected
            foreach (var line in AsciiLines)
            {
                console.WriteLine(line);
            }
            console.WriteLine($"v{GetVersion()}");
            return;
        }

        // Animated reveal: display each line with a gradient sweep effect
        for (var lineIndex = 0; lineIndex < AsciiLines.Length; lineIndex++)
        {
            var line = AsciiLines[lineIndex];
            var colorIndex = lineIndex % GradientColors.Length;
            var color = GradientColors[colorIndex];

            console.MarkupLine($"[rgb({color.R},{color.G},{color.B})]{Markup.Escape(line)}[/]");

            // Small delay between lines for animation effect
            try
            {
                await Task.Delay(50, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        // Display version after banner
        console.MarkupLine($"[dim]v{GetVersion()}[/]");
    }

    internal static async Task DisplayAnimatedInOutputAsync(CancellationToken ct = default)
    {
        if (Console.IsOutputRedirected)
        {
            foreach (var line in AsciiLines)
            {
                ConsoleOutput.WriteLine(line);
            }

            ConsoleOutput.WriteLine($"v{GetVersion()}");
            return;
        }

        for (var lineIndex = 0; lineIndex < AsciiLines.Length; lineIndex++)
        {
            var line = AsciiLines[lineIndex];
            var colorIndex = lineIndex % GradientColors.Length;
            var color = GradientColors[colorIndex];

            ConsoleOutput.MarkupLine($"[rgb({color.R},{color.G},{color.B})]{Markup.Escape(line)}[/]");

            try
            {
                await Task.Delay(50, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        ConsoleOutput.MarkupLine($"[dim]v{GetVersion()}[/]");
    }

    internal static void Display(IAnsiConsole console)
    {
        if (Console.IsOutputRedirected)
        {
            foreach (var line in AsciiLines)
            {
                console.WriteLine(line);
            }
            console.WriteLine($"v{GetVersion()}");
            return;
        }

        for (var lineIndex = 0; lineIndex < AsciiLines.Length; lineIndex++)
        {
            var line = AsciiLines[lineIndex];
            var colorIndex = lineIndex % GradientColors.Length;
            var color = GradientColors[colorIndex];

            console.MarkupLine($"[rgb({color.R},{color.G},{color.B})]{Markup.Escape(line)}[/]");
        }

        // Display version after banner
        console.MarkupLine($"[dim]v{GetVersion()}[/]");
    }
}
