using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SystemHarness.Mcp.Dispatch;

namespace SystemHarness.Mcp.Tools;

[McpServerToolType]
public sealed class DispatchTools(CommandRegistry registry)
{
    [McpServerTool(Name = "help"), Description(
        "Discover available commands. " +
        "No arguments: list categories. " +
        "Category name (e.g. 'mouse'): list commands. " +
        "Full command name (e.g. 'mouse.click'): show parameters.")]
    public Task<string> HelpAsync(
        [Description("Category or command name.")] string? topic = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return Task.FromResult(registry.FormatCategoryList());

        topic = topic.Trim();

        // If it contains a dot, treat as command name
        if (topic.Contains('.'))
            return Task.FromResult(registry.FormatCommand(topic));

        // Otherwise try as category first, then as command
        var byCategory = registry.GetByCategory(topic);
        if (byCategory.Count > 0)
            return Task.FromResult(registry.FormatCategory(topic));

        // Maybe it's a command without dot notation? Try to find it
        return Task.FromResult(registry.FormatCommand(topic));
    }

    [McpServerTool(Name = "do"), Description(
        "Execute a mutation command (mouse/keyboard actions, file writes, process control, etc.)")]
    public async Task<string> DoAsync(
        [Description("Command in dot notation (e.g. 'mouse.click').")] string command,
        [Description("JSON object with parameters.")] string? @params = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            return McpResponse.Error("invalid_parameter", "command cannot be empty.");

        var cmd = registry.Find(command.Trim());
        if (cmd is null)
            return McpResponse.Error("not_found", $"Unknown command: '{command}'. Use help() to discover commands.");

        if (!cmd.IsMutation)
            return McpResponse.Error("wrong_verb",
                $"'{command}' is read-only. Use get(\"{command}\") instead.");

        return await ExecuteAsync(cmd, @params, ct);
    }

    [McpServerTool(Name = "get"), Description(
        "Execute a read-only query (window list, file read, screen capture, etc.)")]
    public async Task<string> GetAsync(
        [Description("Command in dot notation (e.g. 'window.list').")] string command,
        [Description("JSON object with parameters.")] string? @params = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            return McpResponse.Error("invalid_parameter", "command cannot be empty.");

        var cmd = registry.Find(command.Trim());
        if (cmd is null)
            return McpResponse.Error("not_found", $"Unknown command: '{command}'. Use help() to discover commands.");

        if (cmd.IsMutation)
            return McpResponse.Error("wrong_verb",
                $"'{command}' is a mutation. Use do(\"{command}\") instead.");

        return await ExecuteAsync(cmd, @params, ct);
    }

    private static async Task<string> ExecuteAsync(CommandDescriptor cmd, string? paramsJson, CancellationToken ct)
    {
        JsonElement? args = null;
        if (!string.IsNullOrWhiteSpace(paramsJson))
        {
            try
            {
                args = JsonDocument.Parse(paramsJson).RootElement;
            }
            catch (JsonException ex)
            {
                return McpResponse.Error("invalid_parameter",
                    $"Invalid JSON in params: {ex.Message}");
            }
        }

        return await cmd.Handler(args, ct);
    }
}
