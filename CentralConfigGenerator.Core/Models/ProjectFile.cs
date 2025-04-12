namespace CentralConfigGenerator.Core.Models;

public record ProjectFile
{
    public string Path { get; set; } = string.Empty;
    
    public string Content { get; set; } = string.Empty;
}