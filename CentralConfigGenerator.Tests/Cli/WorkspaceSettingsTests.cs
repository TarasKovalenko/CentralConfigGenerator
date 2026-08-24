using CentralConfigGenerator.Cli.Commands;

namespace CentralConfigGenerator.Tests.Cli;

public class WorkspaceSettingsTests
{
    private static readonly string Temp = Path.GetFullPath(Path.GetTempPath())
        .TrimEnd(Path.DirectorySeparatorChar);

    [Fact]
    public void ResolvedDirectory_ShouldDefaultToTheCurrentDirectory() =>
        new MigrateSettings().ResolvedDirectory.ShouldBe(
            Directory.GetCurrentDirectory().TrimEnd(Path.DirectorySeparatorChar)
        );

    [Fact]
    public void ResolvedDirectory_ShouldFollowTheSolutionFolder()
    {
        var solution = Path.Combine(Path.GetTempPath(), "Some.sln");

        new MigrateSettings { Solution = solution }.ResolvedDirectory.ShouldBe(Temp);
    }

    [Fact]
    public void ResolvedDirectory_ShouldPreferAnExplicitDirectory()
    {
        var settings = new MigrateSettings
        {
            Directory = Path.GetTempPath(),
            Solution = Path.Combine("elsewhere", "Some.sln"),
        };

        settings.ResolvedDirectory.ShouldBe(Temp);
    }

    [Fact]
    public void Validate_ShouldRejectAMissingDirectory()
    {
        var settings = new MigrateSettings { Directory = Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid()) };

        settings.Validate().Successful.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ShouldAcceptAnExistingDirectory() =>
        new MigrateSettings { Directory = Path.GetTempPath() }.Validate().Successful.ShouldBeTrue();

    [Fact]
    public void Validate_ShouldRejectAMissingSolution()
    {
        var settings = new MigrateSettings
        {
            Solution = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".sln"),
        };

        settings.Validate().Successful.ShouldBeFalse();
    }
}
