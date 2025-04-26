using CentralConfigGenerator.Extensions;

namespace CentralConfigGenerator.Tests.Extensions;

public class MsgLoggerTests
{
    [Fact]
    public void LogInformation_ShouldNotThrowException()
    {
        // Act & Assert - Should not throw exception
        Should.NotThrow(() => MsgLogger.LogInformation("Test information message"));
        Should.NotThrow(() => MsgLogger.LogInformation("Test information message with parameter: {0}", "param"));
    }

    [Fact]
    public void LogWarning_ShouldNotThrowException()
    {
        // Act & Assert - Should not throw exception
        Should.NotThrow(() => MsgLogger.LogWarning("Test warning message"));
        Should.NotThrow(() => MsgLogger.LogWarning("Test warning message with parameter: {0}", "param"));
    }

    [Fact]
    public void LogError_ShouldNotThrowException()
    {
        // Act & Assert - Should not throw exception
        Should.NotThrow(() => MsgLogger.LogError("Test error message"));
        Should.NotThrow(() => MsgLogger.LogError("Test error message with parameter: {0}", "param"));
    }

    [Fact]
    public void LogErrorWithException_ShouldNotThrowException()
    {
        try
        {
            // Arrange
            throw new Exception("Test error message");
        }
        catch (Exception e)
        {
            // Act & Assert - Should not throw exception
            Should.NotThrow(() => MsgLogger.LogError(e, "Test error message with exception"));
            Should.NotThrow(() =>
                MsgLogger.LogError(e, "Test error message with exception and parameter: {0}", "param"));
        }
    }
}