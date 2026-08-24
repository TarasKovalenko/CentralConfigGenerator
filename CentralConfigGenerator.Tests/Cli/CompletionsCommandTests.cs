using CentralConfigGenerator.Cli.Commands;

namespace CentralConfigGenerator.Tests.Cli;

public class CompletionsCommandTests
{
    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("fish")]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    [InlineData("BASH")]
    public void ShouldEmitAScriptForEverySupportedShell(string shell)
    {
        var script = CompletionsCommand.Render(shell);

        script.ShouldNotBeNull();
        script.ShouldContain("central-config");
        script.ShouldContain("migrate");
    }

    [Theory]
    [InlineData("tcsh")]
    [InlineData("")]
    public void ShouldReturnNothingForAnUnsupportedShell(string shell) =>
        CompletionsCommand.Render(shell).ShouldBeNull();

    [Fact]
    public void BashScriptShouldDefineACompletionFunction() =>
        Render("bash").ShouldContain("complete -F _central_config_completions");

    [Fact]
    public void ZshScriptShouldDeclareTheCompdef() => Render("zsh").ShouldContain("#compdef central-config");

    [Fact]
    public void PowerShellScriptShouldRegisterAnArgumentCompleter() =>
        Render("powershell").ShouldContain("Register-ArgumentCompleter");

    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("fish")]
    [InlineData("powershell")]
    public void EveryScriptShouldListTheAnalyzeCommand(string shell) =>
        Render(shell).ShouldContain("analyze");

    private static string Render(string shell) =>
        CompletionsCommand.Render(shell).ShouldNotBeNull();
}
