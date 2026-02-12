namespace Coralph.Ui;

internal static class UiModeResolver
{
    internal static UiMode Resolve(
        UiMode requestedMode,
        bool streamEvents,
        bool isInputRedirected,
        bool isOutputRedirected,
        bool isErrorRedirected)
    {
        if (streamEvents)
        {
            return UiMode.Classic;
        }

        if (isInputRedirected || isOutputRedirected || isErrorRedirected)
        {
            return UiMode.Classic;
        }

        return requestedMode switch
        {
            UiMode.Auto => UiMode.Tui,
            UiMode.Tui => UiMode.Tui,
            UiMode.Classic => UiMode.Classic,
            _ => UiMode.Classic
        };
    }

    internal static UiMode Resolve(LoopOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        return Resolve(
            options.UiMode,
            options.StreamEvents,
            Console.IsInputRedirected,
            Console.IsOutputRedirected,
            Console.IsErrorRedirected);
    }
}
