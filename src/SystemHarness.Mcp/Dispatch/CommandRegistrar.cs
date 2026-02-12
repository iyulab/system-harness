using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace SystemHarness.Mcp.Dispatch;

/// <summary>
/// Scans existing tool classes and registers them as dispatchable commands.
/// Converts snake_case tool names to dot notation (mouse_click → mouse.click).
/// </summary>
public static class CommandRegistrar
{
    /// <summary>
    /// Tools that modify system state. All others are read-only.
    /// Source of truth — matches the convention test classification.
    /// </summary>
    private static readonly HashSet<string> MutationToolNames =
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

    /// <summary>
    /// Register all tool implementation classes in DI as transient services.
    /// Scans for types that have methods with [McpServerTool], excluding DispatchTools.
    /// Call this before Build() so DI can resolve them later.
    /// </summary>
    public static void RegisterToolTypes(IServiceCollection services)
    {
        var assembly = typeof(CommandRegistrar).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (type == typeof(Tools.DispatchTools))
                continue;

            var hasMcpMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

            if (hasMcpMethods)
                services.AddTransient(type);
        }
    }

    /// <summary>
    /// Scan assembly for [McpServerTool] methods and register them in the command registry.
    /// Call this after Build() when DI is ready.
    /// </summary>
    public static void RegisterAll(CommandRegistry registry, IServiceProvider sp)
    {
        var assembly = typeof(CommandRegistrar).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (type == typeof(Tools.DispatchTools))
                continue;

            // Find types that have methods with [McpServerTool] — these are tool classes
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (toolAttr?.Name is null)
                    continue;

                var snakeName = toolAttr.Name;
                var dotName = ToDotNotation(snakeName);
                var category = dotName.Split('.')[0];
                var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var isMutation = MutationToolNames.Contains(snakeName);
                var parameters = BuildParamDescriptors(method);
                var handler = CreateHandler(sp, type, method);

                registry.Register(new CommandDescriptor
                {
                    Name = dotName,
                    Category = category,
                    Description = description,
                    IsMutation = isMutation,
                    Parameters = parameters,
                    Handler = handler,
                });
            }
        }
    }

    /// <summary>
    /// Convert snake_case to dot notation: first underscore becomes dot, rest stay.
    /// mouse_click → mouse.click, mouse_click_double → mouse.click_double
    /// </summary>
    public static string ToDotNotation(string snakeName)
    {
        var idx = snakeName.IndexOf('_');
        if (idx < 0) return snakeName;
        return string.Concat(snakeName.AsSpan(0, idx), ".", snakeName.AsSpan(idx + 1));
    }

    private static IReadOnlyList<ParamDescriptor> BuildParamDescriptors(MethodInfo method)
    {
        var list = new List<ParamDescriptor>();
        foreach (var p in method.GetParameters())
        {
            if (p.ParameterType == typeof(CancellationToken))
                continue;

            var desc = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            var typeName = MapTypeName(p.ParameterType);
            var isRequired = !p.HasDefaultValue;
            string? defaultValue = p.HasDefaultValue && p.DefaultValue is not null
                ? p.DefaultValue.ToString()
                : p.HasDefaultValue ? "null" : null;

            list.Add(new ParamDescriptor
            {
                Name = p.Name!,
                TypeName = typeName,
                Description = desc,
                IsRequired = isRequired,
                DefaultValue = defaultValue,
            });
        }
        return list;
    }

    private static string MapTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return MapTypeName(underlying) + "?";

        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(bool)) return "bool";
        return type.Name;
    }

    private static Func<JsonElement?, CancellationToken, Task<string>> CreateHandler(
        IServiceProvider sp, Type toolType, MethodInfo method)
    {
        return async (JsonElement? args, CancellationToken ct) =>
        {
            var instance = sp.GetRequiredService(toolType);
            var parameters = method.GetParameters();
            var values = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType == typeof(CancellationToken))
                {
                    values[i] = ct;
                    continue;
                }

                if (args.HasValue && args.Value.TryGetProperty(p.Name!, out var prop))
                {
                    values[i] = DeserializeParam(prop, p.ParameterType);
                }
                else if (p.HasDefaultValue)
                {
                    values[i] = p.DefaultValue;
                }
                else
                {
                    return McpResponse.Error("invalid_parameter",
                        $"Missing required parameter: '{p.Name}'");
                }
            }

            var result = method.Invoke(instance, values);
            return await (Task<string>)result!;
        };
    }

    private static object? DeserializeParam(JsonElement element, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying is not null)
        {
            if (element.ValueKind == JsonValueKind.Null)
                return null;
            targetType = underlying;
        }

        if (targetType == typeof(string))
            return element.GetString();
        if (targetType == typeof(int))
            return element.GetInt32();
        if (targetType == typeof(long))
            return element.GetInt64();
        if (targetType == typeof(double))
            return element.GetDouble();
        if (targetType == typeof(float))
            return element.GetSingle();
        if (targetType == typeof(bool))
            return element.GetBoolean();

        // Fallback: deserialize as JSON
        return JsonSerializer.Deserialize(element.GetRawText(), targetType);
    }
}
