using Coralph.Ui;

namespace Coralph.Tests;

public class UiModeResolverTests
{
    [Fact]
    public void Resolve_AutoInteractive_UsesTui()
    {
        var mode = UiModeResolver.Resolve(
            requestedMode: UiMode.Auto,
            streamEvents: false,
            isInputRedirected: false,
            isOutputRedirected: false,
            isErrorRedirected: false);

        Assert.Equal(UiMode.Tui, mode);
    }

    [Fact]
    public void Resolve_Redirected_UsesClassic()
    {
        var mode = UiModeResolver.Resolve(
            requestedMode: UiMode.Tui,
            streamEvents: false,
            isInputRedirected: false,
            isOutputRedirected: true,
            isErrorRedirected: false);

        Assert.Equal(UiMode.Classic, mode);
    }

    [Fact]
    public void Resolve_StreamEvents_ForcesClassic()
    {
        var mode = UiModeResolver.Resolve(
            requestedMode: UiMode.Tui,
            streamEvents: true,
            isInputRedirected: false,
            isOutputRedirected: false,
            isErrorRedirected: false);

        Assert.Equal(UiMode.Classic, mode);
    }

    [Fact]
    public void Resolve_ExplicitClassic_StaysClassic()
    {
        var mode = UiModeResolver.Resolve(
            requestedMode: UiMode.Classic,
            streamEvents: false,
            isInputRedirected: false,
            isOutputRedirected: false,
            isErrorRedirected: false);

        Assert.Equal(UiMode.Classic, mode);
    }
}
