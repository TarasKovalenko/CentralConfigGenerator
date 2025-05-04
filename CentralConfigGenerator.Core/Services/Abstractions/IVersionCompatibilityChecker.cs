using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Services.Abstractions;

public interface IVersionCompatibilityChecker
{
    Task<CompatibilityCheckResult> CheckCompatibilityAsync(string packageId, string version);
}