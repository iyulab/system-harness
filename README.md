# system-harness

**A computer-use primitives library for .NET — eyes, hands, and shell in one harness.**

system-harness provides a unified interface for programmatic and interactive computer control. It wraps shell execution, process management, filesystem operations, screen capture, OCR, input simulation, UI automation, and document processing into a single coherent API.

No AI inside. No opinions about your agent framework. Just the primitives you need to **use** a computer — whether you're a bot or a human writing automation scripts.

## Why

AI agents need to operate computers. The "brain" (LLM) decides what to do. But it still needs:

- **Shell** — run `cmd`, `powershell`, `bash` commands and get results
- **Process** — launch, kill, and list running programs
- **FileSystem** — read, write, move, delete files and directories
- **Screen** — capture what's on screen (for vision-capable models or logging)
- **Mouse / Keyboard** — click, type, drag when there's no API and GUI is the only way
- **OCR** — read text from screen regions without requiring vision models
- **UI Automation** — interact with UI elements by accessibility tree, not pixel coordinates
- **Office Documents** — read and write Word, Excel, PowerPoint, HWP without Office installed

Today, you stitch together 5+ libraries to get all of this. system-harness is one library, one interface, three layers:

```
Layer 1: Programmatic Control (fast, precise, preferred)
  Shell, Process, FileSystem, Window, Clipboard, Display, SystemInfo

Layer 2: Vision + Action (when GUI is the only way)
  Screen, Mouse, Keyboard, OCR, UIAutomation, TemplateMatcher, DialogHandler

Layer 3: App Automation (document processing)
  Office (Word, Excel, PowerPoint, HWP) — no installation required
```

Use Layer 1 whenever possible. Fall back to Layer 2 when you must.

## Quick Start

```bash
dotnet add package SystemHarness.Core
dotnet add package SystemHarness.Windows  # Windows implementation
```

```csharp
using SystemHarness;
using SystemHarness.Windows;

using var harness = new WindowsHarness();

// Layer 1 — Programmatic
var result = await harness.Shell.RunAsync("cmd", "/C echo Hello!");
Console.WriteLine(result.StdOut);   // "Hello!\r\n"

await harness.FileSystem.WriteAsync("hello.txt", "world");
var content = await harness.FileSystem.ReadAsync("hello.txt");

await harness.Process.StartAsync("notepad.exe");
await harness.Window.FocusAsync("Notepad");

// Layer 2 — Vision + Action
var screenshot = await harness.Screen.CaptureAsync();
// screenshot.Base64, screenshot.Width, screenshot.Height, screenshot.MimeType

await harness.Mouse.ClickAsync(350, 200);
await harness.Keyboard.TypeAsync("Hello World");
await harness.Keyboard.HotkeyAsync(default, Key.Ctrl, Key.S);

// OCR — read text from screen
var ocrResult = await harness.Ocr.RecognizeScreenAsync();
Console.WriteLine(ocrResult.Text);

// UI Automation — interact with elements by name
var tree = await harness.UIAutomation.GetTreeAsync("Notepad");
await harness.UIAutomation.TypeIntoAsync("Notepad", "Edit", "Hello from automation");
```

## MCP Server (AI Tool Integration)

system-harness includes a built-in [Model Context Protocol](https://modelcontextprotocol.io/) server with **172 commands** across 24 categories, accessed through 3 MCP tools using a command dispatch pattern.

```json
{
  "mcpServers": {
    "system-harness": {
      "command": "dotnet",
      "args": ["run", "--project", "src/SystemHarness.Mcp"]
    }
  }
}
```

### 3 MCP Tools

Instead of 172 individual tool definitions (which consume ~12,000 tokens per API call), commands are accessed through 3 dispatch tools:

| Tool | Purpose | Example |
|------|---------|---------|
| `help(topic?)` | Discover commands | `help()`, `help("mouse")`, `help("mouse.click")` |
| `do(command, params?)` | Execute mutations | `do("mouse.click", '{"x":100,"y":200}')` |
| `get(command, params?)` | Execute queries | `get("window.list")` |

### Command Categories

| Category | Commands | Examples |
|----------|----------|---------|
| **shell** | 1 | `shell.execute` |
| **process** | 14 | `process.start`, `process.list`, `process.find_by_port` |
| **file** | 13 | `file.read`, `file.write`, `file.read_bytes`, `file.hash` |
| **window** | 19 | `window.list`, `window.focus`, `window.wait`, `window.set_opacity` |
| **app / dialog** | 7 | `app.open`, `app.close`, `dialog.check`, `dialog.click` |
| **screen** | 5 | `screen.capture`, `screen.capture_region`, `screen.capture_monitor` |
| **mouse** | 11 | `mouse.click`, `mouse.drag`, `mouse.smooth_move` |
| **keyboard** | 8 | `keyboard.type`, `keyboard.press`, `keyboard.hotkey` |
| **ocr** | 4 | `ocr.read`, `ocr.read_region`, `ocr.read_detailed` |
| **vision** | 10 | `vision.click_text`, `vision.wait_text`, `vision.find_image` |
| **ui** | 15 | `ui.get_tree`, `ui.find`, `ui.click`, `ui.select`, `ui.annotate` |
| **clipboard** | 9 | `clipboard.get_text`, `clipboard.set_text`, `clipboard.set_html` |
| **display** | 5 | `display.list`, `display.get_primary`, `display.get_at_point` |
| **coord** | 4 | `coord.to_absolute`, `coord.to_relative`, `coord.scale_info` |
| **desktop** | 4 | `desktop.count`, `desktop.current`, `desktop.switch` |
| **system** | 4 | `system.get_info`, `system.get_env`, `system.set_env` |
| **office** | 10 | `office.read_word`, `office.write_excel`, `office.read_hwpx` |
| **safety** | 12 | `safety.emergency_stop`, `safety.set_zone`, `safety.confirm_before` |
| **monitor** | 4 | `monitor.start`, `monitor.stop`, `monitor.read`, `monitor.list` |
| **report** | 3 | `report.get_desktop`, `report.get_screen`, `report.get_window` |
| **session** | 5 | `session.save`, `session.compare`, `session.bookmark` |
| **observe** | 1 | `observe.window` (hybrid screenshot + accessibility + OCR) |
| **record** | 4 | `record.start`, `record.stop`, `record.get_actions`, `record.replay` |

### Compound Facades (reduce multiple tool calls to one)

| Command | What it does | Calls saved |
|---------|-------------|-------------|
| `vision.click_text` | OCR + find text + click center | 3 &rarr; 1 |
| `vision.click_and_verify` | Screenshot + click + screenshot + compare | 4 &rarr; 1 |
| `app.open` | Start process + wait for window | 2 &rarr; 1 |
| `app.close` | Close window + handle dialog + wait for exit | 3 &rarr; 1 |
| `report.get_screen` | Screenshot + OCR + UI elements | 3 &rarr; 1 |
| `observe.window` | Screenshot + accessibility tree + OCR | 3 &rarr; 1 |
| `ui.select_menu` | Navigate menu path like "File > Save As" | N &rarr; 1 |

## Office Documents

Read and write Microsoft Office and Korean HWP documents **without Office installed** — uses OpenXML and OWPML directly.

```csharp
using SystemHarness.Apps.Office;

// Register in DI
services.AddOfficeReaders();

// Read documents to markdown
var wordContent = await documentReader.ReadWordAsync("report.docx");
var excelContent = await documentReader.ReadExcelAsync("data.xlsx");
var pptxContent = await documentReader.ReadPowerPointAsync("slides.pptx");
var hwpContent = await hwpReader.ReadHwpxAsync("document.hwpx");

// Write documents from structured content
await documentReader.WriteWordAsync("output.docx", documentContent);
await documentReader.WriteExcelAsync("output.xlsx", spreadsheetContent);

// Find and replace
await documentReader.FindReplaceWordAsync("template.docx", new() { { "{{name}}", "John" } });
```

## Safety Features

```csharp
using var harness = new WindowsHarness(new HarnessOptions
{
    // Block dangerous commands (format, shutdown, rm -rf, etc.)
    CommandPolicy = CommandPolicy.CreateDefault(),

    // Record all actions for auditing
    AuditLog = new InMemoryAuditLog(),
});

// This throws CommandPolicyException:
await harness.Shell.RunAsync("format", "C: /FS:NTFS");
```

### Emergency Stop

```csharp
var stop = new EmergencyStop();
// Pass stop.Token to any CancellationToken parameter
// Call stop.Trigger() to cancel all operations at once
```

### Safe Zones, Rate Limiting, and Confirmation

Available through MCP tools or programmatically:

- **Safe zones** — restrict mouse/keyboard to a window or screen region
- **Rate limiting** — cap actions per second to prevent runaway automation
- **Confirmation gates** — require user approval before destructive actions
- **Action history** — full audit trail of all tool invocations

## Monitoring

Background monitors track system changes in real-time, writing events to JSONL files:

| Monitor Type | Watches |
|-------------|---------|
| **file** | File/directory create, modify, delete, rename |
| **process** | Process start and exit events |
| **window** | Window create, close, focus change, title change |
| **clipboard** | Clipboard content changes |
| **screen** | Visual changes with periodic snapshots |
| **dialog** | Dialog/popup window appearances |

## Dependency Injection

```csharp
services.AddSystemHarness(); // default options
// or
services.AddSystemHarness(new HarnessOptions
{
    CommandPolicy = CommandPolicy.CreateDefault(),
});

// Inject IHarness or individual services:
public class MyService(IShell shell, IScreen screen, IOcr ocr) { }
```

## Platform Factory

```csharp
// Auto-detect platform at runtime
using var harness = HarnessFactory.Create();
```

## Architecture

```
SystemHarness.Core              Interfaces + models (zero platform dependencies)
  |
  +-- SystemHarness.Windows     Win32/DXGI/SendInput/FlaUI (Windows implementation)
  +-- SystemHarness.Linux       X11/Wayland (planned)
  +-- SystemHarness.Mac         AppKit/AppleScript (planned)
  |
  +-- SystemHarness.Apps.Office OpenXML/OWPML document processing
  +-- SystemHarness.Apps.Email  IMAP/SMTP via MailKit
  +-- SystemHarness.Apps.Browser Playwright-based web automation
  |
  +-- SystemHarness.Mcp         MCP server (3 tools, 172 commands)
```

### IHarness Services (15 interfaces)

| Service | Layer | Purpose |
|---------|-------|---------|
| `IShell` | 1 | Execute shell commands |
| `IProcessManager` | 1 | Start, stop, list processes |
| `IFileSystem` | 1 | Read, write, list, delete files |
| `IWindow` | 1 | Focus, resize, move, close windows |
| `IClipboard` | 1 | Text, HTML, image, file drop clipboard |
| `IDisplay` | 1 | Monitor enumeration, DPI, bounds |
| `ISystemInfo` | 1 | Environment variables, OS info |
| `IVirtualDesktop` | 1 | Virtual desktop management |
| `IScreen` | 2 | Full-screen and region capture |
| `IMouse` | 2 | Click, drag, scroll, move |
| `IKeyboard` | 2 | Type, press, hotkey, key state |
| `IOcr` | 2 | Screen and image text recognition |
| `ITemplateMatcher` | 2 | Find template images on screen |
| `IUIAutomation` | 2 | Accessibility tree navigation |
| `IDialogHandler` | 2 | System dialog interaction |

### Windows Implementation Details

| Capability | Technology |
|---|---|
| Shell | cmd.exe / powershell via `Process.Start` |
| Process | `System.Diagnostics.Process` + Toolhelp32 for child processes |
| Screen | DXGI Desktop Duplication (GPU) with GDI BitBlt fallback |
| Mouse/KB | Win32 `SendInput` with Unicode surrogate pair support |
| Window | `EnumWindows`, `SetForegroundWindow`, `MoveWindow`, `SetLayeredWindowAttributes` |
| Clipboard | Win32 Clipboard API — text, image, HTML (CF_HTML), file drop (CF_HDROP) |
| Display | `EnumDisplayMonitors`, `GetDpiForMonitor`, per-monitor capture |
| OCR | Windows.Media.Ocr (built-in Windows OCR engine) |
| UI Automation | FlaUI / UIA3 |
| Template Matching | Normalized Cross-Correlation (NCC) via SkiaSharp |
| DPI | Per-Monitor DPI V2 awareness, virtual desktop coordinates |
| Cursor | GDI cursor overlay with hotspot-adjusted `DrawIconEx` |

## Design Principles

- **Zero AI dependency** — no LLM SDKs, no model opinions, no agent loops
- **AI-ready** — Screenshot returns `Base64` for vision APIs; ShellResult is structured
- **Layer 1 first** — programmatic control is always preferred; vision+action is the fallback
- **One interface, multiple backends** — same `IHarness` across all platforms
- **Async-first** — every operation is `Task`-based
- **No magic** — thin wrappers over OS primitives, not a framework
- **Safety built-in** — command policy, audit logging, emergency stop, safe zones

## Roadmap

- [x] Core interfaces and models (15 services)
- [x] Windows Layer 1: Shell, Process, FileSystem, Window, Clipboard, Display, SystemInfo
- [x] Windows Layer 2: Screen (DXGI+GDI), Mouse, Keyboard, OCR, UIAutomation
- [x] Template matching (NCC-based image search)
- [x] Compound facades: vision_click_text, app_open/close, report_get_screen
- [x] Smart waiting: vision_wait_text, ui_wait_element, window_wait, vision_wait_change
- [x] Action verification: vision_click_and_verify, vision_type_and_verify
- [x] Background monitors: file, process, window, clipboard, screen, dialog
- [x] Safety: EmergencyStop, safe zones, rate limiting, confirmation gates
- [x] Session management: save, compare, bookmark
- [x] MCP server with 172 commands (3-tool dispatch architecture)
- [x] Office document processing (Word, Excel, PowerPoint, HWP)
- [x] DPI-aware coordinates, Unicode support, cursor overlay
- [x] NuGet packaging with SourceLink
- [ ] Linux implementation (X11/Wayland)
- [ ] macOS implementation (AppKit/AppleScript)

## License

MIT
