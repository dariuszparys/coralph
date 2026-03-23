using Coralph;

namespace Coralph.Tests;

[CollectionDefinition("ConsoleOutput", DisableParallelization = true)]
public sealed class ConsoleOutputCollection : ICollectionFixture<ConsoleOutputFixture>
{
}

public sealed class ConsoleOutputFixture : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await ConsoleOutput.ResetAsync();
    }
}
