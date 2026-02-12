using Microsoft.Extensions.DependencyInjection;

namespace SystemHarness.Apps.Office;

/// <summary>
/// Extension methods for registering Office document services with DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Office document reader/writer services.
    /// <para>
    /// Tier 1 (file-based, no Office/HWP installation required):
    /// <list type="bullet">
    /// <item><see cref="IDocumentReader"/> — Read/write Word, Excel, PowerPoint via OpenXML</item>
    /// <item><see cref="IHwpReader"/> — Read/write HWPX (OWPML) documents</item>
    /// </list>
    /// </para>
    /// <para>
    /// Tier 2 (app-based, requires Office installed):
    /// <list type="bullet">
    /// <item><see cref="IOfficeApp"/> — Automate running Office applications (requires <see cref="IHarness"/>)</item>
    /// </list>
    /// </para>
    /// </summary>
    public static IServiceCollection AddOfficeServices(this IServiceCollection services)
    {
        // Tier 1: File-based readers (stateless, singleton-safe)
        services.AddSingleton<IDocumentReader, OpenXmlDocumentReader>();
        services.AddSingleton<IHwpReader, HwpxReader>();

        // Tier 2: App-based automation (requires IHarness)
        services.AddSingleton<IOfficeApp, OfficeApp>();

        return services;
    }

    /// <summary>
    /// Registers only file-based Office document services (Tier 1).
    /// No Office or HWP application installation required.
    /// </summary>
    public static IServiceCollection AddOfficeReaders(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentReader, OpenXmlDocumentReader>();
        services.AddSingleton<IHwpReader, HwpxReader>();
        return services;
    }
}
