record Scenario(string Name, string Category, string Task);

static class Scenarios
{
    public const string SystemPrompt =
        """
        You are a computer automation agent with access to tools that control the computer.
        Execute the requested task using the provided tools. Be efficient — use the minimum number of tool calls needed.
        Report the results concisely when done. Keep your response brief (under 200 words).
        Always clean up after yourself (close apps, delete temp files).
        When closing Notepad without saving: use keyboard_hotkey with "Alt+F4", then keyboard_press with "Right", then keyboard_press with "Enter".
        If a tool returns an error, report it and move on — do not retry the same call more than once.
        When working with window_list, match windows by title substring, not by handle number.
        For UWP apps (Calculator), use window_close instead of process_kill since PIDs may differ.
        When reading large file content, report only the length or summary — do not echo the full content.
        """;

    public static readonly IReadOnlyList<Scenario> All =
    [
        // Category 1: System
        new("sys-info", "System",
            "Use system_info to get the machine name, OS version, and current username. Report all three values."),
        new("sys-env-get", "System",
            "Use system_get_env to read the PATH environment variable. Report the first 3 directories listed in it."),
        new("sys-env-roundtrip", "System",
            """Use system_set_env to set HARNESS_TEST to "demo-check-12345", then use system_get_env to read it back. Confirm the value matches."""),

        // Category 2: Shell
        new("shell-echo", "Shell",
            """Use shell_execute to run "echo SystemHarness test 12345". Report the exact output text."""),
        new("shell-dir", "Shell",
            """Use shell_execute to run "dir C:\\". Report the total number of items found."""),
        new("shell-env", "Shell",
            """Use shell_execute to run "set COMPUTERNAME". Report the computer name from the output."""),

        // Category 3: FileSystem
        new("fs-write-read", "FileSystem",
            """Use fs_write to create C:\\temp\\harness-test.txt with content "Hello from AI agent". Use fs_read to read it back. Use fs_delete to clean up."""),
        new("fs-exists", "FileSystem",
            """Use fs_exists to check if C:\\Windows\\System32\\notepad.exe exists. Report true or false."""),
        new("fs-list", "FileSystem",
            """Use fs_list to list files in C:\\temp\\ with pattern "*.txt". Report the count and file names."""),
        new("fs-copy", "FileSystem",
            """Use fs_write to create C:\\temp\\original.txt with "copy test data". Use fs_copy to copy it to C:\\temp\\copied.txt. Use fs_read to verify the copy. Use fs_delete to clean up both files."""),
        new("fs-multi-write", "FileSystem",
            """Use fs_write to create 3 files: C:\\temp\\test1.txt with "content1", C:\\temp\\test2.txt with "content2", C:\\temp\\test3.txt with "content3". Use fs_list with pattern "test*.txt" in C:\\temp\\ to confirm all 3 exist. Then use fs_delete to remove all 3."""),

        // Category 4: Clipboard
        new("clip-text", "Clipboard",
            """Use clipboard_set_text to set "SystemHarness clipboard test 2024", then use clipboard_get_text to verify it matches."""),
        new("clip-overwrite", "Clipboard",
            """Use clipboard_set_text to set "first value", then set "second value". Use clipboard_get_text to confirm it says "second value"."""),
        new("clip-html", "Clipboard",
            """Use clipboard_set_text to set "<b>bold test</b>", then use clipboard_get_html to check the HTML content. Report whether HTML format is available or only plain text."""),

        // Category 5: Process
        new("proc-list", "Process",
            """Use process_list to get all running processes. Report the total count and whether "explorer" is among them."""),
        new("proc-filter", "Process",
            """Use process_list with filter "svchost". Report the count and PID of the first one found."""),
        new("proc-start-kill", "Process",
            "Use process_start to launch notepad.exe. Use process_is_running to confirm it is running. Use process_kill with its PID. Confirm it stopped."),
        new("proc-is-running", "Process",
            """Use process_is_running to check "explorer", then check "nonexistent_app_xyz". Report both results."""),

        // Category 6: Window
        new("win-list", "Window",
            "Use window_list to get all visible windows. Report the total count and each window title."),
        new("win-focus", "Window",
            "Use window_list to find windows, then use window_focus on the first one. Report which window you focused."),
        new("win-minimize-restore", "Window",
            "Use process_start to launch notepad.exe. Use window_minimize on the Notepad window. Then use window_maximize on it. Then use window_close to close it."),
        new("win-resize", "Window",
            "Use process_start to launch notepad.exe. Use window_resize to set the Notepad window to 800x600. Then use window_close to close it. Report success."),
        new("win-multi-manage", "Window",
            """Use window_list to get all windows. Report how many are visible. Find any window with "Explorer" in the title and report its bounds."""),

        // Category 7: Display
        new("disp-monitors", "Display",
            "Use display_list_monitors to get all monitors. Report the count, which one is primary, and the resolution of each."),
        new("disp-primary", "Display",
            "Use display_get_primary to get the primary monitor info. Report its name, resolution, DPI, and scale factor."),

        // Category 8: Mouse
        new("mouse-position", "Mouse",
            "Use mouse_get_position to get the current cursor position. Report the X and Y coordinates."),
        new("mouse-move", "Mouse",
            "Use mouse_get_position, then mouse_move to (500, 500), then mouse_get_position again. Report before and after coordinates."),
        new("mouse-click", "Mouse",
            "Use display_get_primary to get the screen resolution, calculate the center, then use mouse_move to move there. Report the center coordinates."),

        // Category 9: Keyboard
        new("kb-type-notepad", "Keyboard",
            """Use process_start to launch notepad.exe. Use window_focus on the Notepad window. Use keyboard_type to type "Hello from SystemHarness AI Agent!". Then close Notepad without saving."""),
        new("kb-hotkey", "Keyboard",
            """Use process_start to launch notepad.exe. Use window_focus on it. Use keyboard_type to type "test text". Use keyboard_hotkey with "Ctrl+A" to select all. Use keyboard_press with "Delete" to clear. Then close Notepad without saving."""),

        // Category 10: Screen & OCR
        new("screen-capture", "Screen",
            "Use screen_capture to take a full desktop screenshot. Report the image dimensions, format, and file size from the metadata."),
        new("ocr-screen", "Screen",
            "Use ocr_screen to perform OCR on the full screen. Report the first 200 characters of detected text."),
        new("ocr-detailed", "Screen",
            "Use ocr_screen_detailed to perform detailed OCR. Report how many lines were detected and list the first 5 lines."),
        new("screen-window", "Screen",
            """Use process_start to launch notepad.exe. Use window_focus and keyboard_type to type "OCR test content 12345". Use screen_capture_window to capture the Notepad window. Report the image dimensions. Then close Notepad without saving."""),

        // Category 11: UI Automation
        new("uia-tree", "UIAutomation",
            "Use process_start to launch notepad.exe. Use uia_get_tree on the Notepad window with maxDepth 2. Report the top-level UI elements found. Then use window_close to close Notepad."),

        // Category 12: Edge Cases
        new("edge-fs-unicode", "EdgeCase",
            """Use fs_write to create C:\\temp\\unicode-test.txt with content "한글 テスト 中文 émojis: ✅🔥". Use fs_read to read it back. Report whether the content matches. Use fs_delete to clean up."""),
        new("edge-fs-not-found", "EdgeCase",
            """Use fs_exists to check if C:\\nonexistent\\fake-file.txt exists. Report the result (should be false)."""),
        new("edge-shell-exitcode", "EdgeCase",
            """Use shell_execute to run "exit /b 42". Report the exit code from the output."""),
        new("edge-clip-special", "EdgeCase",
            """Use clipboard_set_text to set "line1\nline2\ttab\t\"quotes\"". Use clipboard_get_text to read it back. Report the exact content."""),
        new("edge-env-empty", "EdgeCase",
            """Use system_get_env to read "NONEXISTENT_VAR_XYZ_12345". Report what the tool returns for a missing variable."""),

        // Category 13: Cross-Tool Workflows
        new("cross-notepad-type-ocr", "CrossTool",
            """Use process_start to launch notepad.exe. Use window_focus on it. Use keyboard_type to type "CrossTool OCR test 98765". Use ocr_screen to read the screen text. Report whether "98765" appears in the OCR result. Then close Notepad without saving."""),
        new("cross-file-clip", "CrossTool",
            """Use fs_write to create C:\\temp\\clip-source.txt with "clipboard roundtrip test". Use fs_read to read it. Use clipboard_set_text to set the file content. Use clipboard_get_text to verify. Use fs_delete to clean up."""),
        new("cross-proc-window", "CrossTool",
            """Use process_start to launch notepad.exe. Use window_list to find the Notepad window. Use window_resize to set it to 640x480. Use screen_capture_window to capture it. Report the captured image dimensions. Then use window_close to close Notepad."""),

        // Category 14: Stress / Boundary
        new("stress-large-file", "Stress",
            """Use fs_write to create C:\\temp\\large-test.txt with a string that repeats "ABCDEFGHIJ" 5000 times (50,000 characters total). Then use fs_read to read it back — just report the length, not the full content. Then use fs_delete to clean up. This should take exactly 3 tool calls."""),
        new("stress-deep-path", "Stress",
            """Use fs_write to create file C:\\temp\\a\\b\\c\\d\\e\\f\\g\\h\\deep-file.txt with content "deep path test". Use fs_read to read it back. Report whether content matches. Use fs_delete to clean up the file, then report success."""),
        new("stress-empty-content", "Stress",
            """Use fs_write to create C:\\temp\\empty-test.txt with an empty string "". Use fs_read to read it back. Report the content (should be empty). Use fs_delete to clean up."""),
        new("stress-shell-pipes", "Stress",
            """Use shell_execute to run "echo hello & echo world & echo done". Report all three output lines."""),
        new("stress-rapid-clipboard", "Stress",
            """Use clipboard_set_text to set "value-1", then immediately set "value-2", then immediately set "value-3". Use clipboard_get_text to read the final value. Report whether it is "value-3"."""),
        new("stress-window-resize-bounds", "Stress",
            """Use process_start to launch notepad.exe. Use window_resize to set the Notepad window to 200x100 (very small). Then use window_list to find the Notepad window and report its actual bounds. Then use window_close to close it."""),
        new("stress-mouse-extreme", "Stress",
            """Use mouse_move to move to coordinates (0, 0) — the top-left corner. Use mouse_get_position to verify. Then use mouse_move to (9999, 9999) — far beyond screen. Use mouse_get_position again. Report both positions."""),

        // Category 15: Concurrency
        new("conc-multi-notepad", "Concurrency",
            """Use process_start to launch notepad.exe three times (3 separate calls). Use window_list to confirm all three Notepad windows exist. Report the count of Notepad windows. Then use window_close to close all three."""),
        new("conc-rapid-file-ops", "Concurrency",
            """Use fs_write to create 5 files in C:\\temp\\: rapid1.txt through rapid5.txt, each with content "file N" where N is the number. Then use fs_list to list C:\\temp\\ with pattern "rapid*.txt" to confirm all 5 exist. Then use fs_delete to delete all 5 files."""),
        new("conc-clipboard-file-roundtrip", "Concurrency",
            """Use clipboard_set_text to set "step1". Use fs_write to create C:\\temp\\conc-test.txt with "step2". Use clipboard_set_text to set "step3". Use fs_read to read C:\\temp\\conc-test.txt. Use clipboard_get_text to get clipboard. Confirm clipboard is "step3" and file content is "step2". Use fs_delete to clean up."""),
        new("conc-process-enumeration", "Concurrency",
            """Use process_start to launch notepad.exe. Use process_list with filter "notepad" to find it. Use process_is_running with "notepad" to confirm. Use window_list to find the Notepad window. Report the PID from process_list and the handle from window_list. Then use window_close to close Notepad."""),

        // Category 16: Error Paths
        new("err-fs-read-missing", "ErrorPath",
            """Use fs_read to read C:\\nonexistent\\missing-file.txt. Report the error message returned by the tool."""),
        new("err-proc-kill-invalid", "ErrorPath",
            """Use process_kill with PID 9999999 (a process that almost certainly doesn't exist). Report what the tool returns."""),
        new("err-window-focus-missing", "ErrorPath",
            """Use window_focus with titleOrHandle "NonExistentWindowTitle_XYZ_12345". Report the error message."""),
        new("err-fs-delete-missing", "ErrorPath",
            """Use fs_delete on C:\\temp\\this-file-does-not-exist-xyz.txt. Report what the tool returns — does it error or succeed silently?"""),
        new("err-shell-invalid-cmd", "ErrorPath",
            """Use shell_execute to run "nonexistent_command_xyz_2024". Report the exit code and stderr message."""),

        // Category 17: Advanced Cross-Tool Workflows
        new("workflow-file-to-notepad", "Workflow",
            """Use fs_write to create C:\\temp\\workflow-input.txt with "Automation pipeline test 2024". Use process_start to launch notepad.exe. Use window_focus on Notepad. Use fs_read to read the file. Use keyboard_type to type the file content into Notepad. Then close Notepad without saving. Use fs_delete to clean up."""),
        new("workflow-env-shell-verify", "Workflow",
            """Use system_set_env to set HARNESS_WORKFLOW to "pipeline-ok". Use shell_execute to run "echo %HARNESS_WORKFLOW%". Report whether the shell output contains "pipeline-ok"."""),
        new("workflow-screenshot-to-file", "Workflow",
            """Use screen_capture to take a screenshot. Use fs_exists to verify the screenshot file was saved to the temp path reported. Report the file path and whether it exists."""),
        new("workflow-multi-window-info", "Workflow",
            """Use process_start to launch notepad.exe. Use display_get_primary to get screen resolution. Use window_list to find the Notepad window. Use window_resize to set Notepad to half the screen width and full height. Report the final window bounds. Then use window_close to close Notepad."""),

        // Category 18: Mouse Click/Scroll
        new("mouse-click-notepad", "MouseAction",
            """Use process_start to launch notepad.exe. Use window_focus on Notepad. Use window_list to find the Notepad window bounds. Use mouse_click at the center of the Notepad window area. Use keyboard_type to type "clicked here". Then close Notepad without saving."""),
        new("mouse-right-click", "MouseAction",
            """Use process_start to launch notepad.exe. Use window_focus on Notepad. Use window_list to find the Notepad window bounds. Use mouse_click with button "right" at the center of the Notepad window. Report that a right-click was performed. Then use keyboard_press with "Escape" to dismiss any context menu. Then close Notepad without saving."""),
        new("mouse-double-click", "MouseAction",
            """Use process_start to launch notepad.exe. Use window_focus on Notepad. Use keyboard_type to type "double click test word". Use window_list to find the Notepad window bounds. Use mouse_double_click at the center of the Notepad window to select a word. Report success. Then close Notepad without saving."""),
        new("mouse-scroll-notepad", "MouseAction",
            """Use process_start to launch notepad.exe. Use window_focus on Notepad. Use keyboard_type to type 20 lines of text (use "line 1\nline 2\n..." up to "line 20\n"). Use window_list to find the Notepad window bounds. Use mouse_scroll at the center of Notepad with delta -3 (scroll down). Then use mouse_scroll with delta 3 (scroll up). Report success. Then close Notepad without saving."""),

        // Category 19: Mouse Drag + Region Capture
        new("mouse-drag-notepad", "RegionOps",
            """Use process_start to launch notepad.exe. Use window_focus on Notepad. Use keyboard_type to type "drag test text here". Use window_list to find the Notepad window bounds. Calculate the text area center. Use mouse_click at the start of the text area, then use mouse_drag from the click position to 200 pixels to the right to select text. Report success. Then close Notepad without saving."""),
        new("screen-capture-region", "RegionOps",
            """Use display_get_primary to get the screen resolution. Use screen_capture_region with x=0, y=0, width=400, height=300 to capture the top-left corner region. Report the image dimensions and file size from the metadata."""),
        new("ocr-region", "RegionOps",
            """Use process_start to launch notepad.exe. Use window_focus on Notepad. Use keyboard_type to type "Region OCR 54321". Use window_list to find the Notepad window bounds. Use ocr_region with the Notepad window bounds to read text from just that region. Report whether "54321" appears in the OCR result. Then close Notepad without saving."""),

        // Category 20: UI Automation Advanced
        new("uia-find-notepad", "UIAAdvanced",
            """Use process_start to launch notepad.exe. Use uia_find on the Notepad window with controlType "Pane" to find all pane elements. Report the count and names of panes found. Then use window_close to close Notepad."""),
        new("uia-click-notepad", "UIAAdvanced",
            """Use process_start to launch notepad.exe. Use uia_get_tree on the Notepad window with maxDepth 2 to find UI elements. Then use uia_click on the Notepad window with the name of the text area element (or automationId if available). Report what was clicked. Then close Notepad without saving."""),
        new("uia-set-value", "UIAAdvanced",
            """Use process_start to launch notepad.exe. Use uia_get_tree on the Notepad window with maxDepth 3 to explore UI elements. Try to use uia_set_value on the Notepad window to set the text area value to "UIA set value test". Report whether it succeeded or what error occurred. Then close Notepad without saving."""),
        new("uia-invoke-button", "UIAAdvanced",
            """Use process_start to launch notepad.exe. Use uia_get_tree on the Notepad window with maxDepth 3 to explore all UI elements including buttons. Report the names and control types of all elements found at the top level. Then use window_close to close Notepad."""),

        // Category 21: Real-World App Automation
        new("app-calc-launch", "RealWorld",
            """Use process_start to launch calc.exe (Windows Calculator). Use window_list to find the Calculator window. Use uia_get_tree on the Calculator window with maxDepth 2 to explore its UI structure. Report the top-level element types found. Then use window_close to close Calculator."""),
        new("app-calc-buttons", "RealWorld",
            """Use process_start to launch calc.exe. Use uia_find on the Calculator window with controlType "Button" to find all buttons. Report the total count and names of the first 10 buttons found. Then use window_close to close Calculator."""),
        new("app-notepad-save-as", "RealWorld",
            """Use process_start to launch notepad.exe. Use window_focus on Notepad. Use keyboard_type to type "Save As test content". Use keyboard_hotkey with "Ctrl+Shift+S" to open Save As dialog. Use window_list to check for any new dialog windows. Report what you find. Then use keyboard_press with "Escape" to cancel the dialog. Then close Notepad without saving."""),
        new("app-notepad-full-workflow", "RealWorld",
            """Use process_start to launch notepad.exe. Use window_focus on Notepad. Use keyboard_type to type "Full workflow test 2024". Use keyboard_hotkey with "Ctrl+S" to open Save dialog. Use keyboard_type to type "C:\\temp\\notepad-workflow-test.txt" to set the filename. Use keyboard_press with "Enter" to save. Use fs_exists to verify the file was saved. Use fs_read to verify the content. Use fs_delete to clean up. Then close Notepad without saving."""),
        new("app-multi-app", "RealWorld",
            """Use process_start to launch notepad.exe. Use process_start to launch calc.exe. Use window_list to confirm both Notepad and Calculator windows exist. Report the count and titles of all windows containing "Notepad" or "Calculator". Then use window_close to close both windows."""),

        // Category 22: Shell + FileSystem Integration
        new("shell-fs-integration", "Integration",
            """Use shell_execute to run "echo integration-test > C:\\temp\\shell-output.txt". Use fs_exists to verify C:\\temp\\shell-output.txt was created. Use fs_read to read its content. Report the content. Use fs_delete to clean up."""),
        new("shell-dir-to-clipboard", "Integration",
            """Use shell_execute to run "dir C:\\temp". Use clipboard_set_text to set the first line of the output. Use clipboard_get_text to verify. Report the clipboard content."""),
        new("fs-shell-roundtrip", "Integration",
            """Use fs_write to create C:\\temp\\shell-input.txt with "echo roundtrip success". Use shell_execute to run "type C:\\temp\\shell-input.txt". Report whether the shell output contains "roundtrip success". Use fs_delete to clean up."""),
        // Category 23: Rapid Sequential (10+ tool calls per scenario)
        new("rapid-file-pipeline", "RapidSeq",
            """Create a multi-step file pipeline: Use fs_write to create C:\\temp\\step1.txt with "raw data". Use fs_read to read it. Use clipboard_set_text to set the content. Use clipboard_get_text to verify. Use fs_write to create C:\\temp\\step2.txt with the clipboard content. Use fs_read to read step2.txt. Use fs_copy to copy step2.txt to C:\\temp\\step3.txt. Use fs_read to read step3.txt. Use fs_exists to verify all 3 files exist. Finally use fs_delete on all 3 files. Report whether the data was preserved through all steps."""),
        new("rapid-notepad-workflow", "RapidSeq",
            """Use process_start to launch notepad.exe. Use window_list to find it. Use window_focus on Notepad. Use keyboard_type to type "Line 1". Use keyboard_press with "Enter". Use keyboard_type to type "Line 2". Use keyboard_press with "Enter". Use keyboard_type to type "Line 3". Use keyboard_hotkey with "Ctrl+A" to select all. Use keyboard_hotkey with "Ctrl+C" to copy. Use clipboard_get_text to read the clipboard. Report what text was copied. Then close Notepad without saving."""),
        new("rapid-system-audit", "RapidSeq",
            """Perform a system audit: Use system_info to get system details. Use system_get_env to read COMPUTERNAME. Use system_get_env to read USERNAME. Use system_get_env to read OS. Use process_list with filter "explorer". Use display_get_primary to get monitor info. Use mouse_get_position to get cursor position. Use window_list to get visible windows. Use shell_execute to run "ver". Use shell_execute to run "hostname". Report a summary of all findings."""),
        // Category 24: Error Recovery (cascading failures + cleanup)
        new("err-cascade-file", "ErrorRecovery",
            """Use fs_read to read C:\\temp\\nonexistent-cascade.txt (this will fail). Despite the error, use fs_write to create C:\\temp\\recovery.txt with "recovered". Use fs_read to verify recovery.txt was created correctly. Use fs_delete to clean up. Report whether you successfully recovered from the initial error."""),
        new("err-proc-cleanup", "ErrorRecovery",
            """Use process_start to launch notepad.exe. Use process_kill with PID 9999999 (will fail — not found). Despite the error, use window_list to find the Notepad window. Use window_close on Notepad. Use process_is_running to confirm "notepad" is no longer running. Report whether cleanup succeeded despite the earlier error."""),
        new("err-multi-tool-resilience", "ErrorRecovery",
            """Perform these operations in order, continuing despite any errors: 1) Use fs_read on C:\\nonexistent\\file.txt. 2) Use window_focus on "NonExistentWindow_XYZ". 3) Use system_get_env to read "VALID_PATH" (which won't exist). 4) Use system_info (should succeed). 5) Use shell_execute to run "echo resilience-test" (should succeed). Report which steps succeeded and which failed."""),
        // Category 25: Advanced Shell + FileSystem Integration
        new("shell-batch-create", "ShellFS",
            """Use fs_write to create C:\\temp\\test-batch.cmd with content "echo BATCH_OK > C:\\temp\\batch-output.txt". Use shell_execute to run "C:\\temp\\test-batch.cmd". Use fs_exists to verify C:\\temp\\batch-output.txt was created. Use fs_read to read it. Report whether it contains "BATCH_OK". Use fs_delete on both files."""),
        new("shell-env-to-file", "ShellFS",
            """Use system_set_env to set HARNESS_SHELL_TEST to "env-pipeline-ok". Use shell_execute to run "echo %HARNESS_SHELL_TEST%". Use fs_write to create C:\\temp\\env-output.txt with the shell output. Use fs_read to verify the file content. Report whether the round-trip preserved the value. Use fs_delete to clean up."""),
        new("shell-file-listing-analysis", "ShellFS",
            """Use fs_write to create 3 files: C:\\temp\\analysis-a.txt with "alpha", C:\\temp\\analysis-b.txt with "beta", C:\\temp\\analysis-c.txt with "gamma". Use shell_execute to run "dir /b C:\\temp\\analysis-*.txt" to list them. Use fs_list on C:\\temp\\ with pattern "analysis-*.txt" and compare the results. Report whether both methods found the same files. Use fs_delete to clean up all 3 files."""),
        // Category 26: Window Management Deep-Dive
        new("win-minimize-all-restore", "WindowDeep",
            """Use process_start to launch notepad.exe twice (2 separate calls). Use window_list to find both Notepad windows. Use window_minimize on each one. Use window_list to check they are still listed. Use window_maximize on the first one. Use window_list to see its state. Then use window_close on both. Report the window states at each step."""),
        new("win-resize-sequence", "WindowDeep",
            """Use process_start to launch notepad.exe. Find the Notepad window. Use window_resize to set it to 400x300. Then resize to 800x600. Then resize to 1200x900. Report the window bounds at each step (you can get bounds from window_list). Then close Notepad."""),
        new("win-focus-switching", "WindowDeep",
            """Use process_start to launch notepad.exe. Use process_start to launch calc.exe. Use window_list to find both. Use window_focus on Notepad. Use window_focus on Calculator. Use window_focus on Notepad again. Report which window had focus at each step. Then close both windows."""),
    ];
}
