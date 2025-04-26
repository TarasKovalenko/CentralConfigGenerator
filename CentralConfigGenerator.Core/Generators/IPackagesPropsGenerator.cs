namespace CentralConfigGenerator.Core.Generators;

public interface IPackagesPropsGenerator
{
    string GeneratePackagesPropsContent(Dictionary<string, string> packageVersions);
}