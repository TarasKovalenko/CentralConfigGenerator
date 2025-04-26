using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Analyzers;

public interface IPackageAnalyzer
{
    Dictionary<string, string> ExtractPackageVersions(IEnumerable<ProjectFile> projectFiles);
}