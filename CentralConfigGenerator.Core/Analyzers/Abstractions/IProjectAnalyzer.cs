using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Analyzers.Abstractions;

public interface IProjectAnalyzer
{
    Dictionary<string, string> ExtractCommonProperties(IReadOnlyCollection<ProjectFile> projectFiles);
}