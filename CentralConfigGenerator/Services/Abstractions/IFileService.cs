namespace CentralConfigGenerator.Services.Abstractions;

public interface IFileService
{
    bool Exists(string path);
    
    Task<string> ReadAllTextAsync(string path);
    
    Task WriteAllTextAsync(string path, string contents);
}