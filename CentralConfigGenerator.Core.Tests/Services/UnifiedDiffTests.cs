using CentralConfigGenerator.Core.Services;

namespace CentralConfigGenerator.Core.Tests.Services;

public class UnifiedDiffTests
{
    [Fact]
    public void Create_ShouldReturnEmptyForIdenticalInput() =>
        UnifiedDiff.Create("same\ntext", "same\ntext", "a", "b").ShouldBeEmpty();

    [Fact]
    public void Create_ShouldMarkAddedAndRemovedLines()
    {
        var diff = UnifiedDiff.Create("one\ntwo\nthree", "one\nTWO\nthree", "a.txt", "b.txt");

        diff.ShouldContain("--- a.txt");
        diff.ShouldContain("+++ b.txt");
        diff.ShouldContain("-two");
        diff.ShouldContain("+TWO");
        diff.ShouldContain(" one");
    }

    [Fact]
    public void Create_ShouldIncludeAHunkHeader() =>
        UnifiedDiff.Create("a", "b", "x", "y").ShouldContain("@@");

    [Fact]
    public void Create_ShouldHandlePureInsertion()
    {
        var diff = UnifiedDiff.Create(string.Empty, "new line", "/dev/null", "b.txt");

        diff.ShouldContain("+new line");
    }

    [Fact]
    public void Create_ShouldNormaliseWindowsLineEndings()
    {
        var diff = UnifiedDiff.Create("a\r\nb", "a\nb", "x", "y");

        diff.ShouldBeEmpty();
    }
}
