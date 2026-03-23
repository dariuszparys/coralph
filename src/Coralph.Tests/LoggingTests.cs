using System.IO;
using Coralph;
using Xunit;

namespace Coralph.Tests;

[CollectionDefinition("Logging", DisableParallelization = true)]
public sealed class LoggingCollection : ICollectionFixture<LoggingFixture>
{
}

public sealed class LoggingFixture : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Logging.Close();
        return Task.CompletedTask;
    }
}

[Collection("Logging")]
public class LoggingTests
{
    [Fact]
    public void Configure_CreatesLogDirectory()
    {
        // Arrange
        var options = new LoopOptions { Model = "test-model" };
        var originalDirectory = Directory.GetCurrentDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), $"coralph-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            Directory.SetCurrentDirectory(tempDir);

            // Act
            Logging.Configure(options);

            // Assert
            var logDir = Path.Combine(tempDir, "logs");
            Assert.True(Directory.Exists(logDir));
        }
        finally
        {
            Logging.Close();
            Directory.SetCurrentDirectory(originalDirectory);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Configure_DoesNotThrowWithDefaultOptions()
    {
        // Arrange
        var options = new LoopOptions();

        // Act & Assert - should not throw
        Logging.Configure(options);
        Logging.Close();
    }
}
