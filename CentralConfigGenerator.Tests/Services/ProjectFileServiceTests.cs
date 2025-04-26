using CentralConfigGenerator.Services;
using CentralConfigGenerator.Services.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CentralConfigGenerator.Tests.Services;

public class ProjectFileServiceTests
{
    private readonly IFileService _fileServiceMock;
    private readonly IProjectFileService _projectFileService;

    public ProjectFileServiceTests()
    {
        _fileServiceMock = Substitute.For<IFileService>();
        _projectFileService = new ProjectFileService(_fileServiceMock);
    }

    [Fact]
    public async Task ScanDirectoryForProjectsAsync_ShouldReturnEmptyCollection_WhenNoCsprojFilesExist()
    {
        // Arrange
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        try
        {
            // Act
            var result = await _projectFileService.ScanDirectoryForProjectsAsync(directory);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(0);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task ScanDirectoryForProjectsAsync_ShouldReadAllCsprojFiles()
    {
        // Arrange - Create a temp directory with csproj files
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var projectPath1 = Path.Combine(tempDir, "Test1.csproj");
        var projectPath2 = Path.Combine(tempDir, "Test2.csproj");

        await File.WriteAllTextAsync(projectPath1, "<Project></Project>");
        await File.WriteAllTextAsync(projectPath2, "<Project></Project>");

        // Setup mock
        _fileServiceMock.ReadAllTextAsync(projectPath1)
            .Returns(Task.FromResult(
                "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>"));
        _fileServiceMock.ReadAllTextAsync(projectPath2)
            .Returns(Task.FromResult(
                "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>"));

        try
        {
            // Act
            var result = await _projectFileService.ScanDirectoryForProjectsAsync(new DirectoryInfo(tempDir));

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);

            // Verify that the file service was called for each project file
            await _fileServiceMock.Received(1).ReadAllTextAsync(projectPath1);
            await _fileServiceMock.Received(1).ReadAllTextAsync(projectPath2);

            result.Any(p => p.Path == projectPath1).ShouldBeTrue();
            result.Any(p => p.Path == projectPath2).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScanDirectoryForProjectsAsync_ShouldHandleReadErrors()
    {
        // Arrange - Create a temp directory with csproj files
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var projectPath1 = Path.Combine(tempDir, "Test1.csproj");
        var projectPath2 = Path.Combine(tempDir, "Test2.csproj");

        await File.WriteAllTextAsync(projectPath1, "<Project></Project>");
        await File.WriteAllTextAsync(projectPath2, "<Project></Project>");

        // Setup mock - first file succeeds, second throws an exception
        _fileServiceMock.ReadAllTextAsync(projectPath1)
            .Returns(Task.FromResult(
                "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>"));
        _fileServiceMock.ReadAllTextAsync(projectPath2)
            .Throws(new IOException("Test exception"));

        try
        {
            // Act
            var result = await _projectFileService.ScanDirectoryForProjectsAsync(new DirectoryInfo(tempDir));

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1); // Only one file should be successfully read

            await _fileServiceMock.Received(1).ReadAllTextAsync(projectPath1);
            await _fileServiceMock.Received(1).ReadAllTextAsync(projectPath2);

            result.Any(p => p.Path == projectPath1).ShouldBeTrue();
            result.Any(p => p.Path == projectPath2).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScanDirectoryForProjectsAsync_ShouldHandleSubdirectories()
    {
        // Arrange - Create a temp directory with subdirectories containing csproj files
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var subDir = Path.Combine(tempDir, "SubDir");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(subDir);

        var projectPath1 = Path.Combine(tempDir, "Test1.csproj");
        var projectPath2 = Path.Combine(subDir, "Test2.csproj");

        await File.WriteAllTextAsync(projectPath1, "<Project></Project>");
        await File.WriteAllTextAsync(projectPath2, "<Project></Project>");

        // Setup mock
        _fileServiceMock.ReadAllTextAsync(projectPath1)
            .Returns(Task.FromResult(
                "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>"));
        _fileServiceMock.ReadAllTextAsync(projectPath2)
            .Returns(Task.FromResult(
                "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>"));

        try
        {
            // Act
            var result = await _projectFileService.ScanDirectoryForProjectsAsync(new DirectoryInfo(tempDir));

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);

            await _fileServiceMock.Received(1).ReadAllTextAsync(projectPath1);
            await _fileServiceMock.Received(1).ReadAllTextAsync(projectPath2);

            result.Any(p => p.Path == projectPath1).ShouldBeTrue();
            result.Any(p => p.Path == projectPath2).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}