using System.Xml.Linq;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Extensions;
using CentralConfigGenerator.Services.Abstractions;

namespace CentralConfigGenerator.Commands;

public class PackagesPropsCommand(
    IPackageAnalyzer packageAnalyzer,
    IProjectFileService projectFileService,
    IPackagesPropsGenerator packagesPropsGenerator,
    IFileService fileService
)
{
    public async Task ExecuteAsync(DirectoryInfo directory, bool overwrite)
    {
        MsgLogger.LogInformation("Generating Directory.Packages.props for directory: {0}",
            directory.FullName);

        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");

        if (fileService.Exists(targetPath) && !overwrite)
        {
            MsgLogger.LogWarning("File Directory.Packages.props already exists. Use --overwrite to replace it.");
            return;
        }

        var projectFiles = await projectFileService.ScanDirectoryForProjectsAsync(directory);

        if (projectFiles.Count == 0)
        {
            MsgLogger.LogWarning("No .csproj files found in the directory tree.");
            return;
        }

        MsgLogger.LogInformation("Found {0} project files", projectFiles.Count);

        var packageVersions = packageAnalyzer.ExtractPackageVersions(projectFiles);
        MsgLogger.LogInformation("Identified {0} unique packages", packageVersions.Count);

        foreach (var package in packageVersions)
        {
            MsgLogger.LogDebug("Package: {0} = {1}", package.Key, package.Value);
        }

        var packagesPropsContent = packagesPropsGenerator.GeneratePackagesPropsContent(packageVersions);

        await fileService.WriteAllTextAsync(targetPath, packagesPropsContent);

        MsgLogger.LogInformation("Created Directory.Packages.props at {0}", targetPath);

        MsgLogger.LogInformation("Removing package version attributes from project files...");

        foreach (var projectFile in projectFiles)
        {
            try
            {
                var xDoc = XDocument.Parse(projectFile.Content);
                var changed = false;

                var packageReferences = xDoc.Descendants("PackageReference").ToList();

                foreach (var packageRef in packageReferences)
                {
                    var versionAttr = packageRef.Attribute("Version");
                    if (versionAttr != null)
                    {
                        versionAttr.Remove();
                        changed = true;
                    }
                }

                if (changed)
                {
                    await fileService.WriteAllTextAsync(projectFile.Path, xDoc.ToString());
                    MsgLogger.LogInformation("Updated package references in project file: {0}",
                        projectFile.Path);
                }
            }
            catch (Exception ex)
            {
                MsgLogger.LogError(ex, "Error updating package references in project file: {0}",
                    projectFile.Path);
            }
        }
    }
}