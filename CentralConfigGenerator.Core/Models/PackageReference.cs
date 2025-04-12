namespace CentralConfigGenerator.Core.Models;

public record PackageReference
{
    public string Name { get; set; } = string.Empty;
    
    public string Version { get; set; } = string.Empty;
    
    public bool IsPrivateAssets { get; set; }
}