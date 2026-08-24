namespace CentralConfigGenerator.Core.Tests.TestSupport;

/// <summary>Reusable project XML fixtures.</summary>
public static class Sample
{
    public const string AppProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
            <PackageReference Include="Serilog" Version="3.1.1" />
          </ItemGroup>
        </Project>
        """;

    public const string LibProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
            <PackageReference Include="Serilog">
              <Version>2.12.0</Version>
            </PackageReference>
          </ItemGroup>
        </Project>
        """;

    public const string PackagesProps = """
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
            <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
            <PackageVersion Include="Serilog" Version="3.1.1" />
          </ItemGroup>
        </Project>
        """;
}
