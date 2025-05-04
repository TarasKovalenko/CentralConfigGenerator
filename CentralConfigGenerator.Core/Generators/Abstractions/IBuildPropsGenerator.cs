namespace CentralConfigGenerator.Core.Generators.Abstractions;

public interface IBuildPropsGenerator
{
    string GenerateBuildPropsContent(Dictionary<string, string> commonProperties);
}