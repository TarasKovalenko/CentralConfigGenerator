using System.Diagnostics;
using System.Text;

namespace CentralConfigGenerator.Core.Services;

/// <summary>Result of running a <c>dotnet</c> sub-command.</summary>
public sealed record ProcessResult
{
    public required int ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public required TimeSpan Duration { get; init; }

    public bool Succeeded => ExitCode == 0;

    public string CombinedOutput =>
        string.IsNullOrWhiteSpace(StandardError)
            ? StandardOutput
            : StandardOutput + Environment.NewLine + StandardError;
}

public interface IDotNetCliService
{
    bool IsAvailable { get; }

    Task<ProcessResult> RunAsync(
        string arguments,
        string workingDirectory,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    );

    Task<ProcessResult> RestoreAsync(
        string target,
        string workingDirectory,
        CancellationToken cancellationToken = default
    );

    Task<ProcessResult> BuildAsync(
        string target,
        string workingDirectory,
        CancellationToken cancellationToken = default
    );

    Task<ProcessResult> TestAsync(
        string target,
        string workingDirectory,
        string? filter = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Runs the local <c>dotnet</c> CLI, used for restores, package graphs, builds and tests.</summary>
public sealed class DotNetCliService : IDotNetCliService
{
    private bool? _available;

    public bool IsAvailable
    {
        get
        {
            _available ??= Probe();
            return _available.Value;
        }
    }

    public async Task<ProcessResult> RunAsync(
        string arguments,
        string workingDirectory,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        var startInfo = new ProcessStartInfo("dotnet")
        {
            Arguments = arguments,
            WorkingDirectory = Directory.Exists(workingDirectory)
                ? workingDirectory
                : Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Keep output machine-parsable and free of MSBuild's terminal logger escape codes.
        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["TERM"] = "dumb";

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new ProcessResult
            {
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message,
                Duration = stopwatch.Elapsed,
            };
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutSource = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : new CancellationTokenSource();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token
        );

        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);

            return new ProcessResult
            {
                ExitCode = -1,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString() + Environment.NewLine + "Process timed out.",
                Duration = stopwatch.Elapsed,
            };
        }

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
            Duration = stopwatch.Elapsed,
        };
    }

    public Task<ProcessResult> RestoreAsync(
        string target,
        string workingDirectory,
        CancellationToken cancellationToken = default
    ) => RunAsync($"restore \"{target}\"", workingDirectory, TimeSpan.FromMinutes(10), cancellationToken);

    public Task<ProcessResult> BuildAsync(
        string target,
        string workingDirectory,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(
            $"build \"{target}\" --nologo -v quiet",
            workingDirectory,
            TimeSpan.FromMinutes(15),
            cancellationToken
        );

    public Task<ProcessResult> TestAsync(
        string target,
        string workingDirectory,
        string? filter = null,
        CancellationToken cancellationToken = default
    )
    {
        var arguments = $"test \"{target}\" --nologo -v quiet";
        if (!string.IsNullOrWhiteSpace(filter))
        {
            arguments += $" --filter \"{filter}\"";
        }

        return RunAsync(arguments, workingDirectory, TimeSpan.FromMinutes(30), cancellationToken);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // The process already exited or cannot be killed; nothing useful to do.
        }
    }

    private static bool Probe()
    {
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo("dotnet", "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            );

            if (process is null)
            {
                return false;
            }

            process.WaitForExit(10_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
