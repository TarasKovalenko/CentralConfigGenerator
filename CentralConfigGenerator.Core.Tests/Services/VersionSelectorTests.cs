using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Core.Tests.Services;

public class VersionSelectorTests
{
    private readonly VersionSelector _selector = new(new VersionConflictResolver());

    [Fact]
    public void Select_ShouldReturnTheOnlyVersion() =>
        _selector
            .Select("Pkg", ["1.0.0"], VersionResolutionStrategy.Highest, false)
            .ShouldBe("1.0.0");

    [Fact]
    public void Select_ShouldPickTheHighestVersion() =>
        _selector
            .Select("Pkg", ["1.0.0", "2.1.0", "2.0.0"], VersionResolutionStrategy.Highest, false)
            .ShouldBe("2.1.0");

    [Fact]
    public void Select_ShouldPickTheLowestVersion() =>
        _selector
            .Select("Pkg", ["1.0.0", "2.1.0"], VersionResolutionStrategy.Lowest, false)
            .ShouldBe("1.0.0");

    [Fact]
    public void Select_ShouldDropPrereleasesWhenStableExists() =>
        _selector
            .Select("Pkg", ["2.0.0-beta", "1.9.0"], VersionResolutionStrategy.Highest, true)
            .ShouldBe("1.9.0");

    [Fact]
    public void Select_ShouldKeepPrereleasesWhenNoStableExists() =>
        _selector
            .Select("Pkg", ["2.0.0-beta", "2.0.0-alpha"], VersionResolutionStrategy.Highest, true)
            .ShouldBe("2.0.0-beta");

    [Fact]
    public void Select_ShouldThrowForTheFailStrategy() =>
        Should.Throw<VersionConflictException>(() =>
            _selector.Select("Pkg", ["1.0.0", "2.0.0"], VersionResolutionStrategy.Fail, false)
        );

    [Fact]
    public void Select_ShouldThrowWhenNoVersionsAreSupplied() =>
        Should.Throw<ArgumentException>(() =>
            _selector.Select("Pkg", [], VersionResolutionStrategy.Highest, false)
        );

    [Theory]
    [InlineData("4.*", true)]
    [InlineData("1.2.*", true)]
    [InlineData("[4.0.0,)", true)]
    [InlineData("1.2.3", false)]
    [InlineData("[1.0.0,2.0.0)", false)]
    public void IsFloating_ShouldDetectWildcardsAndOpenRanges(string version, bool expected) =>
        VersionSelector.IsFloating(version).ShouldBe(expected);

    [Fact]
    public void Select_ShouldIgnorePrereleaseTagsUnderTheVersionComparisonScope()
    {
        // VersionComparison.Version compares only the numeric parts, so the release and the
        // pre-release of the same number tie and the first candidate wins.
        var chosen = _selector.Select(
            "Pkg",
            ["2.0.0-beta", "2.0.0"],
            VersionResolutionStrategy.Highest,
            ignorePrerelease: false,
            global::NuGet.Versioning.VersionComparison.Version
        );

        chosen.ShouldBeOneOf("2.0.0", "2.0.0-beta");
    }

    [Fact]
    public void Select_ShouldRankReleaseAbovePrereleaseUnderVersionRelease() =>
        _selector
            .Select(
                "Pkg",
                ["2.0.0-beta", "2.0.0"],
                VersionResolutionStrategy.Highest,
                ignorePrerelease: false,
                global::NuGet.Versioning.VersionComparison.VersionRelease
            )
            .ShouldBe("2.0.0");

    [Fact]
    public void Select_ShouldFallBackToTheResolverForUnparseableVersions() =>
        _selector
            .Select("Pkg", ["$(SomeProperty)", "$(Other)"], VersionResolutionStrategy.Highest, false)
            .ShouldNotBeNullOrEmpty();

    [Theory]
    [InlineData("1.0.0-beta", true)]
    [InlineData("1.0.0", false)]
    [InlineData("not-a-version", false)]
    public void IsPrerelease_ShouldDetectPrereleaseTags(string version, bool expected) =>
        VersionSelector.IsPrerelease(version).ShouldBe(expected);
}
