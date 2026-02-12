using Coralph.Ui;
using Coralph.Ui.Tui;

namespace Coralph.Tests;

public class ProjectTypePromptTests
{
    [Fact]
    public async Task ClassicPrompt_UsesConsoleInputSelection()
    {
        var previousIn = Console.In;
        try
        {
            Console.SetIn(new StringReader("3\n"));
            var backend = new ClassicConsoleOutputBackend();

            var selected = await backend.PromptSelectionAsync(
                "Pick type",
                ["JavaScript", "Python", "Go"],
                defaultIndex: 0);

            Assert.Equal(2, selected);
        }
        finally
        {
            Console.SetIn(previousIn);
        }
    }

    [Fact]
    public async Task TuiPromptState_ReturnsSelectedIndex()
    {
        var state = new TuiState();

        var pending = state.RequestPromptSelectionAsync(
            "Pick type",
            ["JavaScript", "Python", "Go", "Rust"],
            defaultIndex: 0);

        state.UpdatePromptSelection(3);
        state.CompletePromptSelection(3);

        var selected = await pending;

        Assert.Equal(3, selected);
    }
}
