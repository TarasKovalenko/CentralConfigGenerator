namespace CentralConfigGenerator.Core.Analysis;

/// <summary>Stable identifiers for every analysis rule, usable with <c>explain</c> and <c>--rules</c>.</summary>
public static class RuleIds
{
    public const string SecurityVulnerability = "SecurityVulnerability";
    public const string InlineVersionUnderCpm = "InlineVersionUnderCpm";
    public const string LicenseRisk = "LicenseRisk";
    public const string VersionInconsistency = "VersionInconsistency";
    public const string FloatingVersion = "FloatingVersion";
    public const string TransitiveConflict = "TransitiveConflict";
    public const string DuplicatePackageCasing = "DuplicatePackageCasing";
    public const string RedundantReference = "RedundantReference";
    public const string OutdatedPackage = "OutdatedPackage";
    public const string DeprecatedPackage = "DeprecatedPackage";
    public const string FrameworkAlignment = "FrameworkAlignment";
    public const string PrereleaseInProduction = "PrereleaseInProduction";
    public const string MissingCentralVersion = "MissingCentralVersion";
    public const string OrphanedPackageVersion = "OrphanedPackageVersion";
    public const string UnpinnedTransitiveDependency = "UnpinnedTransitiveDependency";
    public const string PropertyDrift = "PropertyDrift";

    public static IReadOnlyList<string> All { get; } =
    [
        SecurityVulnerability,
        InlineVersionUnderCpm,
        LicenseRisk,
        VersionInconsistency,
        FloatingVersion,
        TransitiveConflict,
        DuplicatePackageCasing,
        RedundantReference,
        OutdatedPackage,
        DeprecatedPackage,
        FrameworkAlignment,
        PrereleaseInProduction,
        MissingCentralVersion,
        OrphanedPackageVersion,
        UnpinnedTransitiveDependency,
        PropertyDrift,
    ];
}
