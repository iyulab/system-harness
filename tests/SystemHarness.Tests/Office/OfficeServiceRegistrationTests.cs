using Microsoft.Extensions.DependencyInjection;
using SystemHarness.Apps.Office;

namespace SystemHarness.Tests.Office;

[Trait("Category", "CI")]
public class OfficeServiceRegistrationTests
{
    [Fact]
    public void AddOfficeReaders_RegistersDocumentReader()
    {
        var services = new ServiceCollection();
        services.AddOfficeReaders();
        var provider = services.BuildServiceProvider();

        var reader = provider.GetService<IDocumentReader>();
        Assert.NotNull(reader);
        Assert.IsType<OpenXmlDocumentReader>(reader);
    }

    [Fact]
    public void AddOfficeReaders_RegistersHwpReader()
    {
        var services = new ServiceCollection();
        services.AddOfficeReaders();
        var provider = services.BuildServiceProvider();

        var reader = provider.GetService<IHwpReader>();
        Assert.NotNull(reader);
        Assert.IsType<HwpxReader>(reader);
    }

    [Fact]
    public void AddOfficeReaders_ReturnsSingletons()
    {
        var services = new ServiceCollection();
        services.AddOfficeReaders();
        var provider = services.BuildServiceProvider();

        var reader1 = provider.GetService<IDocumentReader>();
        var reader2 = provider.GetService<IDocumentReader>();
        Assert.Same(reader1, reader2);
    }

    [Fact]
    public void AddOfficeServices_RegistersAllServices()
    {
        var services = new ServiceCollection();
        // AddOfficeServices requires IHarness for OfficeApp — add a mock
        services.AddSingleton<IHarness>(new NullHarness());
        services.AddOfficeServices();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IDocumentReader>());
        Assert.NotNull(provider.GetService<IHwpReader>());
        Assert.NotNull(provider.GetService<IOfficeApp>());
    }

    /// <summary>Minimal IHarness implementation for DI testing.</summary>
    private sealed class NullHarness : IHarness
    {
        public IShell Shell => null!;
        public IProcessManager Process => null!;
        public IFileSystem FileSystem => null!;
        public IWindow Window => null!;
        public IClipboard Clipboard => null!;
        public IScreen Screen => null!;
        public IMouse Mouse => null!;
        public IKeyboard Keyboard => null!;
        public IDisplay Display => null!;
        public ISystemInfo SystemInfo => null!;
        public IVirtualDesktop VirtualDesktop => null!;
        public IDialogHandler DialogHandler => null!;
        public IUIAutomation UIAutomation => null!;
        public IOcr Ocr => null!;
        public ITemplateMatcher TemplateMatcher => null!;
        public void Dispose() { }
        public ValueTask DisposeAsync() => default;
    }
}
