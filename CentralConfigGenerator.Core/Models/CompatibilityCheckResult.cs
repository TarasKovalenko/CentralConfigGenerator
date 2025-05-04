namespace CentralConfigGenerator.Core.Models;

public class CompatibilityCheckResult
{
    public bool IsCompatible { get; set; }
    public List<string> Issues { get; set; } = [];
    public string? SuggestedVersion { get; set; }
}