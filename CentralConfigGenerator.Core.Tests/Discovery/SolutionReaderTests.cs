using CentralConfigGenerator.Core.Discovery;

namespace CentralConfigGenerator.Core.Tests.Discovery;

public class SolutionReaderTests
{
    [Theory]
    [InlineData("MySolution.sln", true)]
    [InlineData("MySolution.slnx", true)]
    [InlineData("MySolution.slnf", true)]
    [InlineData("MyProject.csproj", false)]
    public void IsSolution_ShouldRecogniseSolutionExtensions(string path, bool expected) =>
        SolutionReader.IsSolution(path).ShouldBe(expected);

    [Fact]
    public void ReadProjects_ShouldParseClassicSlnFormat()
    {
        var content = """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "src\App\App.csproj", "{GUID1}"
            EndProject
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{FOLDER}"
            EndProject
            Project("{F2A71F9B-5D33-465A-A702-920D77279786}") = "Lib", "src\Lib\Lib.fsproj", "{GUID2}"
            EndProject
            """;

        var projects = SolutionReader.ReadProjects(
            Path.Combine(Path.GetTempPath(), "MySolution.sln"),
            content
        );

        projects.Count.ShouldBe(2);
        projects.ShouldContain(p => p.EndsWith("App.csproj", StringComparison.Ordinal));
        projects.ShouldContain(p => p.EndsWith("Lib.fsproj", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadProjects_ShouldSkipSolutionFolders()
    {
        var content = """
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "tests", "tests", "{FOLDER}"
            EndProject
            """;

        SolutionReader
            .ReadProjects(Path.Combine(Path.GetTempPath(), "S.sln"), content)
            .ShouldBeEmpty();
    }

    [Fact]
    public void ReadProjects_ShouldParseSlnxFormat()
    {
        var content = """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/App/App.csproj" />
                <Project Path="src/Lib/Lib.vbproj" />
              </Folder>
            </Solution>
            """;

        var projects = SolutionReader.ReadProjects(
            Path.Combine(Path.GetTempPath(), "MySolution.slnx"),
            content
        );

        projects.Count.ShouldBe(2);
    }

    [Fact]
    public void ReadProjects_ShouldParseSolutionFilter()
    {
        var content = """
            {
              "solution": {
                "path": "MySolution.sln",
                "projects": [ "src/App/App.csproj" ]
              }
            }
            """;

        var projects = SolutionReader.ReadProjects(
            Path.Combine(Path.GetTempPath(), "MySolution.slnf"),
            content
        );

        projects.Count.ShouldBe(1);
        projects[0].ShouldEndWith("App.csproj");
    }

    [Fact]
    public void ReadProjects_ShouldReturnEmptyForMalformedXml() =>
        SolutionReader
            .ReadProjects(Path.Combine(Path.GetTempPath(), "Broken.slnx"), "<Solution>")
            .ShouldBeEmpty();

    [Fact]
    public void ReadProjects_ShouldReturnEmptyForMalformedJson() =>
        SolutionReader
            .ReadProjects(Path.Combine(Path.GetTempPath(), "Broken.slnf"), "{ not json")
            .ShouldBeEmpty();
}
