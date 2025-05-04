namespace CentralConfigGenerator.Core.Services.Abstractions;

public interface IVersionConflictResolver
{
    string Resolve(string packageName, IEnumerable<string> versions, VersionResolutionStrategy strategy);
}