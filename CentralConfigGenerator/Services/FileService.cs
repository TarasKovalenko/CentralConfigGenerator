namespace CentralConfigGenerator.Services;

public class FileService : IFileService
{
    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public async Task<string> ReadAllTextAsync(string path)
    {
        return await File.ReadAllTextAsync(path);
    }

    public async Task WriteAllTextAsync(string path, string contents)
    {
        await File.WriteAllTextAsync(path, contents);
    }
}

public interface IFileService
{
    bool Exists(string path);
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string contents);
}