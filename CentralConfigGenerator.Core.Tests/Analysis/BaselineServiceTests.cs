using CentralConfigGenerator.Core.Analysis;
using CentralConfigGenerator.Core.Tests.TestSupport;

namespace CentralConfigGenerator.Core.Tests.Analysis;

public class BaselineServiceTests
{
    private static readonly string Path = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "ccg-baseline.json"
    );

    private static readonly Finding Sample = new()
    {
        RuleId = RuleIds.FloatingVersion,
        Severity = Severity.Moderate,
        Message = "floating",
        PackageId = "xunit",
        Version = "2.9.*",
    };

    [Fact]
    public async Task WriteAsync_ThenLoadAsync_ShouldRoundTrip()
    {
        var fileSystem = new InMemoryFileSystem();
        var service = new BaselineService(fileSystem);

        await service.WriteAsync(Path, [Sample]);
        var baseline = await service.LoadAsync(Path);

        baseline.ShouldNotBeNull();
        baseline.Fingerprints.ShouldContain(Sample.Fingerprint);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnNullWhenTheFileIsMissing() =>
        (await new BaselineService(new InMemoryFileSystem()).LoadAsync(Path)).ShouldBeNull();

    [Fact]
    public async Task LoadAsync_ShouldReturnNullForMalformedJson()
    {
        var fileSystem = new InMemoryFileSystem().AddFile(Path, "{ not json");

        (await new BaselineService(fileSystem).LoadAsync(Path)).ShouldBeNull();
    }

    [Fact]
    public void Apply_ShouldSuppressRecordedFindings()
    {
        var service = new BaselineService(new InMemoryFileSystem());
        var baseline = new Baseline { Fingerprints = [Sample.Fingerprint] };

        service.Apply([Sample], baseline).ShouldBeEmpty();
    }

    [Fact]
    public void Apply_ShouldKeepFindingsThatAreNotInTheBaseline()
    {
        var service = new BaselineService(new InMemoryFileSystem());
        var baseline = new Baseline { Fingerprints = ["Something|else|-|-"] };

        service.Apply([Sample], baseline).ShouldHaveSingleItem();
    }

    [Fact]
    public void Apply_ShouldReturnEverythingWhenThereIsNoBaseline() =>
        new BaselineService(new InMemoryFileSystem()).Apply([Sample], null).ShouldHaveSingleItem();
}
