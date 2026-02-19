using System.Reflection;

namespace SystemHarness.Tests;

/// <summary>
/// Convention tests for assembly-level metadata consistency.
/// Ensures version alignment, company/author info, and SourceLink across all packages.
/// </summary>
[Trait("Category", "CI")]
public class AssemblyConventionTests
{
    private static readonly Assembly[] ProjectAssemblies =
    [
        typeof(IHarness).Assembly,                           // SystemHarness.Core
        typeof(SystemHarness.Windows.WindowsHarness).Assembly, // SystemHarness.Windows
        typeof(SystemHarness.Mcp.McpResponse).Assembly,      // SystemHarness.Mcp
        typeof(SystemHarness.Apps.Office.OfficeApp).Assembly, // SystemHarness.Apps.Office
    ];

    [Fact]
    public void AllAssemblies_HaveConsistentVersion()
    {
        // All project assemblies must share the same version
        var versions = ProjectAssemblies
            .Select(a => a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
            .Where(v => v is not null)
            .Select(v => v!.Split('+')[0]) // Strip SourceLink hash suffix
            .Distinct()
            .ToList();

        Assert.Single(versions);
        Assert.Equal("0.28.1", versions[0]);
    }

    [Fact]
    public void AllAssemblies_HaveCompanyAttribute()
    {
        var missing = new List<string>();
        foreach (var asm in ProjectAssemblies)
        {
            var company = asm.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
            if (string.IsNullOrWhiteSpace(company))
                missing.Add(asm.GetName().Name!);
        }

        Assert.True(missing.Count == 0,
            $"Assemblies missing Company attribute: {string.Join(", ", missing)}");
    }

    [Fact]
    public void AllAssemblies_HaveDescriptionAttribute()
    {
        var missing = new List<string>();
        foreach (var asm in ProjectAssemblies)
        {
            var desc = asm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
            if (string.IsNullOrWhiteSpace(desc) || desc.Length < 10)
                missing.Add(asm.GetName().Name!);
        }

        Assert.True(missing.Count == 0,
            $"Assemblies missing Description (min 10 chars): {string.Join(", ", missing)}");
    }

    [Fact]
    public void AllAssemblies_HaveRepositoryUrl()
    {
        // SourceLink embeds repository URL into the assembly metadata
        foreach (var asm in ProjectAssemblies)
        {
            var attrs = asm.GetCustomAttributes()
                .Where(a => a.GetType().FullName == "System.Reflection.AssemblyMetadataAttribute")
                .ToList();

            var repoUrl = attrs
                .Select(a =>
                {
                    var keyProp = a.GetType().GetProperty("Key");
                    var valProp = a.GetType().GetProperty("Value");
                    return (Key: keyProp?.GetValue(a)?.ToString(), Value: valProp?.GetValue(a)?.ToString());
                })
                .FirstOrDefault(kv => kv.Key == "RepositoryUrl");

            Assert.NotNull(repoUrl.Value);
            Assert.Contains("github.com/iyulab/system-harness", repoUrl.Value);
        }
    }

    [Fact]
    public void AssemblyCount_IsExact()
    {
        // Guard: update when adding new project assemblies to the test
        Assert.Equal(4, ProjectAssemblies.Length);
    }
}
