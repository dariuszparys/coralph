namespace Coralph;

/// <summary>
/// Abstraction for file system operations to improve testability.
/// </summary>
internal interface IFileSystem
{
    bool Exists(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken ct = default);
    DateTime GetLastWriteTimeUtc(string path);
    long GetFileLength(string path);
}
