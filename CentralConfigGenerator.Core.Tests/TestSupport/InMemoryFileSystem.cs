using System.Text;
using CentralConfigGenerator.Core.IO;

namespace CentralConfigGenerator.Core.Tests.TestSupport;

/// <summary>
/// An in-memory <see cref="IFileSystem"/> so the file-touching services can be tested without
/// going near a real disk.
/// </summary>
public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryFileSystem AddFile(string path, string content)
    {
        var full = Normalize(path);
        _files[full] = new UTF8Encoding(false).GetBytes(content);
        AddParentDirectories(full);
        return this;
    }

    public InMemoryFileSystem AddFile(string path, byte[] bytes)
    {
        var full = Normalize(path);
        _files[full] = bytes;
        AddParentDirectories(full);
        return this;
    }

    public InMemoryFileSystem AddDirectory(string path)
    {
        var full = Normalize(path);
        _directories.Add(full);
        AddParentDirectories(full);
        return this;
    }

    public string ReadText(string path) =>
        new UTF8Encoding(false).GetString(_files[Normalize(path)]);

    public byte[] ReadBytes(string path) => _files[Normalize(path)];

    public IReadOnlyCollection<string> Files => _files.Keys;

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public void CreateDirectory(string path) => AddDirectory(path);

    public void DeleteFile(string path) => _files.Remove(Normalize(path));

    public void DeleteDirectory(string path, bool recursive)
    {
        var prefix = Normalize(path) + Path.DirectorySeparatorChar;

        foreach (var file in _files.Keys.Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _files.Remove(file);
        }

        foreach (var directory in _directories.Where(d => d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _directories.Remove(directory);
        }

        _directories.Remove(Normalize(path));
    }

    public IReadOnlyList<string> EnumerateFiles(string path, string searchPattern, bool recursive)
    {
        var prefix = Normalize(path) + Path.DirectorySeparatorChar;

        return _files
            .Keys.Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Where(f =>
                recursive
                || !f[prefix.Length..].Contains(Path.DirectorySeparatorChar)
            )
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        var prefix = Normalize(path) + Path.DirectorySeparatorChar;

        return _directories
            .Where(d => d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Where(d => !d[prefix.Length..].Contains(Path.DirectorySeparatorChar))
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files[Normalize(path)]);

    public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
    {
        var full = Normalize(path);
        _files[full] = bytes;
        AddParentDirectories(full);
        return Task.CompletedTask;
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReadText(path));

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
    {
        AddFile(path, contents);
        return Task.CompletedTask;
    }

    public void CopyFile(string source, string destination, bool overwrite)
    {
        var full = Normalize(destination);
        _files[full] = _files[Normalize(source)];
        AddParentDirectories(full);
    }

    public DateTimeOffset GetLastWriteTime(string path) => DateTimeOffset.UtcNow;

    public long GetFileSize(string path) => _files[Normalize(path)].Length;

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    private void AddParentDirectories(string path)
    {
        // Every ancestor must be registered, even if a sibling already registered one of them.
        for (
            var directory = Path.GetDirectoryName(path);
            !string.IsNullOrEmpty(directory);
            directory = Path.GetDirectoryName(directory)
        )
        {
            _directories.Add(directory);
        }
    }
}
