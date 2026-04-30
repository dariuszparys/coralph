using System.Text.Json;
using Coralph;
using GitHub.Copilot.SDK;
using Spectre.Console.Testing;

namespace Coralph.Tests;

[Collection("ConsoleOutput")]
public sealed class CopilotModelDiscoveryTests
{
    [Fact]
    public void WriteModels_WithNullableTokenLimits_FormatsValuesAndNulls()
    {
        var console = new TestConsole();
        ConsoleOutput.Configure(console, console);

        try
        {
            CopilotModelDiscovery.WriteModels(
            [
                CreateModel(maxContextTokens: 128_000, maxPromptTokens: null)
            ]);

            Assert.Contains("128000", console.Output);
            Assert.Contains("-", console.Output);
        }
        finally
        {
            ConsoleOutput.Reset();
        }
    }

    [Fact]
    public void WriteModelsJson_WithNullableTokenLimits_EmitsValuesAndOmitsNulls()
    {
        var console = new TestConsole();
        ConsoleOutput.Configure(console, console);

        try
        {
            CopilotModelDiscovery.WriteModelsJson(
            [
                CreateModel(maxContextTokens: 128_000, maxPromptTokens: null)
            ]);

            using var doc = JsonDocument.Parse(console.Output);
            var limits = doc.RootElement[0]
                .GetProperty("Capabilities")
                .GetProperty("Limits");

            Assert.Equal(128_000, limits.GetProperty("MaxContextWindowTokens").GetInt32());
            Assert.False(limits.TryGetProperty("MaxPromptTokens", out _));
        }
        finally
        {
            ConsoleOutput.Reset();
        }
    }

    private static ModelInfo CreateModel(int maxContextTokens, int? maxPromptTokens)
    {
        return new ModelInfo
        {
            Id = "gpt-test",
            Name = "GPT Test",
            Capabilities = new ModelCapabilities
            {
                Supports = new ModelSupports
                {
                    Vision = false
                },
                Limits = new ModelLimits
                {
                    MaxContextWindowTokens = maxContextTokens,
                    MaxPromptTokens = maxPromptTokens
                }
            },
            Policy = new ModelPolicy
            {
                State = "enabled",
                Terms = "test"
            },
            Billing = new ModelBilling
            {
                Multiplier = 1
            }
        };
    }
}
