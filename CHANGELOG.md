# Changelog

All notable changes to this project will be documented in this file.

## [0.29.0]

### Changed
- **Breaking** — **the MCP server's safety settings now stop actions instead of only reporting them.** Every command
  passes one gate before it runs: an active emergency stop refuses mutations (`emergency_stopped`) and cancels the
  command that is running, the rate limit refuses actions over it (`rate_limited`), and a safe zone refuses input
  actions that target anything outside the zone window (`outside_safe_zone`) — coordinates outside it, a window
  argument naming another window, keyboard input while another window is in front. A zone window that cannot be
  found refuses the action. Refusals are recorded in the action history.
  Migration: an agent that set a zone or a rate limit and kept acting outside them now gets refusals.
- **Breaking** — **the MCP server applies the default command policy**: shell commands and process starts of
  destructive programs (format, shutdown, diskpart, `rm -rf`, `del /s`, ...) are refused (`policy_blocked`).
  Migration: start the server with `--command-policy=none` to run without a policy.
- **Breaking** — **automatic updates are off by default.** The server no longer contacts GitHub or replaces its own
  binary unless started with `--auto-update=true`; without it `update.check` and `update.apply` are refused
  (`updates_disabled`). Migration: add `--auto-update=true` to keep updating automatically.
- **Breaking** — **`safety.approve` and `safety.deny` are removed**: the agent that asks for confirmation can no
  longer answer it. The user approves or denies by setting `status` in the request's JSON file; the agent polls
  with `safety.check_confirmation`. `ConfirmationManager.Approve`/`Deny` are removed too.
- **`CommandPolicy` also guards process starts.** `WindowsHarness` built with a policy wraps its process manager in
  the new `PolicyEnforcingProcessManager`, and a shell host's arguments (`cmd /c dir & shutdown /s`) are checked
  for blocked programs as well.
- `SafeZone` and `RateLimiter` are services instead of static classes.
- **The MCP server keeps its own files in one private directory** (`%TEMP%\system-harness\<pid>`): screenshots,
  clipboard images, bookmarks and confirmation requests. It is deleted when the server stops, and directories left
  by a server that did not stop cleanly are deleted at the next start. File commands cannot write, move or delete
  anything inside it (`protected_path`), so a confirmation request can only be answered by the user. Paths returned
  by earlier versions pointed directly into `%TEMP%` and were never cleaned up.

### Added
- **Operator settings for the MCP server**: `--rate-limit=N` (the agent can lower it but not raise or disable it),
  `--safe-zone=<window>` (the agent cannot change it), `--stop-hotkey=false`, `--command-policy`, `--auto-update`.
- **Emergency stop hotkey**: Ctrl+Shift+Escape stops the session until the server restarts; `safety.resume`
  resets only a stop the agent triggered itself. `EmergencyStop.Trigger(EmergencyStopSource)` and
  `EmergencyStop.TriggeredBy` say who stopped.

### Fixed
- A bookmark name containing path characters could place its snapshot outside the temp directory; caller-supplied
  names are now reduced to letters, digits, `-` and `_`.
- The MCP server reports its real version (it always said 0.27.0).

## [0.28.10]

### Fixed
- **Breaking** (released as a patch) — **cancelling a call now cancels it.** 13 method(s) that take a `CancellationToken` caught every exception to
  return a fallback (`null`, an empty result, a failure value) or to log and continue, and treated the caller's own
  cancellation the same way. They now let the caller's `OperationCanceledException` through; other failures behave
  as before. Affected: app, monitor and session tools, the auto-updater, keyboard input and the action recorder.
  Migration: code that relied on a cancelled call returning `null`, an empty result or a failure value now
  receives `OperationCanceledException` — catch it where a cancellation is expected.

## [0.28.9]

### Changed
- **`HarnessFactory.Create()` on Linux or macOS says «Windows only»** with a `PlatformNotSupportedException`, instead
  of failing to load a `SystemHarness.Linux`/`SystemHarness.Mac` assembly that never existed. A Windows app that does
  not reference `SystemHarness.Windows` now gets a `PlatformNotSupportedException` naming the package (it was a
  `FileNotFoundException`).
- README: Linux and macOS implementations are no longer listed as planned — SystemHarness is Windows only.

## [0.28.8]

### Changed
- **The packages now carry the LICENSE text**, so an application that redistributes them can ship the MIT notice
  from the package itself.

### Dependencies
- Microsoft.Extensions.AI 10.10.0, Microsoft.Extensions.AI.OpenAI 10.10.1, OpenAI 2.14.0; Microsoft.Extensions.* 10.0.12 servicing.

## [0.28.7]

### Fixed
- **Stopping a monitor now returns only after it has stopped writing.** `MonitorManager.Stop` and `Dispose`
  cancelled the monitor and returned at once, so a write that was in flight landed in the output file after the
  caller had been told the monitor was stopped - deleting or moving the file right after a stop could fail or lose
  the last event. Both now wait for the monitor to end (up to 5 seconds for one that ignores cancellation).

## [0.28.6]

### Changed
- Moved SourceLink off the line carrying a vulnerable `Microsoft.Build.Tasks.Git`
  (CVE-2026-62900) onto a patched one. Build-time tooling only — no package dependency or public
  API change. Before the move the warning was promoted to an error and the build failed outright.

## [0.28.5]

### Changed
- Updated `ModelContextProtocol` to 2.2.0 (previously 1.3.0). No public API changes — this
  package's MCP server only registers tools over stdio and does not use any of the capabilities
  the 2.0 protocol revision deprecated (roots, sampling, logging).
- Aligned transitive floors raised by that update: `Microsoft.Extensions.DependencyInjection[.
  Abstractions]`/`.Hosting` 10.0.8 → 10.0.11, `Microsoft.Extensions.AI`/`.AI.OpenAI` 10.6.0 →
  10.9.0, and (required by the latter) `OpenAI` SDK 2.10.0 → 2.12.0.

## [0.28.4]

**Gap notice — not a release entry.** Releases from 0.4.0 through 0.28.4 were published without
being recorded here. Reconstructing them now would mean describing changes from memory rather than
from a record, so the gap is marked instead of filled: for anything in that range, read the commit
history. Entries resume from the next release.

Note that the entry below is the last recorded one and is far behind the current version — do not
read it as a description of what this package currently does.

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
