using CentralConfigGenerator.Cli.Commands;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Configuration;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Tests.Cli;

public class EffectiveSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ccg-effective-" + Guid.NewGuid().ToString("N")
    );

    public EffectiveSettingsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task LoadAsync_ShouldPickUpTheConfigFile()
    {
        WriteConfig("""{ "conflictStrategy": "Lowest" }""");

        var effective = await LoadAsync();

        effective.File.ShouldNotBeNull();
        effective.Strategy(null).ShouldBe(VersionResolutionStrategy.Lowest);
    }

    [Fact]
    public async Task LoadAsync_ShouldSkipTheConfigFileWhenDisabled()
    {
        WriteConfig("""{ "conflictStrategy": "Lowest" }""");

        var effective = await LoadAsync(noConfig: true);

        effective.File.ShouldBeNull();
        effective.Strategy(null).ShouldBe(VersionResolutionStrategy.Highest);
    }

    [Fact]
    public async Task Strategy_ShouldPreferTheCommandLine()
    {
        WriteConfig("""{ "conflictStrategy": "Lowest" }""");

        var effective = await LoadAsync();

        effective.Strategy("MostCommon").ShouldBe(VersionResolutionStrategy.MostCommon);
    }

    [Fact]
    public async Task Strategy_ShouldFallBackToHighestForUnknownValues()
    {
        var effective = await LoadAsync();

        effective.Strategy("nonsense").ShouldBe(VersionResolutionStrategy.Highest);
    }

    [Fact]
    public async Task Bool_ShouldOrTheCommandLineWithTheFile()
    {
        WriteConfig("""{ "transitivePinning": true }""");

        var effective = await LoadAsync();

        effective.Bool(false, c => c.TransitivePinning).ShouldBeTrue();
        effective.Bool(true, c => c.IgnorePrerelease).ShouldBeTrue();
    }

    [Fact]
    public async Task Text_ShouldPreferTheCommandLine()
    {
        WriteConfig("""{ "excludeDirs": "from-file" }""");

        var effective = await LoadAsync();

        effective.Text("from-cli", c => c.ExcludeDirs).ShouldBe("from-cli");
        effective.Text(null, c => c.ExcludeDirs).ShouldBe("from-file");
    }

    [Fact]
    public async Task RuleOverrides_ShouldMergeFileAndCommandLine()
    {
        WriteConfig($$"""{ "rules": { "{{RuleIds.OutdatedPackage}}": "Critical" } }""");

        var effective = await LoadAsync();

        var overrides = effective.RuleOverrides([$"{RuleIds.FloatingVersion}=High"]);

        overrides[RuleIds.OutdatedPackage].ShouldBe(Severity.Critical);
        overrides[RuleIds.FloatingVersion].ShouldBe(Severity.High);
    }

    [Fact]
    public async Task RuleOverrides_ShouldTreatNoneAsDisabled()
    {
        var effective = await LoadAsync();

        var overrides = effective.RuleOverrides([$"{RuleIds.OutdatedPackage}=none"]);

        overrides.ShouldContainKey(RuleIds.OutdatedPackage);
        overrides[RuleIds.OutdatedPackage].ShouldBeNull();
    }

    [Fact]
    public async Task RuleOverrides_ShouldIgnoreUnknownRules()
    {
        var effective = await LoadAsync();

        effective.RuleOverrides(["NotARule=High"]).ShouldBeEmpty();
    }

    [Fact]
    public async Task RuleOverrides_ShouldIgnoreMalformedEntries()
    {
        var effective = await LoadAsync();

        effective.RuleOverrides(["no-equals-sign"]).ShouldBeEmpty();
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_root, ConfigurationLoader.FileName), json);

    private async Task<EffectiveSettings> LoadAsync(bool noConfig = false)
    {
        var effective = new EffectiveSettings(new ConfigurationLoader(new PhysicalFileSystem()));
        await effective.LoadAsync(new MigrateSettings { Directory = _root, NoConfig = noConfig });
        return effective;
    }
}
