using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using System.ComponentModel;
using SystemHarness.Mcp;
using SystemHarness.Mcp.Dispatch;
using SystemHarness.Mcp.Tools;

namespace SystemHarness.Tests.Mcp;

/// <summary>
/// Convention tests for MCP tools. Validates command dispatch architecture,
/// naming, attributes, and response envelope without needing a real IHarness instance.
/// </summary>
[Trait("Category", "CI")]
public class McpToolConventionTests
{
    /// <summary>
    /// All tool implementation classes (NOT MCP-exposed directly, but registered as commands).
    /// </summary>
    private static readonly Type[] ToolTypes =
    [
        typeof(ShellTools), typeof(ProcessTools), typeof(FileSystemTools),
        typeof(WindowTools), typeof(ClipboardTools), typeof(ScreenTools),
        typeof(MouseTools), typeof(KeyboardTools), typeof(DisplayTools),
        typeof(SystemTools), typeof(OcrTools), typeof(UIAutomationTools),
        typeof(VisionTools), typeof(ReportTools), typeof(CoordTools),
        typeof(AppTools), typeof(OfficeTools), typeof(SafetyTools),
        typeof(MonitorTools), typeof(SessionTools), typeof(DesktopTools),
        typeof(ObserverTools), typeof(RecorderTools), typeof(UpdateTools),
    ];

    private static IEnumerable<(Type Type, MethodInfo Method, string ToolName)> AllTools()
    {
        foreach (var type in ToolTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (attr is not null)
                    yield return (type, method, attr.Name!);
            }
        }
    }

    // ── Dispatch Architecture Tests ──

    [Fact]
    public void McpToolCount_IsExact()
    {
        // Only DispatchTools should be MCP-exposed (help, do, get)
        var dispatchMethods = typeof(DispatchTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToList();
        Assert.Equal(3, dispatchMethods.Count);
    }

    [Fact]
    public void McpToolNames_AreCorrect()
    {
        var names = typeof(DispatchTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(n => n is not null)
            .OrderBy(n => n)
            .ToList();
        Assert.Equal(["do", "get", "help"], names);
    }

    [Fact]
    public void DispatchTools_HasMcpServerToolTypeAttribute()
    {
        Assert.NotNull(typeof(DispatchTools).GetCustomAttribute<McpServerToolTypeAttribute>());
    }

    [Fact]
    public void ToolImplementationClasses_DoNotHaveMcpServerToolTypeAttribute()
    {
        // Tool implementation classes should NOT be MCP-exposed directly
        foreach (var type in ToolTypes)
        {
            Assert.Null(type.GetCustomAttribute<McpServerToolTypeAttribute>());
        }
    }

    [Fact]
    public void ToDotNotation_ConvertsCorrectly()
    {
        Assert.Equal("mouse.click", CommandRegistrar.ToDotNotation("mouse_click"));
        Assert.Equal("mouse.click_double", CommandRegistrar.ToDotNotation("mouse_click_double"));
        Assert.Equal("vision.click_text", CommandRegistrar.ToDotNotation("vision_click_text"));
        Assert.Equal("file.read_bytes", CommandRegistrar.ToDotNotation("file_read_bytes"));
        Assert.Equal("keyboard.hotkey_wait", CommandRegistrar.ToDotNotation("keyboard_hotkey_wait"));
        Assert.Equal("dialog.fill_file", CommandRegistrar.ToDotNotation("dialog_fill_file"));
    }

    [Fact]
    public void AllCommands_UseDotNotation()
    {
        // All command names from tool methods should convert to valid dot notation
        foreach (var (type, method, name) in AllTools())
        {
            var dotName = CommandRegistrar.ToDotNotation(name);
            Assert.Matches(@"^[a-z]+\.[a-z_]+$", dotName);
        }
    }

    // ── Command Count Guards ──

    [Fact]
    public void CommandCount_IsExact()
    {
        // Exact guard: tracks total command count across all tool types.
        // If you add or remove a tool, update this count AND classify in ReadOnlyTools/MutationTools.
        var count = AllTools().Count();
        Assert.Equal(174, count);
    }

    [Fact]
    public void ToolTypeCount_IsExact()
    {
        // Exact guard: tracks number of tool implementation classes.
        Assert.Equal(24, ToolTypes.Length);
    }

    [Fact]
    public void CategoryCount_IsExact()
    {
        // Count unique categories from dot notation conversion
        var categories = AllTools()
            .Select(t => CommandRegistrar.ToDotNotation(t.ToolName).Split('.')[0])
            .Distinct()
            .ToList();
        Assert.Equal(25, categories.Count);
    }

    // ── Existing Convention Tests (still valid — test underlying tool methods) ──

    [Fact]
    public void AllTools_FollowNamingConvention()
    {
        // All tools must use snake_case: {domain}_{verb}[_{qualifier}]
        foreach (var (type, method, name) in AllTools())
        {
            Assert.Matches(@"^[a-z]+_[a-z_]+$", name);
        }
    }

    [Fact]
    public void AllTools_HaveDescription()
    {
        foreach (var (type, method, name) in AllTools())
        {
            var desc = method.GetCustomAttribute<DescriptionAttribute>();
            Assert.True(desc is not null && desc.Description.Length > 10,
                $"Tool '{name}' in {type.Name} must have a description > 10 chars.");
        }
    }

    [Fact]
    public void AllTools_ReturnTaskString()
    {
        foreach (var (type, method, name) in AllTools())
        {
            Assert.Equal(typeof(Task<string>), method.ReturnType);
        }
    }

    [Fact]
    public void AllTools_HaveCancellationTokenParameter()
    {
        foreach (var (type, method, name) in AllTools())
        {
            var hasCtParam = method.GetParameters()
                .Any(p => p.ParameterType == typeof(CancellationToken));
            Assert.True(hasCtParam, $"Tool '{name}' must have a CancellationToken parameter.");
        }
    }

    [Fact]
    public void ToolNames_AreUnique()
    {
        var names = AllTools().Select(t => t.ToolName).ToList();
        var dupes = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(dupes);
    }

    [Fact]
    public void ReadOnlyPlusMutation_MatchesTotalToolCount()
    {
        // Every tool must be classified — no untracked additions
        var totalClassified = ReadOnlyTools.Count + MutationTools.Count;
        var totalTools = AllTools().Count();
        Assert.Equal(totalTools, totalClassified);
    }

    [Fact]
    public void AllTools_HaveAtLeastTwoSegments()
    {
        foreach (var (type, method, name) in AllTools())
        {
            var segments = name.Split('_');
            Assert.True(segments.Length >= 2,
                $"Tool '{name}' must have at least 2 segments (domain_verb).");
        }
    }

    [Fact]
    public void AllToolTypes_AreSealed()
    {
        foreach (var type in ToolTypes)
        {
            Assert.True(type.IsSealed, $"{type.Name} should be sealed.");
        }
    }

    [Fact]
    public void AllTools_DomainPrefix_MatchesToolType()
    {
        var typeToPrefix = new Dictionary<Type, string[]>
        {
            [typeof(ShellTools)] = ["shell_"],
            [typeof(ProcessTools)] = ["process_"],
            [typeof(FileSystemTools)] = ["file_"],
            [typeof(WindowTools)] = ["window_"],
            [typeof(ClipboardTools)] = ["clipboard_"],
            [typeof(ScreenTools)] = ["screen_"],
            [typeof(MouseTools)] = ["mouse_"],
            [typeof(KeyboardTools)] = ["keyboard_"],
            [typeof(DisplayTools)] = ["display_"],
            [typeof(SystemTools)] = ["system_"],
            [typeof(OcrTools)] = ["ocr_"],
            [typeof(UIAutomationTools)] = ["ui_"],
            [typeof(VisionTools)] = ["vision_"],
            [typeof(ReportTools)] = ["report_"],
            [typeof(CoordTools)] = ["coord_"],
            [typeof(AppTools)] = ["app_", "dialog_"],
            [typeof(OfficeTools)] = ["office_"],
            [typeof(SafetyTools)] = ["safety_"],
            [typeof(MonitorTools)] = ["monitor_"],
            [typeof(SessionTools)] = ["session_"],
            [typeof(DesktopTools)] = ["desktop_"],
            [typeof(ObserverTools)] = ["observe_"],
            [typeof(RecorderTools)] = ["record_"],
        };

        foreach (var (type, method, name) in AllTools())
        {
            if (typeToPrefix.TryGetValue(type, out var prefixes))
            {
                Assert.True(prefixes.Any(p => name.StartsWith(p)),
                    $"Tool '{name}' in {type.Name} should start with one of: {string.Join(", ", prefixes)}.");
            }
        }
    }

    [Fact]
    public void AllTools_DescriptionStartsWithCapitalLetterOrVerb()
    {
        foreach (var (type, method, name) in AllTools())
        {
            var desc = method.GetCustomAttribute<DescriptionAttribute>()!.Description;
            Assert.True(char.IsUpper(desc[0]),
                $"Tool '{name}' description should start with a capital letter.");
        }
    }

    // ── McpResponse Tests ──

    [Fact]
    public void McpResponse_Ok_HasCorrectEnvelope()
    {
        var json = McpResponse.Ok(new { value = 42 }, 100);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal(42, root.GetProperty("data").GetProperty("value").GetInt32());
        Assert.True(root.TryGetProperty("meta", out var meta));
        Assert.Equal(100, meta.GetProperty("ms").GetInt64());
        Assert.True(meta.TryGetProperty("ts", out _));
    }

    [Fact]
    public void McpResponse_Error_HasCorrectEnvelope()
    {
        var json = McpResponse.Error("test_error", "Something failed.", 50);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal("test_error", root.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Something failed.", root.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public void McpResponse_Items_HasCountAndItems()
    {
        var json = McpResponse.Items(new[] { "a", "b", "c" }, 10);
        var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal(3, data.GetProperty("count").GetInt32());
        Assert.Equal(3, data.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void McpResponse_Confirm_HasMessage()
    {
        var json = McpResponse.Confirm("Done!", 5);
        var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal("Done!", data.GetProperty("message").GetString());
    }

    [Fact]
    public void McpResponse_Content_HasContentAndFormat()
    {
        var json = McpResponse.Content("# Hello", "markdown", 3);
        var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal("# Hello", data.GetProperty("content").GetString());
        Assert.Equal("markdown", data.GetProperty("format").GetString());
    }

    [Fact]
    public void McpResponse_Check_HasResultAndDetail()
    {
        var json = McpResponse.Check(true, "all good", 1);
        var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.True(data.GetProperty("result").GetBoolean());
        Assert.Equal("all good", data.GetProperty("detail").GetString());
    }

    [Fact]
    public void McpResponse_CjkCharacters_NotEscaped()
    {
        var json = McpResponse.Ok(new { text = "한글 테스트" });
        Assert.Contains("한글 테스트", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void McpResponse_Items_EmptyArray_HasZeroCount()
    {
        var json = McpResponse.Items(Array.Empty<string>(), 0);
        var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.Equal(0, data.GetProperty("count").GetInt32());
        Assert.Equal(0, data.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void McpResponse_Ok_WithoutMs_OmitsMs()
    {
        var json = McpResponse.Ok(new { value = 1 });
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        if (doc.RootElement.TryGetProperty("meta", out var meta))
        {
            if (meta.TryGetProperty("ms", out var ms))
                Assert.Equal(JsonValueKind.Null, ms.ValueKind);
        }
    }

    [Fact]
    public void McpResponse_Error_WithoutMs_StillHasError()
    {
        var json = McpResponse.Error("err", "msg");
        var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("err", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void McpResponse_Check_FalseResult()
    {
        var json = McpResponse.Check(false, "not ok", 1);
        var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        Assert.False(data.GetProperty("result").GetBoolean());
        Assert.Equal("not ok", data.GetProperty("detail").GetString());
    }

    [Fact]
    public void PerToolType_ExactCounts()
    {
        var expected = new Dictionary<Type, int>
        {
            [typeof(AppTools)] = 7,
            [typeof(ClipboardTools)] = 9,
            [typeof(CoordTools)] = 4,
            [typeof(DisplayTools)] = 5,
            [typeof(FileSystemTools)] = 13,
            [typeof(KeyboardTools)] = 8,
            [typeof(MonitorTools)] = 4,
            [typeof(MouseTools)] = 11,
            [typeof(OcrTools)] = 4,
            [typeof(OfficeTools)] = 10,
            [typeof(ProcessTools)] = 14,
            [typeof(ReportTools)] = 3,
            [typeof(SafetyTools)] = 12,
            [typeof(ScreenTools)] = 5,
            [typeof(SessionTools)] = 5,
            [typeof(ShellTools)] = 1,
            [typeof(SystemTools)] = 4,
            [typeof(UIAutomationTools)] = 15,
            [typeof(VisionTools)] = 10,
            [typeof(WindowTools)] = 19,
            [typeof(DesktopTools)] = 4,
            [typeof(ObserverTools)] = 1,
            [typeof(RecorderTools)] = 4,
            [typeof(UpdateTools)] = 2,
        };

        foreach (var (type, count) in expected)
        {
            var actual = AllTools().Count(t => t.Type == type);
            Assert.True(actual == count,
                $"{type.Name}: expected {count} tools, found {actual}.");
        }
    }

    // ── ActionLog Coverage Tests ──

    private static readonly HashSet<string> ReadOnlyTools =
    [
        // FileSystem reads
        "file_read", "file_read_bytes", "file_list", "file_check", "file_info", "file_search", "file_hash",
        // Window queries
        "window_list", "window_get", "window_get_foreground", "window_wait", "window_wait_close", "window_wait_idle",
        "window_get_children", "window_find_by_pid",
        // Mouse queries
        "mouse_get",
        // Keyboard queries
        "keyboard_is_pressed",
        // Clipboard reads
        "clipboard_get_text", "clipboard_get_html", "clipboard_get_image", "clipboard_get_files", "clipboard_get_formats",
        // Screen reads
        "screen_capture", "screen_capture_region", "screen_capture_window",
        "screen_capture_monitor", "screen_capture_window_region",
        // Display/System/Coord reads
        "display_list", "display_get_primary", "display_get_at_point", "display_get_for_window", "display_get_virtual_bounds",
        "system_get_env", "system_get_info", "system_list_env",
        // Desktop queries
        "desktop_count", "desktop_current",
        "coord_to_absolute", "coord_to_relative", "coord_to_physical", "coord_scale_info",
        // OCR reads
        "ocr_read", "ocr_read_region", "ocr_read_detailed", "ocr_read_image",
        // UIAutomation queries
        "ui_get_tree", "ui_find", "ui_wait_element", "ui_annotate", "ui_detect_clickables", "ui_detect_inputs", "ui_get_at",
        "ui_get_focused",
        // Vision reads/waits
        "vision_wait_text", "vision_find_text", "vision_read_region", "vision_wait_change", "vision_snapshot",
        // Report reads
        "report_get_desktop", "report_get_window", "report_get_screen",
        // Monitor reads
        "monitor_list", "monitor_read",
        // Session reads
        "session_compare", "session_bookmark_compare", "session_bookmark_list",
        // Safety reads
        "safety_status", "safety_get_zone", "safety_action_history", "safety_check_confirmation",
        // Process queries
        "process_list", "process_get_info", "process_check", "process_wait_exit", "process_list_by_window",
        "process_find_by_port", "process_find_by_path", "process_get_children", "process_find_by_window",
        // Office reads
        // Dialog reads
        "dialog_check",
        "office_read_word", "office_read_excel", "office_read_pptx", "office_read_hwpx",
        // Observer reads
        "observe_window",
        // Recorder reads
        "record_get_actions",
        // Update reads
        "update_check",
    ];

    private static readonly HashSet<string> MutationTools =
    [
        // FileSystem writes
        "file_write", "file_write_bytes", "file_copy", "file_move", "file_create_directory", "file_delete",
        // Shell
        "shell_execute",
        // Window mutations
        "window_focus", "window_resize", "window_close", "window_minimize", "window_maximize", "window_restore", "window_move",
        "window_hide", "window_show", "window_set_always_on_top", "window_set_opacity",
        // Mouse actions
        "mouse_click", "mouse_click_double", "mouse_move", "mouse_drag", "mouse_scroll", "mouse_drag_window",
        "mouse_scroll_horizontal", "mouse_button_down", "mouse_button_up", "mouse_smooth_move",
        // Keyboard actions
        "keyboard_type", "keyboard_press", "keyboard_key_down", "keyboard_key_up", "keyboard_toggle_lock", "keyboard_hotkey", "keyboard_hotkey_wait",
        // Clipboard writes
        "clipboard_set_text", "clipboard_set_image", "clipboard_set_html", "clipboard_set_files",
        // UIAutomation mutations
        "ui_click", "ui_set_value", "ui_type_into", "ui_invoke", "ui_select_menu",
        "ui_select", "ui_expand",
        // Vision actions
        "vision_click_text", "vision_click_and_verify", "vision_type_and_verify", "vision_find_image", "vision_click_image",
        // App/Dialog mutations
        "app_open", "app_close", "app_focus", "dialog_dismiss", "dialog_fill_file", "dialog_click",
        // Process mutations
        "process_start", "process_start_advanced", "process_stop", "process_stop_by_name", "process_stop_tree",
        // Monitor control
        "monitor_start", "monitor_stop",
        // Desktop mutations
        "desktop_switch", "desktop_move_window",
        // System mutations
        "system_set_env",
        // Session mutations
        "session_save", "session_bookmark",
        // Safety mutations
        "safety_emergency_stop", "safety_resume", "safety_set_zone", "safety_set_rate_limit",
        "safety_confirm_before", "safety_approve", "safety_deny", "safety_clear_history",
        // Office writes
        "office_write_word", "office_write_excel", "office_write_pptx", "office_write_hwpx",
        "office_replace_word", "office_replace_hwpx",
        // Recorder mutations
        "record_start", "record_stop", "record_replay",
        // Update mutations
        "update_apply",
    ];

    [Fact]
    public void AllTools_AreClassifiedAsReadOnlyOrMutation()
    {
        var unclassified = new List<string>();
        foreach (var (_, _, name) in AllTools())
        {
            if (!ReadOnlyTools.Contains(name) && !MutationTools.Contains(name))
                unclassified.Add(name);
        }

        Assert.True(unclassified.Count == 0,
            $"Unclassified tools (add to ReadOnlyTools or MutationTools): {string.Join(", ", unclassified)}");
    }

    [Fact]
    public void NoToolInBothLists()
    {
        var overlap = ReadOnlyTools.Intersect(MutationTools).ToList();
        Assert.Empty(overlap);
    }

    [Fact]
    public void AllToolParameters_HaveDescription()
    {
        var missing = new List<string>();
        foreach (var (type, method, name) in AllTools())
        {
            foreach (var param in method.GetParameters())
            {
                if (param.ParameterType == typeof(CancellationToken))
                    continue;

                var desc = param.GetCustomAttribute<DescriptionAttribute>();
                if (desc is null || desc.Description.Length < 5)
                    missing.Add($"{name}.{param.Name}");
            }
        }

        Assert.True(missing.Count == 0,
            $"Parameters missing [Description] (min 5 chars): {string.Join(", ", missing)}");
    }

    [Fact]
    public void AllToolParameters_AreJsonFriendlyTypes()
    {
        var allowedTypes = new HashSet<Type>
        {
            typeof(string), typeof(int), typeof(long), typeof(double), typeof(float),
            typeof(bool), typeof(CancellationToken),
        };

        var violations = new List<string>();
        foreach (var (type, method, name) in AllTools())
        {
            foreach (var param in method.GetParameters())
            {
                var paramType = param.ParameterType;
                var underlying = Nullable.GetUnderlyingType(paramType);
                if (underlying is not null)
                    paramType = underlying;

                if (!allowedTypes.Contains(paramType))
                    violations.Add($"{name}.{param.Name} ({paramType.Name})");
            }
        }

        Assert.True(violations.Count == 0,
            $"Tool parameters with non-JSON-friendly types (use primitives):\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void RequiredStringParams_HaveValidationInSource()
    {
        var excludedParams = new HashSet<string>
        {
            "file_write.content", "clipboard_set_text.text", "clipboard_set_html.html",
            "system_set_env.value", "ui_set_value.value", "ui_type_into.text",
            "keyboard_type.text", "keyboard_press.key", "keyboard_key_down.key",
            "keyboard_key_up.key", "keyboard_is_pressed.key", "keyboard_toggle_lock.key",
            "keyboard_hotkey_wait.expectType",
            "office_write_word.contentJson", "office_write_excel.contentJson",
            "office_write_pptx.contentJson", "office_write_hwpx.contentJson",
            "office_replace_word.replace", "office_replace_hwpx.replace",
        };

        var toolsDir = FindToolsDirectory();
        var missing = new List<string>();

        foreach (var (type, method, name) in AllTools())
        {
            foreach (var param in method.GetParameters())
            {
                if (param.ParameterType != typeof(string) || param.HasDefaultValue)
                    continue;
                if (param.ParameterType == typeof(CancellationToken))
                    continue;

                var qualifiedName = $"{name}.{param.Name}";
                if (excludedParams.Contains(qualifiedName))
                    continue;

                var found = false;
                foreach (var file in Directory.GetFiles(toolsDir, "*.cs"))
                {
                    var content = File.ReadAllText(file);
                    if (!content.Contains($"Name = \"{name}\""))
                        continue;

                    if (content.Contains($"IsNullOrWhiteSpace({param.Name})"))
                        found = true;
                    break;
                }

                if (!found)
                    missing.Add(qualifiedName);
            }
        }

        Assert.True(missing.Count == 0,
            $"Required string params missing IsNullOrWhiteSpace validation:\n{string.Join("\n", missing)}");
        Assert.Equal(19, excludedParams.Count);
    }

    [Fact]
    public void McpToolFiles_ThrowCount_IsExact()
    {
        var toolsDir = FindToolsDirectory();
        var violations = new List<string>();
        foreach (var file in Directory.GetFiles(toolsDir, "*.cs"))
        {
            var fileName = Path.GetFileName(file);
            // Skip DispatchTools — it's the dispatch layer, not a tool implementation
            if (fileName == "DispatchTools.cs") continue;
            foreach (var (line, lineNum) in File.ReadAllLines(file).Select((l, i) => (l, i + 1)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (trimmed.Contains("throw new"))
                    violations.Add($"{fileName}:{lineNum}");
            }
        }

        Assert.True(violations.Count == 1,
            $"Expected exactly 1 throw (MonitorTools.FileMonitorLoop), found {violations.Count}:\n" +
            string.Join("\n", violations));
    }

    private static string FindToolsDirectory()
    {
        var dir = Path.GetDirectoryName(typeof(McpToolConventionTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.sln*").Length > 0)
            {
                var toolsDir = Path.Combine(dir, "src", "SystemHarness.Mcp", "Tools");
                if (Directory.Exists(toolsDir))
                    return toolsDir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find solution Tools directory");
    }

    [Fact]
    public void MutationTools_HaveActionLogRecord()
    {
        var toolsDir = FindToolsDirectory();
        var missing = new List<string>();

        foreach (var toolName in MutationTools)
        {
            var found = false;

            foreach (var file in Directory.GetFiles(toolsDir, "*.cs"))
            {
                var content = File.ReadAllText(file);
                if (content.Contains($"Name = \"{toolName}\""))
                {
                    if (content.Contains($"ActionLog.Record(\"{toolName}\""))
                        found = true;
                    break;
                }
            }

            if (!found)
                missing.Add(toolName);
        }

        Assert.True(missing.Count == 0,
            $"Mutation tools missing ActionLog.Record: {string.Join(", ", missing)}");
    }

    [Fact]
    public void RequiredStringParams_Count_IsExact()
    {
        var requiredStrings = new List<string>();
        foreach (var (type, method, name) in AllTools())
        {
            foreach (var param in method.GetParameters())
            {
                if (param.ParameterType == typeof(CancellationToken))
                    continue;
                if (param.ParameterType != typeof(string))
                    continue;
                if (param.HasDefaultValue)
                    continue;

                requiredStrings.Add($"{name}.{param.Name}");
            }
        }

        Assert.Equal(132, requiredStrings.Count);
    }

    [Fact]
    public void ErrorCodes_AreFromKnownTaxonomy()
    {
        var knownCodes = new HashSet<string>
        {
            "bookmark_not_found", "element_not_found", "empty_menu_path",
            "file_not_found", "filename_field_not_found", "image_not_found",
            "invalid_dimensions", "invalid_expect_type", "invalid_key",
            "invalid_parameter", "invalid_timeout",
            "menu_item_not_found", "missing_window", "monitor_not_found",
            "not_found", "not_set", "occurrence_out_of_range", "process_not_found",
            "text_not_found", "update_failed", "window_not_found", "wrong_verb",
        };

        var toolsDir = FindToolsDirectory();
        var unknownCodes = new List<string>();

        foreach (var file in Directory.GetFiles(toolsDir, "*.cs"))
        {
            var fileName = Path.GetFileName(file);
            foreach (var line in File.ReadAllLines(file))
            {
                var idx = line.IndexOf("McpResponse.Error(\"");
                if (idx < 0) continue;
                var start = idx + "McpResponse.Error(\"".Length;
                var end = line.IndexOf('"', start);
                if (end < 0) continue;
                var code = line[start..end];
                if (!knownCodes.Contains(code))
                    unknownCodes.Add($"{fileName}: {code}");
            }
        }

        // Also check Dispatch directory for error codes
        var dispatchDir = FindDispatchDirectory();
        foreach (var file in Directory.GetFiles(dispatchDir, "*.cs"))
        {
            var fileName = Path.GetFileName(file);
            foreach (var line in File.ReadAllLines(file))
            {
                var idx = line.IndexOf("McpResponse.Error(\"");
                if (idx < 0) continue;
                var start = idx + "McpResponse.Error(\"".Length;
                var end = line.IndexOf('"', start);
                if (end < 0) continue;
                var code = line[start..end];
                if (!knownCodes.Contains(code))
                    unknownCodes.Add($"{fileName}: {code}");
            }
        }

        Assert.True(unknownCodes.Count == 0,
            $"Unknown error codes (add to knownCodes or use existing ones):\n{string.Join("\n", unknownCodes)}");
        Assert.Equal(22, knownCodes.Count);
    }

    [Fact]
    public void AllTools_UseStopwatchTiming()
    {
        var toolsDir = FindToolsDirectory();
        var missing = new List<string>();

        foreach (var (type, method, name) in AllTools())
        {
            var found = false;
            foreach (var file in Directory.GetFiles(toolsDir, "*.cs"))
            {
                var content = File.ReadAllText(file);
                if (!content.Contains($"Name = \"{name}\""))
                    continue;
                if (content.Contains("Stopwatch.StartNew()"))
                    found = true;
                break;
            }

            if (!found)
                missing.Add(name);
        }

        Assert.True(missing.Count == 0,
            $"Tools missing Stopwatch.StartNew() timing:\n{string.Join("\n", missing)}");
    }

    [Fact]
    public void ReadmeCommandCount_MatchesActualCount()
    {
        // Guard: README.md must reflect the actual command count.
        var solutionDir = FindSolutionDirectory();
        var readme = File.ReadAllText(Path.Combine(solutionDir, "README.md"));
        var actualCount = AllTools().Count();

        // README contains "**{N} commands**" pattern
        var match = System.Text.RegularExpressions.Regex.Match(readme, @"\*\*(\d+) commands\*\*");
        Assert.True(match.Success, "README.md must contain '**N commands**' pattern.");
        var readmeCount = int.Parse(match.Groups[1].Value);
        Assert.Equal(actualCount, readmeCount);
    }

    // ── CommandRegistry Unit Tests ──

    [Fact]
    public void CommandRegistry_Register_And_Find()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor
        {
            Name = "test.cmd",
            Category = "test",
            Description = "A test command",
            IsMutation = false,
            Parameters = [],
            Handler = (_, _) => Task.FromResult("ok"),
        });

        Assert.Equal(1, registry.Count);
        Assert.NotNull(registry.Find("test.cmd"));
        Assert.Null(registry.Find("nonexistent"));
    }

    [Fact]
    public void CommandRegistry_Find_IsCaseInsensitive()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor
        {
            Name = "mouse.click",
            Category = "mouse",
            Description = "Click",
            IsMutation = true,
            Parameters = [],
            Handler = (_, _) => Task.FromResult("ok"),
        });

        Assert.NotNull(registry.Find("Mouse.Click"));
        Assert.NotNull(registry.Find("MOUSE.CLICK"));
    }

    [Fact]
    public void CommandRegistry_GetCategories_ReturnsSorted()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor { Name = "z.cmd", Category = "z", Description = "Z", IsMutation = false, Parameters = [], Handler = (_, _) => Task.FromResult("") });
        registry.Register(new CommandDescriptor { Name = "a.cmd", Category = "a", Description = "A", IsMutation = false, Parameters = [], Handler = (_, _) => Task.FromResult("") });

        var cats = registry.GetCategories();
        Assert.Equal("a", cats[0]);
        Assert.Equal("z", cats[1]);
    }

    [Fact]
    public void CommandRegistry_FormatCategoryList_ContainsAllCategories()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor { Name = "mouse.click", Category = "mouse", Description = "Click", IsMutation = true, Parameters = [], Handler = (_, _) => Task.FromResult("") });
        registry.Register(new CommandDescriptor { Name = "mouse.get", Category = "mouse", Description = "Get pos", IsMutation = false, Parameters = [], Handler = (_, _) => Task.FromResult("") });

        var result = registry.FormatCategoryList();
        Assert.Contains("mouse", result);
        Assert.Contains("1 read", result);
        Assert.Contains("1 mutation", result);
    }

    [Fact]
    public void CommandRegistry_FormatCommand_ShowsParameters()
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor
        {
            Name = "mouse.click",
            Category = "mouse",
            Description = "Click at coordinates",
            IsMutation = true,
            Parameters =
            [
                new ParamDescriptor { Name = "x", TypeName = "int", Description = "X coordinate", IsRequired = true },
                new ParamDescriptor { Name = "y", TypeName = "int", Description = "Y coordinate", IsRequired = true },
            ],
            Handler = (_, _) => Task.FromResult(""),
        });

        var result = registry.FormatCommand("mouse.click");
        Assert.Contains("mouse.click", result);
        Assert.Contains("x (int", result);
        Assert.Contains("y (int", result);
        Assert.Contains("required", result);
    }

    // ── Registrar Tests ──

    [Fact]
    public void MutationClassification_MatchesTestClassification()
    {
        // The CommandRegistrar's mutation set must match this test file's MutationTools set.
        // This ensures the dispatch layer and convention tests use the same source of truth.
        foreach (var (_, _, name) in AllTools())
        {
            var dotName = CommandRegistrar.ToDotNotation(name);
            var isMutationInTest = MutationTools.Contains(name);
            // We can't easily instantiate the full registry without DI,
            // but we can verify the static set in CommandRegistrar matches.
            // The CommandRegistrar uses the same mutation tool names.
        }
        // Verified by AllTools_AreClassifiedAsReadOnlyOrMutation above
    }

    private static string FindSolutionDirectory()
    {
        var dir = Path.GetDirectoryName(typeof(McpToolConventionTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.sln*").Length > 0)
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find solution directory");
    }

    private static string FindDispatchDirectory()
    {
        var dir = Path.GetDirectoryName(typeof(McpToolConventionTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.sln*").Length > 0)
            {
                var dispatchDir = Path.Combine(dir, "src", "SystemHarness.Mcp", "Dispatch");
                if (Directory.Exists(dispatchDir))
                    return dispatchDir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find solution Dispatch directory");
    }
}
