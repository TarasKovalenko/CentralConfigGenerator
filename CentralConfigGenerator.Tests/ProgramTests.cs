using CentralConfigGenerator.Commands;

namespace CentralConfigGenerator.Tests;

public class ProgramTests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterAllRequiredServices()
    {
        // Act
        var serviceProvider = Program.ConfigureServices();

        // Assert
        serviceProvider.ShouldNotBeNull();
        
        // Verify that all required services can be resolved
        var buildPropsCommand = serviceProvider.GetService(typeof(BuildPropsCommand));
        buildPropsCommand.ShouldNotBeNull();
        
        var packagesPropsCommand = serviceProvider.GetService(typeof(PackagesPropsCommand));
        packagesPropsCommand.ShouldNotBeNull();
    }
}