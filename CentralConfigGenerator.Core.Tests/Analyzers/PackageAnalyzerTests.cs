using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Analyzers.Abstractions;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Tests.Analyzers;

public class PackageAnalyzerTests
{
    private readonly IPackageAnalyzer _analyzer = new PackageAnalyzer();

    [Fact]
    public void ExtractPackageVersions_ShouldReturnEmptyDictionary_WhenNoProjects()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>();

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void ExtractPackageVersions_ShouldReturnPackageVersions_WhenPackagesExist()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0"" />
                        <PackageReference Include=""Package2"" Version=""2.0.0"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContainKey("Package1");
        result["Package1"].ShouldBe("1.0.0");
        result.ShouldContainKey("Package2");
        result["Package2"].ShouldBe("2.0.0");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldUseHighestVersion_WhenSamePackageExistsInMultipleProjects()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0"" />
                    </ItemGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""2.0.0"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContainKey("Package1");
        result["Package1"].ShouldBe("2.0.0");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldIgnorePackagesWithoutVersion()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0"" />
                        <PackageReference Include=""PackageWithoutVersion"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContainKey("Package1");
        result.ShouldNotContainKey("PackageWithoutVersion");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldHandleInvalidXml_AndContinueProcessingOtherFiles()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "InvalidProject.csproj",
                Content = "This is not valid XML"
            },
            new()
            {
                Path = "ValidProject.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContainKey("Package1");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldHandleNonParsableVersions()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0"" />
                        <PackageReference Include=""Package2"" Version=""$(VariableVersion)"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContainKey("Package1");
        result.ShouldContainKey("Package2");
        result["Package2"].ShouldBe("$(VariableVersion)");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldMergeVersions_FromDifferentProjects()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0"" />
                    </ItemGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package2"" Version=""2.0.0"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContainKey("Package1");
        result.ShouldContainKey("Package2");
        result["Package1"].ShouldBe("1.0.0");
        result["Package2"].ShouldBe("2.0.0");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldHandleComplexVersionStrings()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0-preview.1"" />
                        <PackageReference Include=""Package2"" Version=""2.0.0-beta+metadata"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContainKey("Package1");
        result["Package1"].ShouldBe("1.0.0-preview.1");
        result.ShouldContainKey("Package2");
        result["Package2"].ShouldBe("2.0.0-beta+metadata");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldHandleSemanticVersioning_PrereleaseComparison()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0-alpha.1"" />
                    </ItemGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0-beta.1"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContainKey("Package1");
        // beta > alpha in SemVer
        result["Package1"].ShouldBe("1.0.0-beta.1");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldPreferStableOverPrerelease()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0-preview"" />
                    </ItemGroup>
                </Project>"
            },
            new()
            {
                Path = "Project2.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContainKey("Package1");
        // Stable version > pre-release
        result["Package1"].ShouldBe("1.0.0");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldHandleVersionRanges()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""[1.0.0,2.0.0)"" />
                        <PackageReference Include=""Package2"" Version=""[2.0.0,)"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContainKey("Package1");
        // Original range preserved
        result["Package1"].ShouldBe("[1.0.0,2.0.0)"); 
        result.ShouldContainKey("Package2");
        result["Package2"].ShouldBe("[2.0.0,)");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldHandleFloatingVersions()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.*"" />
                        <PackageReference Include=""Package2"" Version=""1.*"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContainKey("Package1");
        result["Package1"].ShouldBe("1.0.*");
        result.ShouldContainKey("Package2");
        result["Package2"].ShouldBe("1.*");
    }

    [Fact]
    public void ExtractPackageVersions_ShouldHandleCommitHashVersions()
    {
        // Arrange
        var projectFiles = new List<ProjectFile>
        {
            new()
            {
                Path = "Project1.csproj",
                Content = @"<Project Sdk=""Microsoft.NET.Sdk"">
                    <ItemGroup>
                        <PackageReference Include=""Package1"" Version=""1.0.0+abcdef123456"" />
                    </ItemGroup>
                </Project>"
            }
        };

        // Act
        var result = _analyzer.ExtractPackageVersions(projectFiles);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContainKey("Package1");
        result["Package1"].ShouldBe("1.0.0+abcdef123456");
    }
}
