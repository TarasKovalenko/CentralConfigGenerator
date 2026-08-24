using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Tests.Migration;

public class PropertyHoisterTests
{
    [Fact]
    public void FindHoistableProperties_ShouldReturnUnanimousValues()
    {
        var projects = new[]
        {
            Project("<PropertyGroup><Nullable>enable</Nullable></PropertyGroup>"),
            Project("<PropertyGroup><Nullable>enable</Nullable></PropertyGroup>"),
        };

        var result = PropertyHoister.FindHoistableProperties(
            projects,
            BuildPropertyDefaults.Recommended
        );

        result["Nullable"].ShouldBe("enable");
    }

    [Fact]
    public void FindHoistableProperties_ShouldSkipPropertiesThatDisagree()
    {
        var projects = new[]
        {
            Project("<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>"),
            Project("<PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>"),
        };

        PropertyHoister
            .FindHoistableProperties(projects, BuildPropertyDefaults.Recommended)
            .ShouldNotContainKey("TargetFramework");
    }

    [Fact]
    public void FindHoistableProperties_ShouldRequireAtLeastTwoProjects()
    {
        var projects = new[]
        {
            Project("<PropertyGroup><Nullable>enable</Nullable></PropertyGroup>"),
        };

        PropertyHoister
            .FindHoistableProperties(projects, BuildPropertyDefaults.Recommended)
            .ShouldBeEmpty();
    }

    [Fact]
    public void FindHoistableProperties_ShouldNeverHoistIdentityProperties()
    {
        var projects = new[]
        {
            Project("<PropertyGroup><AssemblyName>Same</AssemblyName></PropertyGroup>"),
            Project("<PropertyGroup><AssemblyName>Same</AssemblyName></PropertyGroup>"),
        };

        PropertyHoister
            .FindHoistableProperties(projects, ["AssemblyName"])
            .ShouldNotContainKey("AssemblyName");
    }

    [Fact]
    public void FindHoistableProperties_ShouldIgnoreConditionedGroups()
    {
        var projects = new[]
        {
            Project("<PropertyGroup Condition=\" '$(X)' == '1' \"><Nullable>enable</Nullable></PropertyGroup>"),
            Project("<PropertyGroup Condition=\" '$(X)' == '1' \"><Nullable>enable</Nullable></PropertyGroup>"),
        };

        PropertyHoister
            .FindHoistableProperties(projects, BuildPropertyDefaults.Recommended)
            .ShouldBeEmpty();
    }

    private static ProjectFile Project(string body) =>
        new()
        {
            Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csproj"),
            Content = $"<Project Sdk=\"Microsoft.NET.Sdk\">{body}</Project>",
        };
}
