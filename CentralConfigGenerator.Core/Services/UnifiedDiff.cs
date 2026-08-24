using System.Text;

namespace CentralConfigGenerator.Core.Services;

/// <summary>
/// Minimal unified-diff generator (Myers-style LCS) used by <c>--dry-run --diff</c>.
/// </summary>
public static class UnifiedDiff
{
    public static string Create(
        string oldText,
        string newText,
        string oldLabel,
        string newLabel,
        int context = 3
    )
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);
        var hunks = BuildHunks(oldLines, newLines, context);

        if (hunks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("--- ").Append(oldLabel).Append('\n');
        builder.Append("+++ ").Append(newLabel).Append('\n');

        foreach (var hunk in hunks)
        {
            builder
                .Append("@@ -")
                .Append(hunk.OldStart + 1)
                .Append(',')
                .Append(hunk.OldCount)
                .Append(" +")
                .Append(hunk.NewStart + 1)
                .Append(',')
                .Append(hunk.NewCount)
                .Append(" @@\n");

            foreach (var line in hunk.Lines)
            {
                builder.Append(line).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private sealed record Hunk(int OldStart, int OldCount, int NewStart, int NewCount, List<string> Lines);

    private static List<Hunk> BuildHunks(string[] oldLines, string[] newLines, int context)
    {
        var operations = Diff(oldLines, newLines);
        var hunks = new List<Hunk>();

        var index = 0;
        while (index < operations.Count)
        {
            if (operations[index].Kind == OpKind.Equal)
            {
                index++;
                continue;
            }

            var start = Math.Max(0, index - context);
            var end = index;
            while (end < operations.Count)
            {
                if (operations[end].Kind != OpKind.Equal)
                {
                    end++;
                    continue;
                }

                // Extend across short runs of equal lines so nearby edits share a hunk.
                var runEnd = end;
                while (runEnd < operations.Count && operations[runEnd].Kind == OpKind.Equal)
                {
                    runEnd++;
                }

                if (runEnd - end > context * 2 || runEnd == operations.Count)
                {
                    end = Math.Min(operations.Count, end + context);
                    break;
                }

                end = runEnd;
            }

            var lines = new List<string>();
            int oldStart = -1,
                newStart = -1,
                oldCount = 0,
                newCount = 0;

            for (var i = start; i < end; i++)
            {
                var op = operations[i];
                oldStart = oldStart < 0 && op.OldIndex >= 0 ? op.OldIndex : oldStart;
                newStart = newStart < 0 && op.NewIndex >= 0 ? op.NewIndex : newStart;

                switch (op.Kind)
                {
                    case OpKind.Equal:
                        lines.Add(" " + op.Text);
                        oldCount++;
                        newCount++;
                        break;
                    case OpKind.Delete:
                        lines.Add("-" + op.Text);
                        oldCount++;
                        break;
                    case OpKind.Insert:
                        lines.Add("+" + op.Text);
                        newCount++;
                        break;
                }
            }

            hunks.Add(
                new Hunk(
                    Math.Max(0, oldStart),
                    oldCount,
                    Math.Max(0, newStart),
                    newCount,
                    lines
                )
            );
            index = end;
        }

        return hunks;
    }

    private enum OpKind
    {
        Equal,
        Delete,
        Insert,
    }

    private sealed record Op(OpKind Kind, string Text, int OldIndex, int NewIndex);

    private static List<Op> Diff(string[] a, string[] b)
    {
        var lengths = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = a[i] == b[j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var operations = new List<Op>();
        int x = 0,
            y = 0;

        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                operations.Add(new Op(OpKind.Equal, a[x], x, y));
                x++;
                y++;
            }
            else if (lengths[x + 1, y] >= lengths[x, y + 1])
            {
                operations.Add(new Op(OpKind.Delete, a[x], x, -1));
                x++;
            }
            else
            {
                operations.Add(new Op(OpKind.Insert, b[y], -1, y));
                y++;
            }
        }

        while (x < a.Length)
        {
            operations.Add(new Op(OpKind.Delete, a[x], x, -1));
            x++;
        }

        while (y < b.Length)
        {
            operations.Add(new Op(OpKind.Insert, b[y], -1, y));
            y++;
        }

        return operations;
    }
}
