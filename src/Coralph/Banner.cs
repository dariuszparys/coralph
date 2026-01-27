using Spectre.Console;

namespace Coralph;

internal static class Banner
{
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
    }

    internal static void Display(IAnsiConsole console)
    {
        if (Console.IsOutputRedirected)
        {
            foreach (var line in AsciiLines)
            {
                console.WriteLine(line);
            }
            return;
        }

        for (var lineIndex = 0; lineIndex < AsciiLines.Length; lineIndex++)
        {
            var line = AsciiLines[lineIndex];
            var colorIndex = lineIndex % GradientColors.Length;
            var color = GradientColors[colorIndex];
            
            console.MarkupLine($"[rgb({color.R},{color.G},{color.B})]{Markup.Escape(line)}[/]");
        }
    }
}
