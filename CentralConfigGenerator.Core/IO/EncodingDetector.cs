using System.Text;
using CentralConfigGenerator.Core.Models;

namespace CentralConfigGenerator.Core.IO;

/// <summary>
/// Detects the encoding, BOM presence and line endings of a file from its raw bytes.
/// </summary>
public static class EncodingDetector
{
    public static (string Content, TextFileFormat Format) Decode(byte[] bytes)
    {
        var (encoding, bomLength) = DetectEncoding(bytes);
        var content = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);

        var format = new TextFileFormat
        {
            Encoding = encoding,
            HasByteOrderMark = bomLength > 0,
            LineEnding = LineEndingStyleExtensions.Detect(content),
            EndsWithNewLine = content.EndsWith('\n') || content.EndsWith('\r'),
        };

        return (content, format);
    }

    public static byte[] Encode(string content, TextFileFormat format)
    {
        var text = LineEndingStyleExtensions.Normalize(content, format.LineEnding);
        var encoding = format.ToWriteEncoding();
        var preamble = format.HasByteOrderMark ? encoding.GetPreamble() : [];
        var body = encoding.GetBytes(text);

        if (preamble.Length == 0)
        {
            return body;
        }

        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    /// <summary>
    /// Resolves an IANA web name (for example <c>utf-8</c> or <c>windows-1252</c>) to an encoding.
    /// </summary>
    public static Encoding? Resolve(string? webName)
    {
        if (string.IsNullOrWhiteSpace(webName))
        {
            return null;
        }

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch (InvalidOperationException)
        {
            // Provider already registered.
        }

        try
        {
            return Encoding.GetEncoding(webName.Trim());
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static (Encoding Encoding, int BomLength) DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
        {
            return (new UTF32Encoding(false, true), 4);
        }

        if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            return (new UTF32Encoding(true, true), 4);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(true), 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (new UnicodeEncoding(false, true), 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (new UnicodeEncoding(true, true), 2);
        }

        return (new UTF8Encoding(false), 0);
    }
}
