using Coralph;

namespace Coralph.Tests;

[CollectionDefinition("ConsoleOutput", DisableParallelization = true)]
public sealed class ConsoleOutputCollection : ICollectionFixture<ConsoleOutputFixture>
{
}

public sealed class ConsoleOutputFixture : IDisposable
{
    public void Dispose()
    {
        ConsoleOutput.Reset();
    }
}
