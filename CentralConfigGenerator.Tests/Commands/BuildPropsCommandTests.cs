using CentralConfigGenerator.Commands;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Services.Abstractions;

namespace CentralConfigGenerator.Tests.Commands;

public class BuildPropsCommandTests
{
    private readonly IProjectAnalyzer _projectAnalyzer;
    private readonly IProjectFileService _projectFileService;
    private readonly IBuildPropsGenerator _buildPropsGenerator;
    private readonly IFileService _fileService;
    private readonly BuildPropsCommand _command;

    public BuildPropsCommandTests()
    {
        _projectAnalyzer = Substitute.For<IProjectAnalyzer>();
        _projectFileService = Substitute.For<IProjectFileService>();
        _buildPropsGenerator = Substitute.For<IBuildPropsGenerator>();
        _fileService = Substitute.For<IFileService>();

        _command = new BuildPropsCommand(
            _projectAnalyzer,
            _projectFileService,
            _buildPropsGenerator,
            _fileService
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCreateFile_WhenFileExistsAndOverwriteIsFalse()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Build.props");
        
        _fileService.Exists(targetPath).Returns(true);

        // Act
        await _command.ExecuteAsync(directory, false);

        // Assert
        _fileService.Received(1).Exists(targetPath);
        await _projectFileService.DidNotReceive().ScanDirectoryForProjectsAsync(Arg.Any<DirectoryInfo>());
        await _fileService.DidNotReceive().WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCreateFile_WhenNoProjectsFound()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Build.props");
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory)
            .Returns(new List<ProjectFile>());

        // Act
        await _command.ExecuteAsync(directory, false);

        // Assert
        _fileService.Received(1).Exists(targetPath);
        await _projectFileService.Received(1).ScanDirectoryForProjectsAsync(directory);
        _projectAnalyzer.DidNotReceive().ExtractCommonProperties(Arg.Any<IReadOnlyCollection<ProjectFile>>());
        await _fileService.DidNotReceive().WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateFile_WhenProjectsFoundAndFileDoesNotExist()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Build.props");
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "Project1.csproj", Content = "<Project></Project>" }
        };
        
        var commonProperties = new Dictionary<string, string>
        {
            { "TargetFramework", "net9.0" },
            { "ImplicitUsings", "enable" },
            { "Nullable", "enable" }
        };
        
        var buildPropsContent = "<Project><PropertyGroup></PropertyGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _projectAnalyzer.ExtractCommonProperties(projectFiles).Returns(commonProperties);
        _buildPropsGenerator.GenerateBuildPropsContent(commonProperties).Returns(buildPropsContent);

        // Act
        await _command.ExecuteAsync(directory, false);

        // Assert
        _fileService.Received(1).Exists(targetPath);
        await _projectFileService.Received(1).ScanDirectoryForProjectsAsync(directory);
        _projectAnalyzer.Received(1).ExtractCommonProperties(projectFiles);
        _buildPropsGenerator.Received(1).GenerateBuildPropsContent(commonProperties);
        await _fileService.Received(1).WriteAllTextAsync(targetPath, buildPropsContent);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateFile_WhenProjectsFoundAndOverwriteIsTrue()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Build.props");
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "Project1.csproj", Content = "<Project></Project>" }
        };
        
        var commonProperties = new Dictionary<string, string>
        {
            { "TargetFramework", "net9.0" },
            { "ImplicitUsings", "enable" },
            { "Nullable", "enable" }
        };
        
        var buildPropsContent = "<Project><PropertyGroup></PropertyGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(true);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _projectAnalyzer.ExtractCommonProperties(projectFiles).Returns(commonProperties);
        _buildPropsGenerator.GenerateBuildPropsContent(commonProperties).Returns(buildPropsContent);

        // Act
        await _command.ExecuteAsync(directory, true);

        // Assert
        _fileService.Received(1).Exists(targetPath);
        await _projectFileService.Received(1).ScanDirectoryForProjectsAsync(directory);
        _projectAnalyzer.Received(1).ExtractCommonProperties(projectFiles);
        _buildPropsGenerator.Received(1).GenerateBuildPropsContent(commonProperties);
        await _fileService.Received(1).WriteAllTextAsync(targetPath, buildPropsContent);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRemoveSpecifiedProperties_FromProjectFiles()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Build.props");
        
        var validXml = @"<Project>
            <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <OtherProperty>value</OtherProperty>
            </PropertyGroup>
        </Project>";
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "Project1.csproj", Content = validXml }
        };
        
        var commonProperties = new Dictionary<string, string>
        {
            { "TargetFramework", "net9.0" },
            { "ImplicitUsings", "enable" },
            { "Nullable", "enable" }
        };
        
        var buildPropsContent = "<Project><PropertyGroup></PropertyGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _projectAnalyzer.ExtractCommonProperties(projectFiles).Returns(commonProperties);
        _buildPropsGenerator.GenerateBuildPropsContent(commonProperties).Returns(buildPropsContent);

        // Act
        await _command.ExecuteAsync(directory, false);

        // Assert
        await _fileService.Received(1).WriteAllTextAsync(targetPath, buildPropsContent);
        await _fileService.Received(1).WriteAllTextAsync("Project1.csproj", Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleInvalidXml_WhenUpdatingProjectFiles()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Build.props");
        
        var invalidXml = "This is not valid XML";
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "InvalidProject.csproj", Content = invalidXml }
        };
        
        var commonProperties = new Dictionary<string, string>
        {
            { "TargetFramework", "net9.0" },
            { "ImplicitUsings", "enable" },
            { "Nullable", "enable" }
        };
        
        var buildPropsContent = "<Project><PropertyGroup></PropertyGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _projectAnalyzer.ExtractCommonProperties(projectFiles).Returns(commonProperties);
        _buildPropsGenerator.GenerateBuildPropsContent(commonProperties).Returns(buildPropsContent);

        // Act & Assert
        // Should not throw an exception
        await _command.ExecuteAsync(directory, false);
        
        await _fileService.Received(1).WriteAllTextAsync(targetPath, buildPropsContent);
        await _fileService.DidNotReceive().WriteAllTextAsync("InvalidProject.csproj", Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOnlyRemoveSpecifiedProperties()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Build.props");
        
        var projectXml = @"<Project>
            <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <SomeOtherProperty>value</SomeOtherProperty>
            </PropertyGroup>
        </Project>";
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "Project1.csproj", Content = projectXml }
        };
        
        var commonProperties = new Dictionary<string, string>
        {
            { "TargetFramework", "net9.0" },
            { "ImplicitUsings", "enable" },
            { "Nullable", "enable" }
        };
        
        var buildPropsContent = "<Project><PropertyGroup></PropertyGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _projectAnalyzer.ExtractCommonProperties(projectFiles).Returns(commonProperties);
        _buildPropsGenerator.GenerateBuildPropsContent(commonProperties).Returns(buildPropsContent);

        string updatedContent = null!;
        _fileService.WriteAllTextAsync("Project1.csproj", Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(callInfo => updatedContent = callInfo.ArgAt<string>(1));

        // Act
        await _command.ExecuteAsync(directory, false);

        // Assert
        updatedContent.ShouldNotBeNull();
        updatedContent.ShouldNotContain("<TargetFramework>");
        updatedContent.ShouldNotContain("<ImplicitUsings>");
        updatedContent.ShouldNotContain("<Nullable>");
        updatedContent.ShouldContain("<SomeOtherProperty>value</SomeOtherProperty>");
    }
}