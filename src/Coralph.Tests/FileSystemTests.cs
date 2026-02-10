using Xunit;

namespace Coralph.Tests;

public sealed class FileSystemTests
{
    [Fact]
    public async Task FileContentCache_WithMockFileSystem_UsesProvidedImplementation()
    {
        var mockFs = new MockFileSystem();
        mockFs.AddFile("/test.txt", "mock content");
        var cache = new FileContentCache(mockFs);

        var result = await cache.TryReadTextAsync("/test.txt");

        Assert.True(result.Exists);
        Assert.Equal("mock content", result.Content);
    }

    [Fact]
    public async Task FileContentCache_WithMockFileSystem_ReturnsMissingForNonExistentFile()
    {
        var mockFs = new MockFileSystem();
        var cache = new FileContentCache(mockFs);

        var result = await cache.TryReadTextAsync("/nonexistent.txt");

        Assert.False(result.Exists);
        Assert.Equal(string.Empty, result.Content);
    }

    private sealed class MockFileSystem : IFileSystem
    {
        private readonly Dictionary<string, FileEntry> _files = new();

        public void AddFile(string path, string content)
        {
            _files[Normalize(path)] = new FileEntry(content, DateTime.UtcNow, content.Length);
        }

        public bool Exists(string path) => _files.ContainsKey(Normalize(path));

        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default)
        {
            if (!_files.TryGetValue(Normalize(path), out var entry))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            return Task.FromResult(entry.Content);
        }

        public Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default)
        {
            _files[Normalize(path)] = new FileEntry(contents, DateTime.UtcNow, contents.Length);
            return Task.CompletedTask;
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            return _files.TryGetValue(Normalize(path), out var entry) ? entry.LastWriteUtc : DateTime.MinValue;
        }

        public long GetFileLength(string path)
        {
            return _files.TryGetValue(Normalize(path), out var entry) ? entry.Length : 0;
        }

        private static string Normalize(string path) => Path.GetFullPath(path);

        private record FileEntry(string Content, DateTime LastWriteUtc, long Length);
    }
}
