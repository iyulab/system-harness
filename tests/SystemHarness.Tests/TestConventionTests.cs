using System.Reflection;

namespace SystemHarness.Tests;

/// <summary>
/// Convention tests for the test project itself.
/// Ensures consistent categorization and structure across all test classes.
/// </summary>
[Trait("Category", "CI")]
public class TestConventionTests
{
    private static readonly Assembly TestAssembly = typeof(TestConventionTests).Assembly;

    private static IEnumerable<Type> AllTestClasses()
    {
        return TestAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Tests", StringComparison.Ordinal))
            .Where(t => t.GetMethods().Any(m =>
                m.GetCustomAttribute<FactAttribute>() is not null ||
                m.GetCustomAttribute<TheoryAttribute>() is not null));
    }

    [Fact]
    public void AllTestClasses_HaveCategoryTrait()
    {
        // Guard: every test class must be categorized as CI or Local
        var missing = new List<string>();
        foreach (var type in AllTestClasses())
        {
            var hasTrait = type.GetCustomAttributes(typeof(TraitAttribute), inherit: false).Length > 0;
            if (!hasTrait)
                missing.Add(type.FullName ?? type.Name);
        }

        Assert.True(missing.Count == 0,
            $"Test classes missing [Trait(\"Category\", ...)] (add \"CI\" or \"Local\"): {string.Join(", ", missing)}");
    }

    [Fact]
    public void AllTestClasses_CategoryIsValid()
    {
        // Guard: category must be "CI", "Local", or "RequiresDesktop" (supplementary).
        // Every class must have exactly one primary category ("CI" or "Local").
        // "RequiresDesktop" is an optional supplementary trait for CI desktop-test filtering.
        var invalid = new List<string>();
        var ciCount = 0;
        var localCount = 0;

        foreach (var type in AllTestClasses())
        {
            var attrs = type.GetCustomAttributesData()
                .Where(a => a.AttributeType == typeof(TraitAttribute))
                .ToList();

            foreach (var attr in attrs)
            {
                var args = attr.ConstructorArguments;
                if (args.Count == 2)
                {
                    var key = args[0].Value?.ToString();
                    var value = args[1].Value?.ToString();
                    if (key == "Category")
                    {
                        if (value == "CI") ciCount++;
                        else if (value == "Local") localCount++;
                        else if (value == "RequiresDesktop") { /* valid supplementary category */ }
                        else invalid.Add($"{type.Name} has Category='{value}'");
                    }
                }
            }
        }

        Assert.True(invalid.Count == 0,
            $"Test classes with invalid Category (use 'CI', 'Local', or 'RequiresDesktop'): {string.Join(", ", invalid)}");
        Assert.True(ciCount + localCount == AllTestClasses().Count(),
            $"CI({ciCount}) + Local({localCount}) should equal total({AllTestClasses().Count()})");
    }

    [Fact]
    public void TestClassCount_IsExact()
    {
        // Guard: catches untracked additions of test classes
        // Update this count when adding new test classes
        var count = AllTestClasses().Count();
        Assert.Equal(70, count); // 66 existing + 1 for this class + 3 new (CommandRegistry, RateLimiter, SafeZone)
    }
}
