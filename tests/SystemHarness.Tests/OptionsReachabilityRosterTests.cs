using System.Reflection;
using Iyu.Conventions.Testing;
using Xunit;

namespace SystemHarness.Tests;

/// <summary>
/// Every public option in this library is read by the library. An option nothing reads is a promise it does not keep:
/// a caller sets it, and nothing changes and nothing is reported. The roster fails both ways - a new unread option,
/// and a listed one that has since been wired - so each change is recorded on purpose.
/// </summary>
[Trait("Category", "CI")]
public class OptionsReachabilityRosterTests
{
    // Every assembly this repository ships: an option declared in one and read in another only counts as read
    // when both are scanned.
    private static readonly Assembly[] Libraries =
    [
        Assembly.Load("SystemHarness.Core"),
        Assembly.Load("SystemHarness.Windows"),
        Assembly.Load("SystemHarness.Apps.Office"),
        Assembly.Load("SystemHarness.Apps.Browser"),
        Assembly.Load("SystemHarness.Apps.Email"),
        Assembly.Load("SystemHarness.Mcp"),
    ];

    /// <summary>
    /// Options accepted as unread today. Shrink this list; never grow it silently.
    /// <para>
    /// Opening baseline (2026-09-20), checked by hand: each has its declaration as its only reference in src/.
    /// <c>ProcessStartOptions.MaxOutputChars</c> and <c>Timeout</c> share their names with <c>ShellOptions</c>
    /// members that the shell does honour, which is how a text search reports them as read.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string[]> KnownUnread = new()
    {
        ["SystemHarness.HarnessOptions"] = ["DefaultCaptureOptions"],
        ["SystemHarness.ProcessStartOptions"] = ["MaxOutputChars", "Timeout"],
    };

    [Fact]
    public void EveryPublicOption_IsRead() =>
        OptionsReachability.Scan(Libraries, OptionsTypes.NamedWith("Options", "Config"))
            .ShouldMatchRoster(KnownUnread);
}
