using System.Text.Json;

namespace SystemHarness.Mcp.Dispatch;

/// <summary>
/// Describes a single dispatchable command (e.g. "mouse.click").
/// </summary>
public sealed record CommandDescriptor
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public required bool IsMutation { get; init; }
    public required IReadOnlyList<ParamDescriptor> Parameters { get; init; }
    public required Func<JsonElement?, CancellationToken, Task<string>> Handler { get; init; }
}

/// <summary>
/// Describes a single parameter of a command.
/// </summary>
public sealed record ParamDescriptor
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public required string Description { get; init; }
    public required bool IsRequired { get; init; }
    public string? DefaultValue { get; init; }
}
