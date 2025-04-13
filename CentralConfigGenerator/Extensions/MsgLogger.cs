using Spectre.Console;

namespace CentralConfigGenerator.Extensions;

public static class MsgLogger
{
    public static void LogDebug(string message, params object[] args)
    {
#if DEBUG
        AnsiConsole.MarkupLineInterpolated($"[blue]Debug:{string.Format(message, args)}[/]");
#endif
    }

    public static void LogInformation(string message, params object[] args) =>
        AnsiConsole.MarkupLineInterpolated($"[green]Info:{string.Format(message, args)}[/]");

    public static void LogWarning(string message, params object[] args) =>
        AnsiConsole.MarkupLineInterpolated($"[yellow]Warning:{string.Format(message, args)}[/]");

    public static void LogError(string message, params object[] args) =>
        AnsiConsole.MarkupLineInterpolated($"[red]Error:{string.Format(message, args)}[/]");

    public static void LogError(Exception exception, string message, params object[] args)
    {
        LogError(message, args);
        AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
    }
}