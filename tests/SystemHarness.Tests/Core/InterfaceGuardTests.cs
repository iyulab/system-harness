using System.Reflection;

namespace SystemHarness.Tests.Core;

/// <summary>
/// Guards against API surface drift. If a method is added/removed from a core interface,
/// the corresponding test here will fail, forcing intentional updates.
/// </summary>
[Trait("Category", "CI")]
public class InterfaceGuardTests
{
    private static int MethodCount<T>() where T : class =>
        typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => !m.IsSpecialName); // Exclude property getters/setters

    [Fact] public void IHarness_PropertyCount() => Assert.Equal(15, typeof(IHarness).GetProperties().Length);
    [Fact] public void IShell_MethodCount() => Assert.Equal(2, MethodCount<IShell>());
    [Fact] public void IProcessManager_MethodCount() => Assert.Equal(12, MethodCount<IProcessManager>());
    [Fact] public void IFileSystem_MethodCount() => Assert.Equal(12, MethodCount<IFileSystem>());
    [Fact] public void IWindow_MethodCount() => Assert.Equal(17, MethodCount<IWindow>());
    [Fact] public void IClipboard_MethodCount() => Assert.Equal(9, MethodCount<IClipboard>());
    [Fact] public void IScreen_MethodCount() => Assert.Equal(7, MethodCount<IScreen>());
    [Fact] public void IMouse_MethodCount() => Assert.Equal(16, MethodCount<IMouse>());
    [Fact] public void IKeyboard_MethodCount() => Assert.Equal(7, MethodCount<IKeyboard>());
    [Fact] public void IDisplay_MethodCount() => Assert.Equal(5, MethodCount<IDisplay>());
    [Fact] public void ISystemInfo_MethodCount() => Assert.Equal(6, MethodCount<ISystemInfo>());
    [Fact] public void IVirtualDesktop_MethodCount() => Assert.Equal(4, MethodCount<IVirtualDesktop>());
    [Fact] public void IDialogHandler_MethodCount() => Assert.Equal(4, MethodCount<IDialogHandler>());
    [Fact] public void IUIAutomation_MethodCount() => Assert.Equal(10, MethodCount<IUIAutomation>());
    [Fact] public void IOcr_MethodCount() => Assert.Equal(3, MethodCount<IOcr>());
    [Fact] public void ITemplateMatcher_MethodCount() => Assert.Equal(1, MethodCount<ITemplateMatcher>());

    [Fact]
    public void CoreInterfaceCount()
    {
        // Guard: total public interfaces in SystemHarness.Core assembly
        var coreAssembly = typeof(IHarness).Assembly;
        var interfaces = coreAssembly.GetExportedTypes().Where(t => t.IsInterface).ToList();
        // IHarness + 15 service interfaces + IObserver + IActionRecorder + IAuditLog = 19
        Assert.Equal(19, interfaces.Count);
    }
}
