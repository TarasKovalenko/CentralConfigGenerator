using System.ComponentModel;
using CentralConfigGenerator.Cli.Infrastructure;
using CentralConfigGenerator.Core.Models;
using Spectre.Console.Cli;

namespace CentralConfigGenerator.Cli.Commands;

public sealed class CompletionsSettings : CommandSettings
{
    [CommandArgument(0, "<SHELL>")]
    [Description("bash, zsh, fish or powershell.")]
    public string Shell { get; init; } = string.Empty;
}

/// <summary>Emits a shell completion script for the tool.</summary>
public sealed class CompletionsCommand : Command<CompletionsSettings>
{
    private static readonly string[] Commands =
    [
        "migrate",
        "packages",
        "build",
        "all",
        "revert",
        "analyze",
        "update",
        "verify",
        "tree",
        "status",
        "doctor",
        "init",
        "explain",
        "batch",
        "backups",
        "completions",
    ];

    private static readonly string[] Options =
    [
        "--directory",
        "--solution",
        "--project",
        "--exclude-dirs",
        "--dry-run",
        "--diff",
        "--merge",
        "--overwrite",
        "--keep-attrs",
        "--conflict-strategy",
        "--interactive-conflicts",
        "--ignore-prerelease",
        "--transitive-pinning",
        "--encoding",
        "--linewrap",
        "--unify-props",
        "--audit",
        "--outdated",
        "--deprecated",
        "--licenses",
        "--transitive",
        "--all-checks",
        "--fail-on",
        "--rules",
        "--baseline",
        "--write-baseline",
        "--output",
        "--output-file",
        "--fix",
        "--fix-dry-run",
        "--bisect",
        "--bisect-budget",
        "--only",
        "--no-backup",
        "--backup-dir",
        "--add-gitignore",
        "--force",
        "--quiet",
        "--verbose",
        "--help",
    ];

    public override int Execute(CommandContext context, CompletionsSettings settings, CancellationToken cancellationToken)
    {
        var script = Render(settings.Shell);

        if (script is null)
        {
            Ui.Error($"Unsupported shell '{settings.Shell}'. Choose bash, zsh, fish or powershell.");
            return ExitCode.ValidationError;
        }

        Console.Out.Write(script);
        return ExitCode.Success;
    }

    /// <summary>Returns the completion script for a shell, or null when it is not supported.</summary>
    internal static string? Render(string shell) =>
        shell.ToLowerInvariant() switch
        {
            "bash" => Bash(),
            "zsh" => Zsh(),
            "fish" => Fish(),
            "powershell" or "pwsh" => PowerShell(),
            _ => null,
        };

    private static string Bash() =>
        """
        # central-config bash completion
        # Install: central-config completions bash > /etc/bash_completion.d/central-config
        _central_config_completions()
        {
            local cur commands options
            cur="${COMP_WORDS[COMP_CWORD]}"
            commands="__COMMANDS__"
            options="__OPTIONS__"

            if [[ ${COMP_CWORD} -eq 1 ]]; then
                COMPREPLY=( $(compgen -W "${commands}" -- "${cur}") )
            else
                COMPREPLY=( $(compgen -W "${options}" -- "${cur}") )
            fi
            return 0
        }
        complete -F _central_config_completions central-config

        """
            .Replace("__COMMANDS__", string.Join(" ", Commands))
            .Replace("__OPTIONS__", string.Join(" ", Options));

    private static string Zsh() =>
        """
        #compdef central-config
        # Install: central-config completions zsh > "${fpath[1]}/_central-config"
        _central_config() {
            local -a commands options
            commands=(__COMMANDS__)
            options=(__OPTIONS__)

            if (( CURRENT == 2 )); then
                _describe 'command' commands
            else
                _describe 'option' options
            fi
        }
        compdef _central_config central-config

        """
            .Replace("__COMMANDS__", string.Join(" ", Commands))
            .Replace("__OPTIONS__", string.Join(" ", Options));

    private static string Fish() =>
        string.Join(
            "\n",
            new[] { "# central-config fish completion", "# Install: central-config completions fish > ~/.config/fish/completions/central-config.fish" }
                .Concat(
                    Commands.Select(c =>
                        $"complete -c central-config -n '__fish_use_subcommand' -a '{c}'"
                    )
                )
                .Concat(
                    Options.Select(o => $"complete -c central-config -l '{o.TrimStart('-')}'")
                )
        ) + "\n";

    private static string PowerShell() =>
        $$"""
        # central-config PowerShell completion
        # Install: central-config completions powershell | Out-String | Invoke-Expression
        Register-ArgumentCompleter -Native -CommandName central-config -ScriptBlock {
            param($wordToComplete, $commandAst, $cursorPosition)
            $commands = @({{string.Join(", ", Commands.Select(c => $"'{c}'"))}})
            $options = @({{string.Join(", ", Options.Select(o => $"'{o}'"))}})
            $candidates = if ($commandAst.CommandElements.Count -le 2) { $commands + $options } else { $options }
            $candidates | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
            }
        }

        """;
}
