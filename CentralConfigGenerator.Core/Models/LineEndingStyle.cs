namespace CentralConfigGenerator.Core.Models;

/// <summary>
/// Line ending style used when writing files back to disk.
/// </summary>
public enum LineEndingStyle
{
    /// <summary>Keep whatever the original file used.</summary>
    Preserve = 0,
    Lf,
    CrLf,
    Cr,
}

public static class LineEndingStyleExtensions
{
    public static string ToNewLine(this LineEndingStyle style) =>
        style switch
        {
            LineEndingStyle.Lf => "\n",
            LineEndingStyle.CrLf => "\r\n",
            LineEndingStyle.Cr => "\r",
            _ => Environment.NewLine,
        };

    public static LineEndingStyle Detect(string content)
    {
        var crlf = 0;
        var lf = 0;
        var cr = 0;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c == '\r')
            {
                if (i + 1 < content.Length && content[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
            else if (c == '\n')
            {
                lf++;
            }
        }

        if (crlf == 0 && lf == 0 && cr == 0)
        {
            return LineEndingStyle.Preserve;
        }

        if (crlf >= lf && crlf >= cr)
        {
            return LineEndingStyle.CrLf;
        }

        return lf >= cr ? LineEndingStyle.Lf : LineEndingStyle.Cr;
    }

    public static string Normalize(string content, LineEndingStyle style)
    {
        if (style == LineEndingStyle.Preserve)
        {
            return content;
        }

        var unified = content.Replace("\r\n", "\n").Replace('\r', '\n');
        return style switch
        {
            LineEndingStyle.Lf => unified,
            LineEndingStyle.CrLf => unified.Replace("\n", "\r\n"),
            LineEndingStyle.Cr => unified.Replace('\n', '\r'),
            _ => content,
        };
    }
}
