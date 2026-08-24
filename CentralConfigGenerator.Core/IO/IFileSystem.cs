using System.Text;

namespace CentralConfigGenerator.Core.IO;

/// <summary>
/// Thin file-system seam so every service in the core library stays testable.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    void DeleteFile(string path);

    void DeleteDirectory(string path, bool recursive);

    IReadOnlyList<string> EnumerateFiles(string path, string searchPattern, bool recursive);

    IReadOnlyList<string> EnumerateDirectories(string path);

    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);

    void CopyFile(string source, string destination, bool overwrite);

    DateTimeOffset GetLastWriteTime(string path);

    long GetFileSize(string path);
}

/// <summary>Default <see cref="IFileSystem"/> backed by <see cref="System.IO"/>.</summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public IReadOnlyList<string> EnumerateFiles(string path, string searchPattern, bool recursive) =>
        Directory
            .EnumerateFiles(
                path,
                searchPattern,
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
            )
            .ToList();

    public IReadOnlyList<string> EnumerateDirectories(string path) =>
        Directory.EnumerateDirectories(path).ToList();

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(path, cancellationToken);

    public async Task WriteAllBytesAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken = default
    )
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(path, cancellationToken);

    public async Task WriteAllTextAsync(
        string path,
        string contents,
        CancellationToken cancellationToken = default
    )
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, contents, new UTF8Encoding(false), cancellationToken);
    }

    public void CopyFile(string source, string destination, bool overwrite)
    {
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(source, destination, overwrite);
    }

    public DateTimeOffset GetLastWriteTime(string path) => new FileInfo(path).LastWriteTimeUtc;

    public long GetFileSize(string path) => new FileInfo(path).Length;
}
