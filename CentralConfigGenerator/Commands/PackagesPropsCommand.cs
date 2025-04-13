using System.Xml.Linq;
using CentralConfigGenerator.Core.Analyzers;
using CentralConfigGenerator.Core.Generators;
using CentralConfigGenerator.Core.Models;
using CentralConfigGenerator.Extensions;
using CentralConfigGenerator.Services;

namespace CentralConfigGenerator.Commands;

public class PackagesPropsCommand(
    IPackageAnalyzer packageAnalyzer,
    IPackagesPropsGenerator packagesPropsGenerator,
    IFileService fileService
)
{
    public async Task ExecuteAsync(DirectoryInfo directory, bool overwrite)
    {
        MsgExtensions.LogInformation("Generating Directory.Packages.props for directory: {0}",
            directory.FullName);

        var targetPath = Path.Combine(directory.FullName, "Directory.Packages.props");

        // Check if file already exists
        if (fileService.Exists(targetPath) && !overwrite)
        {
            MsgExtensions.LogWarning("File Directory.Packages.props already exists. Use --overwrite to replace it.");
            return;
        }

        // Scan for project files
        var projectFiles = new List<ProjectFile>();
        foreach (var file in directory.GetFiles("*.csproj", SearchOption.AllDirectories))
        {
            try
            {
                var content = await fileService.ReadAllTextAsync(file.FullName);
                projectFiles.Add(new ProjectFile
                {
                    Path = file.FullName,
                    Content = content
                });
            }
            catch (Exception ex)
            {
                MsgExtensions.LogError(ex, "Error reading project file: {0}", file.FullName);
            }
        }

        if (projectFiles.Count == 0)
        {
            MsgExtensions.LogWarning("No .csproj files found in the directory tree.");
            return;
        }

        MsgExtensions.LogInformation("Found {0} project files", projectFiles.Count);

        // Extract package versions
        var packageVersions = packageAnalyzer.ExtractPackageVersions(projectFiles);
        MsgExtensions.LogInformation("Identified {0} unique packages", packageVersions.Count);

        foreach (var package in packageVersions)
        {
            MsgExtensions.LogDebug("Package: {0} = {1}", package.Key, package.Value);
        }

        // Generate packages props content
        var packagesPropsContent = packagesPropsGenerator.GeneratePackagesPropsContent(packageVersions);

        // Write to file
        await fileService.WriteAllTextAsync(targetPath, packagesPropsContent);

        MsgExtensions.LogInformation("Created Directory.Packages.props at {0}", targetPath);

        MsgExtensions.LogInformation("Removing package version attributes from project files...");

        foreach (var projectFile in projectFiles)
        {
            try
            {
                // Load the project file
                var xDoc = XDocument.Parse(projectFile.Content);
                var changed = false;

                // Find all PackageReference elements
                var packageReferences = xDoc.Descendants("PackageReference").ToList();

                foreach (var packageRef in packageReferences)
                {
                    // Check if this package has a Version attribute
                    var versionAttr = packageRef.Attribute("Version");
                    if (versionAttr != null)
                    {
                        // Remove the Version attribute
                        versionAttr.Remove();
                        changed = true;
                    }
                }

                // Save the modified project file if changes were made
                if (changed)
                {
                    await fileService.WriteAllTextAsync(projectFile.Path, xDoc.ToString());
                    MsgExtensions.LogInformation("Updated package references in project file: {0}",
                        projectFile.Path);
                }
            }
            catch (Exception ex)
            {
                MsgExtensions.LogError(ex, "Error updating package references in project file: {0}",
                    projectFile.Path);
            }
        }
    }
}