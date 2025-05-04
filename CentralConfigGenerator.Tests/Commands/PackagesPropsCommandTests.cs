using CentralConfigGenerator.Commands;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Analyzers.Abstractions;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.Generators.Abstractions;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Services.Abstractions;

namespace CentralConfigGenerator.Tests.Commands;

public class PackagesPropsCommandTests
{
    private readonly IPackageAnalyzer _packageAnalyzer;
    private readonly IProjectFileService _projectFileService;
    private readonly IPackagesPropsGenerator _packagesPropsGenerator;
    private readonly IFileService _fileService;
    private readonly PackagesPropsCommand _command;

    public PackagesPropsCommandTests()
    {
        _packageAnalyzer = Substitute.For<IPackageAnalyzer>();
        _projectFileService = Substitute.For<IProjectFileService>();
        _packagesPropsGenerator = Substitute.For<IPackagesPropsGenerator>();
        _fileService = Substitute.For<IFileService>();

        _command = new PackagesPropsCommand(
            _packageAnalyzer,
            _projectFileService,
            _packagesPropsGenerator,
            _fileService
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCreateFile_WhenFileExistsAndOverwriteIsFalse()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");
        
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
        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory)
            .Returns(new List<ProjectFile>());

        // Act
        await _command.ExecuteAsync(directory, false);

        // Assert
        _fileService.Received(1).Exists(targetPath);
        await _projectFileService.Received(1).ScanDirectoryForProjectsAsync(directory);
        _packageAnalyzer.DidNotReceive().ExtractPackageVersions(Arg.Any<IReadOnlyCollection<ProjectFile>>());
        await _fileService.DidNotReceive().WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateFile_WhenProjectsFoundAndFileDoesNotExist()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "Project1.csproj", Content = "<Project></Project>" }
        };
        
        var packageVersions = new Dictionary<string, string>
        {
            { "Package1", "1.0.0" },
            { "Package2", "2.0.0" }
        };
        
        var packagesPropsContent = "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup></ItemGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _packageAnalyzer.ExtractPackageVersions(projectFiles).Returns(packageVersions);
        _packagesPropsGenerator.GeneratePackagesPropsContent(packageVersions).Returns(packagesPropsContent);

        // Act
        await _command.ExecuteAsync(directory, false);

        // Assert
        _fileService.Received(1).Exists(targetPath);
        await _projectFileService.Received(1).ScanDirectoryForProjectsAsync(directory);
        _packageAnalyzer.Received(1).ExtractPackageVersions(projectFiles);
        _packagesPropsGenerator.Received(1).GeneratePackagesPropsContent(packageVersions);
        await _fileService.Received(1).WriteAllTextAsync(targetPath, packagesPropsContent);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateFile_WhenProjectsFoundAndOverwriteIsTrue()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "Project1.csproj", Content = "<Project></Project>" }
        };
        
        var packageVersions = new Dictionary<string, string>
        {
            { "Package1", "1.0.0" },
            { "Package2", "2.0.0" }
        };
        
        var packagesPropsContent = "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup></ItemGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(true);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _packageAnalyzer.ExtractPackageVersions(projectFiles).Returns(packageVersions);
        _packagesPropsGenerator.GeneratePackagesPropsContent(packageVersions).Returns(packagesPropsContent);

        // Act
        await _command.ExecuteAsync(directory, true);

        // Assert
        _fileService.Received(1).Exists(targetPath);
        await _projectFileService.Received(1).ScanDirectoryForProjectsAsync(directory);
        _packageAnalyzer.Received(1).ExtractPackageVersions(projectFiles);
        _packagesPropsGenerator.Received(1).GeneratePackagesPropsContent(packageVersions);
        await _fileService.Received(1).WriteAllTextAsync(targetPath, packagesPropsContent);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRemoveVersionAttributes_FromPackageReferences()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");
        
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
            <ItemGroup>
                <PackageReference Include=""Package1"" Version=""1.0.0"" />
                <PackageReference Include=""Package2"" Version=""2.0.0"" />
            </ItemGroup>
        </Project>";
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "Project1.csproj", Content = projectContent }
        };
        
        var packageVersions = new Dictionary<string, string>
        {
            { "Package1", "1.0.0" },
            { "Package2", "2.0.0" }
        };
        
        var packagesPropsContent = "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup></ItemGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _packageAnalyzer.ExtractPackageVersions(projectFiles).Returns(packageVersions);
        _packagesPropsGenerator.GeneratePackagesPropsContent(packageVersions).Returns(packagesPropsContent);

        string updatedContent = null!;
        _fileService.WriteAllTextAsync("Project1.csproj", Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(callInfo => updatedContent = callInfo.ArgAt<string>(1));

        // Act
        await _command.ExecuteAsync(directory, false);

        // Assert
        await _fileService.Received(1).WriteAllTextAsync(targetPath, packagesPropsContent);
        await _fileService.Received(1).WriteAllTextAsync("Project1.csproj", Arg.Any<string>());
        
        updatedContent.ShouldNotBeNull();
        updatedContent.ShouldNotContain("Version=");
        updatedContent.ShouldContain("<PackageReference Include=\"Package1\" />");
        updatedContent.ShouldContain("<PackageReference Include=\"Package2\" />");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleInvalidXml_WhenUpdatingProjectFiles()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");
        
        var invalidXml = "This is not valid XML";
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "InvalidProject.csproj", Content = invalidXml }
        };
        
        var packageVersions = new Dictionary<string, string>
        {
            { "Package1", "1.0.0" },
            { "Package2", "2.0.0" }
        };
        
        var packagesPropsContent = "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup></ItemGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _packageAnalyzer.ExtractPackageVersions(projectFiles).Returns(packageVersions);
        _packagesPropsGenerator.GeneratePackagesPropsContent(packageVersions).Returns(packagesPropsContent);

        // Act & Assert
        // Should not throw an exception
        await _command.ExecuteAsync(directory, false);
        
        await _fileService.Received(1).WriteAllTextAsync(targetPath, packagesPropsContent);
        await _fileService.DidNotReceive().WriteAllTextAsync("InvalidProject.csproj", Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOnlyUpdateProjectFiles_WithPackageReferences()
    {
        // Arrange
        var directory = new DirectoryInfo("C:\\TestDir");
        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");
        
        var projectWithPackages = @"<Project Sdk=""Microsoft.NET.Sdk"">
            <ItemGroup>
                <PackageReference Include=""Package1"" Version=""1.0.0"" />
            </ItemGroup>
        </Project>";
        
        var projectWithoutPackages = @"<Project Sdk=""Microsoft.NET.Sdk"">
            <ItemGroup>
                <ProjectReference Include=""SomeProject.csproj"" />
            </ItemGroup>
        </Project>";
        
        var projectFiles = new List<ProjectFile>
        {
            new() { Path = "ProjectWithPackages.csproj", Content = projectWithPackages },
            new() { Path = "ProjectWithoutPackages.csproj", Content = projectWithoutPackages }
        };
        
        var packageVersions = new Dictionary<string, string>
        {
            { "Package1", "1.0.0" }
        };
        
        var packagesPropsContent = "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup></ItemGroup></Project>";
        
        _fileService.Exists(targetPath).Returns(false);
        _projectFileService.ScanDirectoryForProjectsAsync(directory).Returns(projectFiles);
        _packageAnalyzer.ExtractPackageVersions(projectFiles).Returns(packageVersions);
        _packagesPropsGenerator.GeneratePackagesPropsContent(packageVersions).Returns(packagesPropsContent);

        // Act
        await _command.ExecuteAsync(directory, false);

        // Assert
        await _fileService.Received(1).WriteAllTextAsync("ProjectWithPackages.csproj", Arg.Any<string>());
        await _fileService.DidNotReceive().WriteAllTextAsync("ProjectWithoutPackages.csproj", Arg.Any<string>());
    }
}