using CentralConfigGenerator.Core.Analysis;

namespace CentralConfigGenerator.Core.Tests.Analysis;

public class AnalysisReportTests
{
    [Fact]
    public void HealthScore_ShouldBe100ForACleanWorkspace() =>
        Report([]).HealthScore.ShouldBe(100);

    [Fact]
    public void HealthScore_ShouldDropSharplyForCriticalFindings()
    {
        var report = Report([Finding(Severity.Critical)]);

        report.HealthScore.ShouldBeLessThan(80);
        report.Grade.ShouldNotBe("A");
    }

    [Fact]
    public void HealthScore_ShouldBarelyMoveForInfoFindings() =>
        Report([Finding(Severity.Info)]).HealthScore.ShouldBeGreaterThan(95);

    [Fact]
    public void HealthScore_ShouldNeverGoBelowZero()
    {
        var findings = Enumerable.Repeat(Finding(Severity.Critical), 100).ToList();

        Report(findings).HealthScore.ShouldBe(0);
    }

    [Fact]
    public void HealthScore_ShouldScaleWithWorkspaceSize()
    {
        var findings = Enumerable.Repeat(Finding(Severity.Moderate), 5).ToList();

        var small = Report(findings, packageCount: 5);
        var large = Report(findings, packageCount: 500);

        large.HealthScore.ShouldBeGreaterThan(small.HealthScore);
    }

    [Fact]
    public void HighestSeverity_ShouldReturnTheWorstFinding() =>
        Report([Finding(Severity.Low), Finding(Severity.High)]).HighestSeverity
            .ShouldBe(Severity.High);

    [Fact]
    public void HighestSeverity_ShouldBeNeverWhenThereAreNoFindings() =>
        Report([]).HighestSeverity.ShouldBe(Severity.Never);

    [Theory]
    [InlineData(100, "A")]
    [InlineData(85, "B")]
    [InlineData(75, "C")]
    [InlineData(65, "D")]
    [InlineData(10, "F")]
    public void Grade_ShouldFollowTheScore(int targetScore, string expected)
    {
        // Work backwards: pick a finding count that lands near the target score.
        var report = Report([], packageCount: 9);
        var findings = new List<Finding>();

        while (Report(findings, packageCount: 9).HealthScore > targetScore)
        {
            findings.Add(Finding(Severity.Low));
        }

        report = Report(findings, packageCount: 9);
        report.Grade.ShouldBe(expected);
    }

    private static Finding Finding(Severity severity) =>
        new()
        {
            RuleId = "X",
            Severity = severity,
            Message = "m",
        };

    private static AnalysisReport Report(IReadOnlyList<Finding> findings, int packageCount = 9) =>
        new()
        {
            Findings = findings,
            ProjectCount = 3,
            PackageCount = packageCount,
        };
}
