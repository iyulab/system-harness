using SystemHarness.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace SystemHarness.Tests;

[Trait("Category", "CI")]
public class WindowsHarnessTests
{
    [Fact]
    public void Constructor_ExposesAllServices()
    {
        using var harness = new WindowsHarness();

        // Layer 1: Programmatic
        Assert.NotNull(harness.Shell);
        Assert.NotNull(harness.Process);
        Assert.NotNull(harness.FileSystem);
        Assert.NotNull(harness.Window);
        Assert.NotNull(harness.Clipboard);
        Assert.NotNull(harness.SystemInfo);

        // Layer 2: Vision+Action
        Assert.NotNull(harness.Screen);
        Assert.NotNull(harness.Mouse);
        Assert.NotNull(harness.Keyboard);

        // Extended services
        Assert.NotNull(harness.Display);
        Assert.NotNull(harness.VirtualDesktop);
        Assert.NotNull(harness.DialogHandler);
        Assert.NotNull(harness.UIAutomation);
        Assert.NotNull(harness.Ocr);
        Assert.NotNull(harness.TemplateMatcher);
    }

    [Fact]
    public void ImplementsIHarness()
    {
        using var harness = new WindowsHarness();
#pragma warning disable CA1859 // intentional: testing interface implementation
        IHarness iface = harness;
#pragma warning restore CA1859

        Assert.NotNull(iface.Shell);
        Assert.NotNull(iface.Screen);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var harness = new WindowsHarness();
        harness.Dispose();
        harness.Dispose(); // should not throw
    }

    [Fact]
    public async Task Shell_WorksThroughFacade()
    {
        using var harness = new WindowsHarness();
        var result = await harness.Shell.RunAsync("cmd", "/C echo hello");
        Assert.True(result.Success);
        Assert.Contains("hello", result.StdOut);
    }

    [Fact]
    public async Task FileSystem_WorksThroughFacade()
    {
        using var harness = new WindowsHarness();
        var tempFile = Path.Combine(Path.GetTempPath(), $"harness-test-{Guid.NewGuid()}.txt");
        try
        {
            await harness.FileSystem.WriteAsync(tempFile, "facade-test");
            var content = await harness.FileSystem.ReadAsync(tempFile);
            Assert.Equal("facade-test", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void HarnessFactory_CreatesWindowsHarness()
    {
        using var harness = HarnessFactory.Create();
        Assert.IsType<WindowsHarness>(harness);
        Assert.NotNull(harness.Shell);
    }

    [Fact]
    public void DI_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddSystemHarness();
        using var provider = services.BuildServiceProvider();

        var harness = provider.GetRequiredService<IHarness>();
        Assert.IsType<WindowsHarness>(harness);

        // Layer 1: Programmatic
        Assert.NotNull(provider.GetRequiredService<IShell>());
        Assert.NotNull(provider.GetRequiredService<IProcessManager>());
        Assert.NotNull(provider.GetRequiredService<IFileSystem>());
        Assert.NotNull(provider.GetRequiredService<IWindow>());
        Assert.NotNull(provider.GetRequiredService<IClipboard>());
        Assert.NotNull(provider.GetRequiredService<ISystemInfo>());

        // Layer 2: Vision+Action
        Assert.NotNull(provider.GetRequiredService<IScreen>());
        Assert.NotNull(provider.GetRequiredService<IMouse>());
        Assert.NotNull(provider.GetRequiredService<IKeyboard>());

        // Extended services
        Assert.NotNull(provider.GetRequiredService<IDisplay>());
        Assert.NotNull(provider.GetRequiredService<IVirtualDesktop>());
        Assert.NotNull(provider.GetRequiredService<IDialogHandler>());
        Assert.NotNull(provider.GetRequiredService<IUIAutomation>());
        Assert.NotNull(provider.GetRequiredService<IOcr>());
        Assert.NotNull(provider.GetRequiredService<ITemplateMatcher>());

        // Workflow
        Assert.NotNull(provider.GetRequiredService<IObserver>());
        Assert.NotNull(provider.GetRequiredService<IActionRecorder>());
    }

    [Fact]
    public void DI_ReturnsSharedHarnessInstance()
    {
        var services = new ServiceCollection();
        services.AddSystemHarness();
        using var provider = services.BuildServiceProvider();

        var harness1 = provider.GetRequiredService<IHarness>();
        var harness2 = provider.GetRequiredService<IHarness>();
        Assert.Same(harness1, harness2);

        // Individual services come from the same harness
        var shell = provider.GetRequiredService<IShell>();
        Assert.Same(harness1.Shell, shell);
    }

    // --- Edge cases (cycle 230) ---

    [Fact]
    public void DI_IndividualServices_FromSameHarness()
    {
        var services = new ServiceCollection();
        services.AddSystemHarness();
        using var provider = services.BuildServiceProvider();

        var harness = provider.GetRequiredService<IHarness>();

        // All DI-resolved services should come from the same harness instance
        Assert.Same(harness.Shell, provider.GetRequiredService<IShell>());
        Assert.Same(harness.Process, provider.GetRequiredService<IProcessManager>());
        Assert.Same(harness.FileSystem, provider.GetRequiredService<IFileSystem>());
        Assert.Same(harness.Window, provider.GetRequiredService<IWindow>());
        Assert.Same(harness.Display, provider.GetRequiredService<IDisplay>());
        Assert.Same(harness.Ocr, provider.GetRequiredService<IOcr>());
    }

    [Fact]
    public void DI_WithDefaultOptions_AllServicesResolvable()
    {
        var services = new ServiceCollection();
        services.AddSystemHarness(); // default overload
        using var provider = services.BuildServiceProvider();

        // Should resolve without exception
        var harness = provider.GetRequiredService<IHarness>();
        Assert.NotNull(harness);
        Assert.IsType<WindowsHarness>(harness);
    }

    // --- HarnessFactory edge cases (cycle 245) ---

    [Fact]
    public void HarnessFactory_WithOptions_PassesOptions()
    {
        var policy = new CommandPolicy().BlockProgram("test-blocked-program");
        using var harness = HarnessFactory.Create(new HarnessOptions { CommandPolicy = policy });

        Assert.IsType<WindowsHarness>(harness);
        Assert.NotNull(harness.Shell);
    }

    [Fact]
    public void HarnessFactory_WithNullOptions_CreatesDefault()
    {
        using var harness = HarnessFactory.Create(null);
        Assert.IsType<WindowsHarness>(harness);
    }

    [Fact]
    public void WindowsHarness_WithOptions_AppliesCommandPolicy()
    {
        var policy = new CommandPolicy().BlockProgram("dangerous-tool");
        using var harness = new WindowsHarness(new HarnessOptions { CommandPolicy = policy });

        Assert.NotNull(harness.Shell);
    }

    [Fact]
    public void WindowsHarness_WithAuditLog_AppliesAuditing()
    {
        var log = new InMemoryAuditLog();
        using var harness = new WindowsHarness(new HarnessOptions { AuditLog = log });

        Assert.NotNull(harness.Shell);
    }

    [Fact]
    public void DI_WithCustomOptions_ServicesStillResolve()
    {
        var services = new ServiceCollection();
        services.AddSystemHarness(new HarnessOptions
        {
            CommandPolicy = new CommandPolicy().BlockProgram("blocked"),
            AuditLog = new InMemoryAuditLog(),
        });
        using var provider = services.BuildServiceProvider();

        var harness = provider.GetRequiredService<IHarness>();
        Assert.NotNull(harness);
        Assert.NotNull(provider.GetRequiredService<IShell>());
    }

    [Fact]
    public void WindowsHarness_ImplementsIDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(WindowsHarness)));
    }
}
