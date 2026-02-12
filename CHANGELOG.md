# Changelog

All notable changes to this project will be documented in this file.

## [0.3.0-preview.1] - 2026-02-10

### Added
- **IUIAutomation** (new interface, 11 methods): accessibility tree, find elements, click/invoke/select/expand, set value — FlaUI UIA3 Windows implementation
- **IOcr** (new interface, 3 methods): `RecognizeImageAsync`, `RecognizeScreenAsync`, `RecognizeRegionAsync` — Windows.Media.Ocr implementation
- **IObserver** + `HarnessObserver`: combines screenshot, accessibility tree, and OCR into a single `Observation`
- **IActionRecorder** + `WindowsActionRecorder`: global input hook recording and replay via SharpHook
- **IScreen convenience DIMs** (+3 methods): `CaptureRegionAsync` with options, `CaptureWindowAsync` with options, `CaptureWindowRegionAsync`
- **IMouse window-relative DIMs** (+4 methods): `MoveToWindowAsync`, `ClickWindowAsync`, `DoubleClickWindowAsync`, `RightClickWindowAsync`
- **CoordinateHelpers** (6 static methods): `WindowToScreen`, `ScreenToWindow`, `Center(Rectangle/OcrWord/OcrLine/UIElement)`
- **ConvenienceHelpers** (7 static methods): `CaptureAndRecognize*Async`, `FindText*Async`, `ClickText*Async`
- **WaitHelpers**: polling utilities for workflow automation
- **SystemHarness.Apps.Browser** (new package): Playwright-based `IBrowser` interface + `PlaywrightBrowser` implementation
- **SystemHarness.Apps.Email** (new package): MailKit-based `IEmail` interface for IMAP/SMTP with OAuth2 support
- **SystemHarness.Apps.Office** (new package): `IOfficeApp` (automation) + `IDocumentReader` (OpenXML) for Word/Excel/PowerPoint
- **SystemHarness.Mcp** (new package): MCP server with 32 tools across 12 tool classes (Shell, Process, Window, Clipboard, Screen, Mouse, Keyboard, Display, System, UIAutomation, OCR, FileSystem)
- OCR types: `OcrResult`, `OcrLine`, `OcrWord`, `OcrOptions`
- UIAutomation types: `UIElement`, `UIElementCondition`, `UIControlType`
- Workflow types: `Observation`, `ObserveOptions`, `RecordedAction`, `RecordedActionType`
- `IHarness` expanded with `UIAutomation`, `Ocr` properties

### Fixed
- Screenshot memory leak in `ConvenienceHelpers.FindText*` — screenshot now disposed after OCR
- DIM fallbacks silently dropped `CaptureOptions` parameter — now throw `NotSupportedException`
- OCR captured at 1024x768 default resolution — fixed to use original resolution for accurate bounding rects
- `SoftwareBitmap` never disposed in `WindowsOcr` — added `using`
- Dead `OcrOptions.Region` property removed
- Screenshot leak in `HarnessObserver` when OCR requested without screenshot return
- Race condition in `WindowsActionRecorder.GetDelay` — protected with lock
- Hook disposal race in `WindowsActionRecorder.StopRecordingAsync` — now properly awaits hook task
- `KeyboardTools` MCP: `Enum.Parse<Key>` threw unhandled exception — changed to `TryParse` with user-friendly error

## [0.2.0-preview.1] - 2026-02-10

### Added
- **IProcessManager extensions** (+7 methods): `FindByPortAsync`, `FindByPathAsync`, `FindByWindowTitleAsync`, `GetChildProcessesAsync`, `KillTreeAsync`, `WaitForExitAsync`, `StartAsync` with `ProcessStartOptions`
- **IWindow extensions** (+10 methods): `RestoreAsync`, `HideAsync`, `ShowAsync`, `SetAlwaysOnTopAsync`, `SetOpacityAsync`, `GetForegroundAsync`, `GetStateAsync`, `WaitForWindowAsync`, `FindByProcessIdAsync`, `GetChildWindowsAsync`
- **IDisplay** (new interface, 5 methods): `GetMonitorsAsync`, `GetPrimaryMonitorAsync`, `GetMonitorAtPointAsync`, `GetMonitorForWindowAsync`, `GetVirtualScreenBoundsAsync`
- **IMouse extensions** (+5 methods): `MiddleClickAsync`, `ScrollHorizontalAsync`, `ButtonDownAsync`, `ButtonUpAsync`, `SmoothMoveAsync`
- **IKeyboard extensions** (+2 methods): `IsKeyPressedAsync`, `ToggleKeyAsync`
- **IClipboard extensions** (+5 methods): `GetHtmlAsync`, `SetHtmlAsync`, `GetFileDropListAsync`, `SetFileDropListAsync`, `GetAvailableFormatsAsync`
- **IScreen extension** (+1 method): `CaptureMonitorAsync` for per-monitor capture
- **ISystemInfo** (new interface, 6 methods): environment variables, machine name, user name, OS version
- **IVirtualDesktop** (new interface, 4 methods): virtual desktop count, switching, window movement (stub)
- **IDialogHandler** (new interface, 4 methods): dialog detection and interaction (stub)
- **IHarness** expanded from 9 to 12 service properties: `Display`, `SystemInfo`, `VirtualDesktop`, `DialogHandler`
- `ProcessStartOptions` model for advanced process launch configuration
- `WindowState` enumeration (Normal, Minimized, Maximized)
- `MonitorInfo` model with DPI, scale factor, work area
- Simulation test infrastructure (`SystemHarness.SimulationTests` project)
- `ProcessInfo` extended with `ParentPid`, `CommandLine`, `MemoryUsageBytes`, `CpuUsagePercent`

### Fixed
- Process handle leaks in `StartAsync` (both overloads) — added `using` disposal
- Process handle leaks in `KillAsync` and `KillTreeAsync` — added `using` disposal
- `GetChildProcessesAsync` was non-functional (ParentPid never populated) — now uses Toolhelp32 API
- `GetHtmlAsync` clipboard parsing could crash on malformed CF_HTML headers — now uses `TryParse`
- `SetFileDropListAsync` missing input validation — added null/empty guard
- Bare catch blocks replaced with typed exception filters across ProcessManager, Display, DxgiScreenCapturer
- `WindowsHarness.Dispose()` now checks all 12 services for IDisposable (was only disposing Screen)
- `DxgiScreenCapturer` threw `InvalidOperationException` instead of `HarnessException`

### Changed
- CsWin32 consolidation: replaced manual DllImport for `GetAsyncKeyState`, `GetKeyState`, `RegisterClipboardFormat`, `EnumClipboardFormats`, `GetClipboardFormatName`, `SetLayeredWindowAttributes`, `GetWindowLong`, `SetWindowLong`
- Default interface methods (DIM) used for backward-compatible interface extensions

## [0.1.0-preview.1] - 2026-02-10

### Added
- Core interfaces: `IShell`, `IProcessManager`, `IFileSystem`, `IWindow`, `IClipboard`, `IScreen`, `IMouse`, `IKeyboard`
- Windows implementations for all 8 interfaces
- DXGI Desktop Duplication screen capture with GDI BitBlt fallback
- DPI-aware coordinates with multi-monitor support
- Cursor overlay compositing for screenshots
- Unicode keyboard input with surrogate pair support
- `WindowsHarness` facade, `HarnessFactory`, DI extensions
- Safety: `CommandPolicy`, `EmergencyStop`, `AuditLog`
- NuGet packaging with SourceLink and symbol packages
- BenchmarkDotNet performance suite
