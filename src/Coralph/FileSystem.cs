namespace Coralph;

/// <summary>
/// Default file system implementation that delegates to System.IO.File.
/// </summary>
internal sealed class FileSystem : IFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) =>
        File.ReadAllTextAsync(path, ct);

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    public long GetFileLength(string path) => new FileInfo(path).Length;
}
