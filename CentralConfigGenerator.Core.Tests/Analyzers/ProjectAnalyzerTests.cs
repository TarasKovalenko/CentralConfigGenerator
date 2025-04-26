using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Tests.Analyzers;

public class ProjectAnalyzerTests
{
    private readonly IProjectAnalyzer _analyzer = new ProjectAnalyzer();

    [Fact]
    public void ExtractCommonProperties_ShouldReturnEmptyDictionary_WhenNoProjects()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>();

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void ExtractCommonProperties_ShouldReturnCommonProperties_WhenPropertiesExistInAllProjects()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                    </PropertyGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContainKey("TargetFramework");
        result["TargetFramework"].ShouldBe("net9.0");
        result.ShouldContainKey("ImplicitUsings");
        result["ImplicitUsings"].ShouldBe("enable");
        result.ShouldContainKey("Nullable");
        result["Nullable"].ShouldBe("enable");
    }

    [Fact]
    public void ExtractCommonProperties_ShouldIgnoreNonCommonProperties_WhenPropertiesDifferAcrossProjects()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <Authors>Author1</Authors>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <Authors>Author2</Authors>
                    </PropertyGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContainKey("TargetFramework");
        result.ShouldContainKey("ImplicitUsings");
        result.ShouldContainKey("Nullable");
        result.ShouldNotContainKey("Authors");
    }

    [Fact]
    public void ExtractCommonProperties_ShouldHandleInvalidXml_AndContinueProcessingOtherFiles()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "InvalidProject.csproj",
                Content = @"This is not valid XML"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                    </PropertyGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContainKey("TargetFramework");
        result.ShouldContainKey("ImplicitUsings");
        result.ShouldContainKey("Nullable");
    }

    [Fact]
    public void ExtractCommonProperties_ShouldIgnoreEmptyProperties()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <EmptyProperty></EmptyProperty>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <EmptyProperty></EmptyProperty>
                    </PropertyGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContainKey("TargetFramework");
        result.ShouldContainKey("ImplicitUsings");
        result.ShouldContainKey("Nullable");
        result.ShouldNotContainKey("EmptyProperty");
    }

    [Fact]
    public void ExtractCommonProperties_ShouldHandleMultiplePropertyGroups()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                    </PropertyGroup>
                    <PropertyGroup>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                    </PropertyGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContainKey("TargetFramework");
        result.ShouldContainKey("ImplicitUsings");
        result.ShouldContainKey("Nullable");
    }

    [Fact]
    public void ExtractCommonProperties_ShouldRequireAllProjects_ForTwoProjectScenario()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                    </PropertyGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContainKey("TargetFramework");
        result["TargetFramework"].ShouldBe("net9.0");
        result.ShouldNotContainKey("ImplicitUsings");
        result.ShouldNotContainKey("Nullable");
    }

    [Fact]
    public void ExtractCommonProperties_ShouldUseCorrectThreshold_WithOddNumberOfProjects()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "Project3.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                    </PropertyGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContainKey("TargetFramework");
        result.ShouldContainKey("ImplicitUsings");
        result.ShouldNotContainKey("Nullable");
    }

    [Fact]
    public void ExtractCommonProperties_ShouldConsiderDifferentValues_WhenCommonValuesExist()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <LangVersion>10.0</LangVersion>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <LangVersion>10.0</LangVersion>
                    </PropertyGroup>
                </Project>"
            },
            new()
            {
                Path = "Project3.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <LangVersion>10.0</LangVersion>
                    </PropertyGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContainKey("TargetFramework");
        result.ShouldContainKey("LangVersion");
        result["LangVersion"].ShouldBe("10.0");
    }

    [Fact]
    public void ExtractCommonProperties_ShouldHandleSingleProject()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                    </PropertyGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContainKey("TargetFramework");
        result.ShouldContainKey("ImplicitUsings");
        result.ShouldContainKey("Nullable");
    }

    [Fact]
    public void ExtractCommonProperties_ShouldHandleLargeNumberOfProjects()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>();

        // Create 5 projects with same TargetFramework, but only 3 with same ImplicitUsings
        for (int i = 0; i < 5; i++)
        {
            projectFiles.Add(new ProjectFile
            {
                Path = $"Project{i + 1}.csproj",
                Content = $@"<Project Sdk=""Microsoft.NET.Sdk"">
                    <PropertyGroup>
                        <TargetFramework>net9.0</TargetFramework>
                        {(i < 3 ? "<ImplicitUsings>enable</ImplicitUsings>" : "")}
                    </PropertyGroup>
                </Project>"
            });
        }

        // Act
        var result = _analyzer.ExtractCommonProperties(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContainKey("TargetFramework");
        result.ShouldContainKey("ImplicitUsings");
    }
}