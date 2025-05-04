using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Analyzers.Abstractions;

public interface IEnhancedPackageAnalyzer
{
    PackageAnalysisResult AnalyzePackages(IEnumerable<ProjectFile> projectFiles);
}