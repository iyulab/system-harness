using System.Reflection;
using System.Runtime.InteropServices;

namespace SystemHarness;

/// <summary>
/// Creates an <see cref="IHarness"/> for the current platform at runtime.
/// Loads the platform-specific assembly by convention (e.g. SystemHarness.Windows).
/// </summary>
public static class HarnessFactory
{
    /// <summary>
    /// Creates an <see cref="IHarness"/> appropriate for the current operating system.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">No platform implementation found.</exception>
    public static IHarness Create(HarnessOptions? options = null)
    {
        var (assemblyName, typeName) = GetPlatformType();

        var assembly = Assembly.Load(assemblyName)
            ?? throw new PlatformNotSupportedException(
                $"Could not load platform assembly '{assemblyName}'. " +
                $"Ensure the NuGet package is referenced.");

        var type = assembly.GetType(typeName)
            ?? throw new PlatformNotSupportedException(
                $"Could not find type '{typeName}' in assembly '{assemblyName}'.");

        // Try constructor with HarnessOptions first, then parameterless
        if (options is not null)
        {
            var optionsCtor = type.GetConstructor([typeof(HarnessOptions)]);
            if (optionsCtor is not null)
                return (IHarness)optionsCtor.Invoke([options]);
        }

        return (IHarness)(Activator.CreateInstance(type)
            ?? throw new PlatformNotSupportedException(
                $"Could not create instance of '{typeName}'."));
    }

    private static (string AssemblyName, string TypeName) GetPlatformType()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ("SystemHarness.Windows", "SystemHarness.Windows.WindowsHarness");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return ("SystemHarness.Linux", "SystemHarness.Linux.LinuxHarness");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ("SystemHarness.Mac", "SystemHarness.Mac.MacHarness");

        throw new PlatformNotSupportedException(
            $"Unsupported platform: {RuntimeInformation.OSDescription}");
    }
}
