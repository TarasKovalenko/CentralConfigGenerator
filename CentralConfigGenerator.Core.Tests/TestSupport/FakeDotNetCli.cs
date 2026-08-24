using CentralConfigGenerator.Core.Services;
using CentralConfigGenerator.Core.Xml;

namespace CentralConfigGenerator.Core.Tests.TestSupport;

/// <summary>
/// A scripted <c>dotnet</c> CLI. Tests decide which package versions make the "test run" pass,
/// which is what drives the bisect logic.
/// </summary>
public sealed class FakeDotNetCli(Func<IReadOnlyDictionary<string, string>, bool> testsPass, Func<string> readProps)
    : IDotNetCliService
{
    public int RestoreCount { get; private set; }

    public int TestCount { get; private set; }

    public bool IsAvailable { get; set; } = true;

    public Task<ProcessResult> RunAsync(
        string arguments,
        string workingDirectory,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(Success());

    public Task<ProcessResult> RestoreAsync(
        string target,
        string workingDirectory,
        CancellationToken cancellationToken = default
    )
    {
        RestoreCount++;
        return Task.FromResult(Success());
    }

    public Task<ProcessResult> BuildAsync(
        string target,
        string workingDirectory,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(Success());

    public Task<ProcessResult> TestAsync(
        string target,
        string workingDirectory,
        string? filter = null,
        CancellationToken cancellationToken = default
    )
    {
        TestCount++;

        var versions = MsBuildXml.TryParse(readProps(), out var document, out _)
            ? PackageReferenceReader.ReadPackageVersions(document)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return Task.FromResult(testsPass(versions) ? Success() : Failure());
    }

    private static ProcessResult Success() =>
        new()
        {
            ExitCode = 0,
            StandardOutput = string.Empty,
            StandardError = string.Empty,
            Duration = TimeSpan.Zero,
        };

    private static ProcessResult Failure() =>
        new()
        {
            ExitCode = 1,
            StandardOutput = string.Empty,
            StandardError = "tests failed",
            Duration = TimeSpan.Zero,
        };
}
