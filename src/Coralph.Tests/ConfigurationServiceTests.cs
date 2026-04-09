using Coralph;
using Coralph.Ui;

namespace Coralph.Tests;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public void Merge_CliOverridesTakePrecedenceOverConfig()
    {
        var cli = new LoopOptionsOverrides
        {
            Model = "cli-model",
            UiMode = UiMode.Tui,
            ShowReasoning = false,
            DockerNetworkMode = "host"
        };
        var config = new LoopOptionsOverrides
        {
            Model = "config-model",
            UiMode = UiMode.Classic,
            ShowReasoning = true,
            DockerNetworkMode = "none",
            DockerMemoryLimit = "2g",
            DockerCpuLimit = "2"
        };

        var options = ConfigurationService.Merge(cli, config);

        Assert.Equal("cli-model", options.Model);
        Assert.Equal(UiMode.Tui, options.UiMode);
        Assert.False(options.ShowReasoning);
        Assert.Equal("host", options.DockerNetworkMode);
    }

    [Fact]
    public void Merge_ConfigValuesFillWhenCliIsUnset()
    {
        var cli = new LoopOptionsOverrides();
        var config = new LoopOptionsOverrides
        {
            MaxIterations = 25,
            PromptFile = "config-prompt.md",
            DockerSandbox = true,
            DockerNetworkMode = "bridge",
            DockerMemoryLimit = "4g",
            DockerCpuLimit = "1.5"
        };

        var options = ConfigurationService.Merge(cli, config);

        Assert.Equal(25, options.MaxIterations);
        Assert.Equal("config-prompt.md", options.PromptFile);
        Assert.True(options.DockerSandbox);
        Assert.Equal("bridge", options.DockerNetworkMode);
        Assert.Equal("4g", options.DockerMemoryLimit);
        Assert.Equal("1.5", options.DockerCpuLimit);
    }

    [Fact]
    public void Merge_DefaultsFillWhenCliAndConfigAreUnset()
    {
        var defaults = new LoopOptions();

        var options = ConfigurationService.Merge(new LoopOptionsOverrides(), new LoopOptionsOverrides());

        Assert.Equal(defaults.MaxIterations, options.MaxIterations);
        Assert.Equal(defaults.Model, options.Model);
        Assert.Equal(defaults.PromptFile, options.PromptFile);
        Assert.Equal(defaults.UiMode, options.UiMode);
        Assert.Equal(defaults.DockerSandbox, options.DockerSandbox);
        Assert.Equal(defaults.DockerNetworkMode, options.DockerNetworkMode);
        Assert.Equal(defaults.DockerMemoryLimit, options.DockerMemoryLimit);
        Assert.Equal(defaults.DockerCpuLimit, options.DockerCpuLimit);
    }

    [Fact]
    public void Merge_EmptyStringsDoNotOverrideConfigOrDefaults()
    {
        var cli = new LoopOptionsOverrides
        {
            Model = "",
            PromptFile = " "
        };
        var config = new LoopOptionsOverrides
        {
            Model = "config-model",
            PromptFile = "config-prompt.md"
        };

        var options = ConfigurationService.Merge(cli, config);

        Assert.Equal("config-model", options.Model);
        Assert.Equal("config-prompt.md", options.PromptFile);
    }

    [Fact]
    public void Merge_TelemetryValuesFollowOverridePrecedence()
    {
        var cli = new LoopOptionsOverrides
        {
            TelemetrySourceName = "cli-source",
            TelemetryCaptureContent = true
        };
        var config = new LoopOptionsOverrides
        {
            TelemetryOtlpEndpoint = "http://localhost:4318",
            TelemetrySourceName = "config-source",
            TelemetryCaptureContent = false
        };

        var options = ConfigurationService.Merge(cli, config);

        Assert.Equal("http://localhost:4318", options.TelemetryOtlpEndpoint);
        Assert.Equal("cli-source", options.TelemetrySourceName);
        Assert.True(options.TelemetryCaptureContent);
    }

    [Fact]
    public void LoadOptions_WithUiModeInConfig_BindsUiMode()
    {
        var tempPath = CreateTempConfig(
            """
            {
              "LoopOptions": {
                "UiMode": "classic"
              }
            }
            """);

        try
        {
            var options = ConfigurationService.LoadOptions(new LoopOptionsOverrides(), tempPath);

            Assert.Equal(UiMode.Classic, options.UiMode);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void LoadOptions_WithTelemetrySettingsInConfig_BindsTelemetryOptions()
    {
        var tempPath = CreateTempConfig(
            """
            {
              "LoopOptions": {
                "TelemetryOtlpEndpoint": "http://localhost:4318",
                "TelemetrySourceName": "coralph-config",
                "TelemetryCaptureContent": true
              }
            }
            """);

        try
        {
            var options = ConfigurationService.LoadOptions(new LoopOptionsOverrides(), tempPath);

            Assert.Equal("http://localhost:4318", options.TelemetryOtlpEndpoint);
            Assert.Equal("coralph-config", options.TelemetrySourceName);
            Assert.True(options.TelemetryCaptureContent);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void LoadOptions_WithUiModeOverride_OverrideWinsOverConfig()
    {
        var tempPath = CreateTempConfig(
            """
            {
              "LoopOptions": {
                "UiMode": "classic"
              }
            }
            """);

        try
        {
            var options = ConfigurationService.LoadOptions(new LoopOptionsOverrides { UiMode = UiMode.Tui }, tempPath);

            Assert.Equal(UiMode.Tui, options.UiMode);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void LoadOptions_AbsentJsonKeysDoNotOverrideDefaults()
    {
        var tempPath = CreateTempConfig(
            """
            {
              "LoopOptions": {
                "Model": "config-model"
              }
            }
            """);

        try
        {
            var options = ConfigurationService.LoadOptions(new LoopOptionsOverrides(), tempPath);

            Assert.Equal("config-model", options.Model);
            Assert.Equal(new LoopOptions().ProgressFile, options.ProgressFile);
            Assert.Equal(new LoopOptions().ShowReasoning, options.ShowReasoning);
            Assert.Equal(new LoopOptions().DockerNetworkMode, options.DockerNetworkMode);
            Assert.Equal(new LoopOptions().DockerMemoryLimit, options.DockerMemoryLimit);
            Assert.Equal(new LoopOptions().DockerCpuLimit, options.DockerCpuLimit);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void LoadOptions_WithDockerLimitsInConfig_BindsAndMerges()
    {
        var tempPath = CreateTempConfig(
            """
            {
              "LoopOptions": {
                "DockerNetworkMode": "bridge",
                "DockerMemoryLimit": "3g",
                "DockerCpuLimit": "1.25"
              }
            }
            """);

        try
        {
            var options = ConfigurationService.LoadOptions(new LoopOptionsOverrides(), tempPath);

            Assert.Equal("bridge", options.DockerNetworkMode);
            Assert.Equal("3g", options.DockerMemoryLimit);
            Assert.Equal("1.25", options.DockerCpuLimit);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static string CreateTempConfig(string json)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"coralph-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, json);
        return tempPath;
    }
}
