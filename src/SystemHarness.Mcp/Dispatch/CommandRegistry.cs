using System.Globalization;
using System.Text;

namespace SystemHarness.Mcp.Dispatch;

/// <summary>
/// Thread-safe registry of all dispatchable commands.
/// Provides lookup and help formatting.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandDescriptor> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<CommandDescriptor>> _categories = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _commands.Count;

    public void Register(CommandDescriptor descriptor)
    {
        _commands[descriptor.Name] = descriptor;
        if (!_categories.TryGetValue(descriptor.Category, out var list))
        {
            list = [];
            _categories[descriptor.Category] = list;
        }
        list.Add(descriptor);
    }

    public CommandDescriptor? Find(string name) =>
        _commands.TryGetValue(name, out var cmd) ? cmd : null;

    public IReadOnlyList<string> GetCategories() =>
        _categories.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<CommandDescriptor> GetByCategory(string category) =>
        _categories.TryGetValue(category, out var list) ? list : [];

    // ── Help formatters ──

    /// <summary>List all categories with command counts.</summary>
    public string FormatCategoryList()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{Count} commands in {_categories.Count} categories:\n");
        foreach (var cat in GetCategories())
        {
            var cmds = _categories[cat];
            var mutations = cmds.Count(c => c.IsMutation);
            var reads = cmds.Count - mutations;
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {cat} ({cmds.Count}) — {reads} read, {mutations} mutation");
        }
        sb.AppendLine($"\nUse help(\"<category>\") to list commands in a category.");
        return McpResponse.Content(sb.ToString(), "text");
    }

    /// <summary>List all commands in a category.</summary>
    public string FormatCategory(string category)
    {
        if (!_categories.TryGetValue(category, out var cmds))
            return McpResponse.Error("not_found", $"Unknown category: '{category}'. Use help() to list categories.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{category} ({cmds.Count} commands):\n");
        foreach (var cmd in cmds.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            var kind = cmd.IsMutation ? "do" : "get";
            sb.AppendLine(CultureInfo.InvariantCulture, $"  [{kind}] {cmd.Name} — {cmd.Description}");
        }
        sb.AppendLine($"\nUse help(\"<command>\") for parameter details.");
        return McpResponse.Content(sb.ToString(), "text");
    }

    /// <summary>Show full parameter details for a command.</summary>
    public string FormatCommand(string commandName)
    {
        if (!_commands.TryGetValue(commandName, out var cmd))
            return McpResponse.Error("not_found", $"Unknown command: '{commandName}'. Use help() to list categories.");

        var sb = new StringBuilder();
        var kind = cmd.IsMutation ? "do" : "get";
        sb.AppendLine(CultureInfo.InvariantCulture, $"{cmd.Name} [{kind}]");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  {cmd.Description}\n");

        if (cmd.Parameters.Count == 0)
        {
            sb.AppendLine("  No parameters.");
        }
        else
        {
            sb.AppendLine("  Parameters:");
            foreach (var p in cmd.Parameters)
            {
                var req = p.IsRequired ? "required" : $"optional, default={p.DefaultValue ?? "null"}";
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {p.Name} ({p.TypeName}, {req}) — {p.Description}");
            }
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"\n  Example: {kind}(\"{cmd.Name}\"{(cmd.Parameters.Count > 0 ? ", '{...}'" : "")})");
        return McpResponse.Content(sb.ToString(), "text");
    }
}
