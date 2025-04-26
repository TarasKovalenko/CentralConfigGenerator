using CentralConfigGenerator.Services;
using CentralConfigGenerator.Services.Abstractions;

namespace CentralConfigGenerator.Tests.Services;

public class FileServiceTests
{
    private readonly string _testFilePath = Path.GetTempFileName();
    private readonly IFileService _fileService = new FileService();

    [Fact]
    public void Exists_ShouldReturnTrue_WhenFileExists()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test content");

        // Act
        var result = _fileService.Exists(_testFilePath);

        // Assert
        result.ShouldBeTrue();

        // Cleanup
        File.Delete(_testFilePath);
    }

    [Fact]
    public void Exists_ShouldReturnFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var result = _fileService.Exists(nonExistentPath);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAllTextAsync_ShouldReturnFileContent()
    {
        // Arrange
        var content = "test content";
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await _fileService.ReadAllTextAsync(_testFilePath);

        // Assert
        result.ShouldBe(content);

        // Cleanup
        File.Delete(_testFilePath);
    }

    [Fact]
    public async Task WriteAllTextAsync_ShouldWriteContentToFile()
    {
        // Arrange
        var content = "test content";

        // Act
        await _fileService.WriteAllTextAsync(_testFilePath, content);

        // Assert
        File.Exists(_testFilePath).ShouldBeTrue();
        (await File.ReadAllTextAsync(_testFilePath)).ShouldBe(content);

        // Cleanup
        File.Delete(_testFilePath);
    }
}