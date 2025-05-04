using CentralConfigGenerator.Core.Analyzers;

namespace CentralConfigGenerator.Core.Services.Abstractions;

public interface IVersionConflictVisualizer
{
    void DisplayResults(PackageAnalysisResult result);
}