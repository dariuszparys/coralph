using System.Text.Json;
using System.Text.Json.Serialization;
using GitHub.Copilot.SDK;
using Serilog;

namespace Coralph;

internal static class CopilotModelDiscovery
{
    internal static async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(LoopOptions opt, CancellationToken ct)
    {
        var clientOptions = new CopilotClientOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
        };

        if (!string.IsNullOrWhiteSpace(opt.CliPath)) clientOptions.CliPath = opt.CliPath;
        if (!string.IsNullOrWhiteSpace(opt.CliUrl)) clientOptions.CliUrl = opt.CliUrl;
        if (!string.IsNullOrWhiteSpace(opt.CopilotToken)) clientOptions.GithubToken = opt.CopilotToken;

        await using var client = new CopilotClient(clientOptions);
        var started = false;

        try
        {
            await client.StartAsync();
            started = true;
            return await client.ListModelsAsync(ct);
        }
        finally
        {
            if (started)
            {
                try
                {
                    await client.StopAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to stop Copilot client");
                }
            }
        }
    }

    internal static void WriteModels(IEnumerable<ModelInfo> models)
    {
        ConsoleOutput.WriteLine("Available models:");

        var rows = models
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .Select(model => new ModelRow(
                model.Id ?? string.Empty,
                model.Name ?? string.Empty,
                model.Capabilities?.Supports?.Vision == true ? "yes" : "no",
                FormatNullable(model.Capabilities?.Limits?.MaxContextWindowTokens),
                FormatNullable(model.Capabilities?.Limits?.MaxPromptTokens),
                FormatNullable(model.Billing?.Multiplier),
                model.Policy?.State ?? string.Empty))
            .ToList();

        if (rows.Count == 0)
        {
            ConsoleOutput.WriteLine("(none)");
            return;
        }

        var idWidth = Math.Max("id".Length, rows.Max(r => r.Id.Length));
        var nameWidth = Math.Max("name".Length, rows.Max(r => r.Name.Length));
        var visionWidth = Math.Max("vision".Length, rows.Max(r => r.Vision.Length));
        var maxContextWidth = Math.Max("max_context_tokens".Length, rows.Max(r => r.MaxContextTokens.Length));
        var maxPromptWidth = Math.Max("max_prompt_tokens".Length, rows.Max(r => r.MaxPromptTokens.Length));
        var billingWidth = Math.Max("billing_multiplier".Length, rows.Max(r => r.BillingMultiplier.Length));
        var policyWidth = Math.Max("policy_state".Length, rows.Max(r => r.PolicyState.Length));

        var header = string.Join("  ", new[]
        {
            PadRight("id", idWidth),
            PadRight("name", nameWidth),
            PadRight("vision", visionWidth),
            PadRight("max_context_tokens", maxContextWidth),
            PadRight("max_prompt_tokens", maxPromptWidth),
            PadRight("billing_multiplier", billingWidth),
            PadRight("policy_state", policyWidth)
        });

        ConsoleOutput.WriteLine(header);

        foreach (var row in rows)
        {
            var line = string.Join("  ", new[]
            {
                PadRight(row.Id, idWidth),
                PadRight(row.Name, nameWidth),
                PadRight(row.Vision, visionWidth),
                PadRight(row.MaxContextTokens, maxContextWidth),
                PadRight(row.MaxPromptTokens, maxPromptWidth),
                PadRight(row.BillingMultiplier, billingWidth),
                PadRight(row.PolicyState, policyWidth)
            });

            ConsoleOutput.WriteLine(line);
        }
    }

    private static string FormatNullable(int? value) => value?.ToString() ?? "-";

    private static string FormatNullable(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.###") : "-";
    }

    internal static void WriteModelsJson(IEnumerable<ModelInfo> models)
    {
        var payload = models
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .Select(model => new ModelInfoDto(
                model.Id,
                model.Name,
                model.Capabilities is null
                    ? null
                    : new ModelCapabilitiesDto(
                        model.Capabilities.Supports is null
                            ? null
                            : new ModelSupportsDto(model.Capabilities.Supports.Vision),
                        model.Capabilities.Limits is null
                            ? null
                            : new ModelLimitsDto(
                                model.Capabilities.Limits.MaxPromptTokens,
                                model.Capabilities.Limits.MaxContextWindowTokens,
                                model.Capabilities.Limits.Vision is null
                                    ? null
                                    : new ModelVisionLimitsDto(
                                        model.Capabilities.Limits.Vision.SupportedMediaTypes,
                                        model.Capabilities.Limits.Vision.MaxPromptImages,
                                        model.Capabilities.Limits.Vision.MaxPromptImageSize)))),
                model.Policy is null
                    ? null
                    : new ModelPolicyDto(model.Policy.State, model.Policy.Terms),
                model.Billing is null
                    ? null
                    : new ModelBillingDto(model.Billing.Multiplier)))
            .ToList();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        ConsoleOutput.WriteLine(JsonSerializer.Serialize(payload, options));
    }

    private static string PadRight(string value, int width)
    {
        if (value.Length >= width)
        {
            return value;
        }

        return value.PadRight(width);
    }

    private readonly record struct ModelRow(
        string Id,
        string Name,
        string Vision,
        string MaxContextTokens,
        string MaxPromptTokens,
        string BillingMultiplier,
        string PolicyState);

    private sealed record ModelInfoDto(
        string? Id,
        string? Name,
        ModelCapabilitiesDto? Capabilities,
        ModelPolicyDto? Policy,
        ModelBillingDto? Billing);

    private sealed record ModelCapabilitiesDto(
        ModelSupportsDto? Supports,
        ModelLimitsDto? Limits);

    private sealed record ModelSupportsDto(
        bool Vision);

    private sealed record ModelLimitsDto(
        int? MaxPromptTokens,
        int MaxContextWindowTokens,
        ModelVisionLimitsDto? Vision);

    private sealed record ModelVisionLimitsDto(
        IReadOnlyList<string>? SupportedMediaTypes,
        int MaxPromptImages,
        int MaxPromptImageSize);

    private sealed record ModelPolicyDto(
        string? State,
        string? Terms);

    private sealed record ModelBillingDto(
        double Multiplier);
}
