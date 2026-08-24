using CentralConfigGenerator.Cli.Commands;
using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Discovery;
using CentralConfigGenerator.Core.Migration;
using CentralConfigGenerator.Core.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace CentralConfigGenerator.Tests.Cli;

public class ServiceRegistrationTests
{
    [Theory]
    [InlineData(typeof(MigrateCommand))]
    [InlineData(typeof(BuildPropsCommand))]
    [InlineData(typeof(AllCommand))]
    [InlineData(typeof(RevertCommand))]
    [InlineData(typeof(AnalyzeCommand))]
    [InlineData(typeof(UpdateCommand))]
    [InlineData(typeof(VerifyCommand))]
    [InlineData(typeof(TreeCommand))]
    [InlineData(typeof(StatusCommand))]
    [InlineData(typeof(DoctorCommand))]
    [InlineData(typeof(InitCommand))]
    [InlineData(typeof(ExplainCommand))]
    [InlineData(typeof(BatchCommand))]
    [InlineData(typeof(BackupListCommand))]
    [InlineData(typeof(BackupRestoreCommand))]
    [InlineData(typeof(BackupPruneCommand))]
    [InlineData(typeof(CompletionsCommand))]
    public void EveryCommandShouldResolve(Type commandType)
    {
        using var provider = Program.ConfigureServices();

        provider.GetService(commandType).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(typeof(IMigrationService))]
    [InlineData(typeof(IAnalysisEngine))]
    [InlineData(typeof(IAutoFixService))]
    [InlineData(typeof(IProjectDiscoveryService))]
    [InlineData(typeof(IVerificationService))]
    [InlineData(typeof(IPackageUpdateService))]
    [InlineData(typeof(DoctorService))]
    [InlineData(typeof(StatusService))]
    [InlineData(typeof(BatchService))]
    public void EveryServiceShouldResolve(Type serviceType)
    {
        using var provider = Program.ConfigureServices();

        provider.GetService(serviceType).ShouldNotBeNull();
    }

    [Fact]
    public void LegacyCommandsShouldStillResolve()
    {
        using var provider = Program.ConfigureServices();

        provider.GetService<CentralConfigGenerator.Commands.BuildPropsCommand>().ShouldNotBeNull();
        provider.GetService<CentralConfigGenerator.Commands.PackagesPropsCommand>().ShouldNotBeNull();
        provider.GetService<CentralConfigGenerator.Commands.EnhancedPackagesPropsCommand>().ShouldNotBeNull();
    }
}
