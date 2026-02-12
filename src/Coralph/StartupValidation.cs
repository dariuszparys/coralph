namespace Coralph;

internal static class StartupValidation
{
    public static bool TryValidatePromptFile(string promptFile, out string? errorMessage)
    {
        var fullPromptPath = Path.GetFullPath(promptFile);
        if (File.Exists(fullPromptPath))
        {
            errorMessage = null;
            return true;
        }

        errorMessage = $"Prompt file not found: {fullPromptPath}. Run 'coralph --init' in this repository to create it.";
        return false;
    }
}
