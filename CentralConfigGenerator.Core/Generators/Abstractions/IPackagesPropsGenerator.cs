namespace CentralConfigGenerator.Core.Generators.Abstractions;

public interface IPackagesPropsGenerator
{
    string GeneratePackagesPropsContent(Dictionary<string, string> packageVersions);
}