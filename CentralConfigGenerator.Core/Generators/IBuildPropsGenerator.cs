namespace CentralConfigGenerator.Core.Generators;

public interface IBuildPropsGenerator
{
    string GenerateBuildPropsContent(Dictionary<string, string> commonProperties);
}