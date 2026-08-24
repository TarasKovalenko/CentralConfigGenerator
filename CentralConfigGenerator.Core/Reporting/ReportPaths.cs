namespace CentralConfigGenerator.Core.Reporting;

/// <summary>Path formatting shared by the report writers.</summary>
public static class ReportPaths
{
    /// <summary>
    /// Renders a path relative to the scanned root with forward slashes, so reports are stable
    /// across machines and diff cleanly in CI.
    /// </summary>
    public static string? Relative(string? path, string? root)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (string.IsNullOrEmpty(root))
        {
            return path;
        }

        try
        {
            var relative = Path.GetRelativePath(root, path);
            return relative.StartsWith("..", StringComparison.Ordinal)
                ? path
                : relative.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (ArgumentException)
        {
            return path;
        }
    }
}
