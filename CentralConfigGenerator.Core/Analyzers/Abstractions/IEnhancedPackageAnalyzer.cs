using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Analyzers.Abstractions;

public interface IEnhancedPackageAnalyzer
{
    Task<PackageAnalysisResult> AnalyzePackagesAsync(IEnumerable<ProjectFile> projectFiles);
}
