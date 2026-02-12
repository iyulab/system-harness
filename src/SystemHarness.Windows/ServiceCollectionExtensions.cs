using Microsoft.Extensions.DependencyInjection;

namespace SystemHarness.Windows;

/// <summary>
/// Extension methods for registering SystemHarness Windows services with DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Windows computer-use primitives as singleton services with default options.
    /// </summary>
    public static IServiceCollection AddSystemHarness(this IServiceCollection services)
    {
        return services.AddSystemHarness(new HarnessOptions());
    }

    /// <summary>
    /// Registers all Windows computer-use primitives as singleton services.
    /// <see cref="IHarness"/> is registered as a singleton <see cref="WindowsHarness"/>.
    /// Individual interfaces (IShell, IScreen, etc.) resolve to the same harness instance.
    /// </summary>
    public static IServiceCollection AddSystemHarness(this IServiceCollection services, HarnessOptions options)
    {
        services.AddSingleton(_ => new WindowsHarness(options));
        services.AddSingleton<IHarness>(sp => sp.GetRequiredService<WindowsHarness>());
        services.AddSingleton<IShell>(sp => sp.GetRequiredService<WindowsHarness>().Shell);
        services.AddSingleton<IProcessManager>(sp => sp.GetRequiredService<WindowsHarness>().Process);
        services.AddSingleton<IFileSystem>(sp => sp.GetRequiredService<WindowsHarness>().FileSystem);
        services.AddSingleton<IWindow>(sp => sp.GetRequiredService<WindowsHarness>().Window);
        services.AddSingleton<IClipboard>(sp => sp.GetRequiredService<WindowsHarness>().Clipboard);
        services.AddSingleton<IScreen>(sp => sp.GetRequiredService<WindowsHarness>().Screen);
        services.AddSingleton<IMouse>(sp => sp.GetRequiredService<WindowsHarness>().Mouse);
        services.AddSingleton<IKeyboard>(sp => sp.GetRequiredService<WindowsHarness>().Keyboard);
        services.AddSingleton<IDisplay>(sp => sp.GetRequiredService<WindowsHarness>().Display);
        services.AddSingleton<ISystemInfo>(sp => sp.GetRequiredService<WindowsHarness>().SystemInfo);
        services.AddSingleton<IVirtualDesktop>(sp => sp.GetRequiredService<WindowsHarness>().VirtualDesktop);
        services.AddSingleton<IDialogHandler>(sp => sp.GetRequiredService<WindowsHarness>().DialogHandler);
        services.AddSingleton<IUIAutomation>(sp => sp.GetRequiredService<WindowsHarness>().UIAutomation);
        services.AddSingleton<IOcr>(sp => sp.GetRequiredService<WindowsHarness>().Ocr);
        services.AddSingleton<ITemplateMatcher>(sp => sp.GetRequiredService<WindowsHarness>().TemplateMatcher);

        // Workflow
        services.AddSingleton<IObserver>(sp => new HarnessObserver(sp.GetRequiredService<IHarness>()));
        services.AddSingleton<IActionRecorder>(sp =>
            new WindowsActionRecorder(
                sp.GetRequiredService<IMouse>(),
                sp.GetRequiredService<IKeyboard>()));

        return services;
    }
}
