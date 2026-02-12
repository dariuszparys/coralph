namespace Coralph.Ui;

internal enum UiMode
{
    Auto,
    Tui,
    Classic
}

internal static class UiModeParser
{
    internal const string HelpText = "auto|tui|classic";

    internal static bool TryParse(string? value, out UiMode mode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "auto":
                mode = UiMode.Auto;
                return true;
            case "tui":
                mode = UiMode.Tui;
                return true;
            case "classic":
                mode = UiMode.Classic;
                return true;
            default:
                mode = UiMode.Auto;
                return false;
        }
    }

    internal static string ToCliValue(UiMode mode)
    {
        return mode switch
        {
            UiMode.Auto => "auto",
            UiMode.Tui => "tui",
            UiMode.Classic => "classic",
            _ => "auto"
        };
    }
}
