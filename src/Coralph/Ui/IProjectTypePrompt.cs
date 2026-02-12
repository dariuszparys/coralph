namespace Coralph.Ui;

internal interface IProjectTypePrompt
{
    Task<int> PromptSelectionAsync(
        string title,
        IReadOnlyList<string> options,
        int defaultIndex,
        CancellationToken ct = default);
}
