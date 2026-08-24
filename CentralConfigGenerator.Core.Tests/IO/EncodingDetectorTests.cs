using System.Text;
using CentralConfigGenerator.Core.IO;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.Tests.IO;

public class EncodingDetectorTests
{
    [Fact]
    public void Decode_ShouldDetectUtf8WithoutBom()
    {
        var bytes = new UTF8Encoding(false).GetBytes("<Project />");

        var (content, format) = EncodingDetector.Decode(bytes);

        content.ShouldBe("<Project />");
        format.HasByteOrderMark.ShouldBeFalse();
    }

    [Fact]
    public void Decode_ShouldDetectUtf8Bom()
    {
        var bytes = new UTF8Encoding(true).GetPreamble()
            .Concat(new UTF8Encoding(false).GetBytes("<Project />"))
            .ToArray();

        var (content, format) = EncodingDetector.Decode(bytes);

        content.ShouldBe("<Project />");
        format.HasByteOrderMark.ShouldBeTrue();
    }

    [Fact]
    public void Decode_ShouldDetectUtf16LittleEndian()
    {
        var bytes = new UnicodeEncoding(false, true).GetPreamble()
            .Concat(new UnicodeEncoding(false, false).GetBytes("<Project />"))
            .ToArray();

        var (content, format) = EncodingDetector.Decode(bytes);

        content.ShouldBe("<Project />");
        format.HasByteOrderMark.ShouldBeTrue();
        format.Encoding.ShouldBeOfType<UnicodeEncoding>();
    }

    [Theory]
    [InlineData("a\r\nb\r\nc", LineEndingStyle.CrLf)]
    [InlineData("a\nb\nc", LineEndingStyle.Lf)]
    [InlineData("a\rb\rc", LineEndingStyle.Cr)]
    [InlineData("single line", LineEndingStyle.Preserve)]
    public void Decode_ShouldDetectLineEndings(string content, LineEndingStyle expected)
    {
        var (_, format) = EncodingDetector.Decode(Encoding.UTF8.GetBytes(content));

        format.LineEnding.ShouldBe(expected);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveBytesExactly()
    {
        var original = new UTF8Encoding(true).GetPreamble()
            .Concat(new UTF8Encoding(false).GetBytes("<Project>\r\n  <X />\r\n</Project>\r\n"))
            .ToArray();

        var (content, format) = EncodingDetector.Decode(original);
        var round = EncodingDetector.Encode(content, format);

        round.ShouldBe(original);
    }

    [Fact]
    public void Encode_ShouldRewriteLineEndingsWhenAsked()
    {
        var (content, format) = EncodingDetector.Decode(Encoding.UTF8.GetBytes("a\r\nb\r\n"));

        var bytes = EncodingDetector.Encode(content, format with { LineEnding = LineEndingStyle.Lf });

        Encoding.UTF8.GetString(bytes).ShouldBe("a\nb\n");
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("UTF-8")]
    [InlineData("us-ascii")]
    public void Resolve_ShouldAcceptIanaNames(string name) =>
        EncodingDetector.Resolve(name).ShouldNotBeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-encoding")]
    public void Resolve_ShouldReturnNullForUnknownNames(string? name) =>
        EncodingDetector.Resolve(name).ShouldBeNull();
}
