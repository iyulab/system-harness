using System.Reflection;
using System.Runtime.InteropServices;

namespace SystemHarness;

/// <summary>
/// Creates an <see cref="IHarness"/> for the current platform at runtime.
/// Loads the platform assembly by convention — <c>SystemHarness.Windows</c>, the only implementation.
/// </summary>
public static class HarnessFactory
{
    /// <summary>
    /// Creates an <see cref="IHarness"/> appropriate for the current operating system.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">
    /// The OS is not Windows, or the <c>SystemHarness.Windows</c> package is not referenced.
    /// </exception>
    public static IHarness Create(HarnessOptions? options = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException(
                $"SystemHarness supports Windows only; there is no implementation for {RuntimeInformation.OSDescription}.");

        const string assemblyName = "SystemHarness.Windows";
        const string typeName = "SystemHarness.Windows.WindowsHarness";

        Assembly assembly;
        try
        {
            assembly = Assembly.Load(assemblyName);
        }
        catch (FileNotFoundException ex)
        {
            throw new PlatformNotSupportedException(
                $"Could not load platform assembly '{assemblyName}'. Reference the {assemblyName} package.", ex);
        }

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
}
