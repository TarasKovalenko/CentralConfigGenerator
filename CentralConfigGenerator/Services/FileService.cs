namespace CentralConfigGenerator.Services;

public class FileService : IFileService
{
    public bool Exists(string path) => File.Exists(path);

    public async Task<string> ReadAllTextAsync(string path) => await File.ReadAllTextAsync(path);

    public async Task WriteAllTextAsync(string path, string contents) => await File.WriteAllTextAsync(path, contents);
}

public interface IFileService
{
    bool Exists(string path);
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string contents);
}