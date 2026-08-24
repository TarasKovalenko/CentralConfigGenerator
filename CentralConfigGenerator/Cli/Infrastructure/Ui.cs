using Spectre.Console;
using Spectre.Console.Rendering;

namespace CentralConfigGenerator.Cli.Infrastructure;

/// <summary>
/// Console output helpers. Every command routes user-facing text through here so <c>--quiet</c>
/// and non-interactive environments behave consistently.
/// </summary>
public static class Ui
{
    public static bool Quiet { get; set; }

    public static bool Verbose { get; set; }

    /// <summary>True when prompts can actually be answered.</summary>
    public static bool IsInteractive =>
        !Quiet && !Console.IsInputRedirected && AnsiConsole.Profile.Capabilities.Interactive;

    public static void Info(string markup)
    {
        if (!Quiet)
        {
            AnsiConsole.MarkupLine(markup);
        }
    }

    public static void Detail(string markup)
    {
        if (Verbose && !Quiet)
        {
            AnsiConsole.MarkupLine($"[grey]{markup}[/]");
        }
    }

    public static void Success(string message) => Info($"[green]✓[/] {Escape(message)}");

    public static void Warn(string message) =>
        AnsiConsole.MarkupLine($"[yellow]![/] {Escape(message)}");

    public static void Error(string message) =>
        AnsiConsole.MarkupLine($"[red]✗[/] {Escape(message)}");

    public static void Rule(string title)
    {
        if (!Quiet)
        {
            AnsiConsole.Write(new Spectre.Console.Rule($"[bold]{Escape(title)}[/]").LeftJustified());
        }
    }

    public static void Blank()
    {
        if (!Quiet)
        {
            AnsiConsole.WriteLine();
        }
    }

    public static bool Confirm(string question, bool defaultValue = true)
    {
        if (!IsInteractive)
        {
            return defaultValue;
        }

        return AnsiConsole.Confirm(Escape(question), defaultValue);
    }

    public static void Write(IRenderable renderable)
    {
        if (!Quiet)
        {
            AnsiConsole.Write(renderable);
        }
    }

    /// <summary>
    /// Runs work behind a spinner when the console can render one. Redirected output gets no
    /// spinner at all, so piped stdout stays clean.
    /// </summary>
    public static async Task<T> StatusAsync<T>(
        string message,
        Func<Action<string>, Task<T>> work
    )
    {
        if (Quiet || Console.IsOutputRedirected || !AnsiConsole.Profile.Capabilities.Ansi)
        {
            return await work(_ => { });
        }

        return await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(
                message,
                async ctx =>
                    await work(update => ctx.Status($"[grey]{Escape(update)}[/]"))
            );
    }

    public static string Escape(string value) => Markup.Escape(value);
}
