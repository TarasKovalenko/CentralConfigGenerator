using System.Text;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.IO;

/// <summary>
/// Captures the on-disk representation of a text file so it can be rewritten byte-faithfully.
/// </summary>
public sealed record TextFileFormat
{
    public static readonly TextFileFormat Default = new()
    {
        Encoding = new UTF8Encoding(false),
        HasByteOrderMark = false,
        LineEnding = LineEndingStyle.Preserve,
        EndsWithNewLine = true,
    };

    public required Encoding Encoding { get; init; }

    public required bool HasByteOrderMark { get; init; }

    public required LineEndingStyle LineEnding { get; init; }

    public required bool EndsWithNewLine { get; init; }

    public Encoding ToWriteEncoding()
    {
        if (Encoding is UTF8Encoding)
        {
            return new UTF8Encoding(HasByteOrderMark);
        }

        if (HasByteOrderMark)
        {
            return Encoding;
        }

        // Strip the preamble for encodings that emit one by default (UTF-16/32).
        return Encoding.GetPreamble().Length == 0
            ? Encoding
            : (Encoding)new UTF8Encoding(false);
    }
}
