using System.ClientModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;

sealed record McpAIAgentConfig
{
    public required string McpServerCommand { get; init; }
    public required string[] McpServerArgs { get; init; }
    public required string AiEndpoint { get; init; }
    public required string AiApiKey { get; init; }
    public required string AiModel { get; init; }
    public int ScenarioTimeoutSeconds { get; init; } = 120;
}

sealed record ScenarioResult(
    string ScenarioName,
    bool Success,
    string Response,
    int ToolCallCount,
    TimeSpan Elapsed,
    string? Error);

sealed class McpAIAgent : IAsyncDisposable
{
    private readonly McpClient _mcpClient;
    private readonly IChatClient _chatClient;
    private readonly IList<McpClientTool> _tools;
    private readonly int _scenarioTimeoutSeconds;

    private McpAIAgent(McpClient mcpClient, IChatClient chatClient, IList<McpClientTool> tools, int timeoutSeconds)
    {
        _mcpClient = mcpClient;
        _chatClient = chatClient;
        _tools = tools;
        _scenarioTimeoutSeconds = timeoutSeconds;
    }

    public int ToolCount => _tools.Count;

    public static async Task<McpAIAgent> CreateAsync(McpAIAgentConfig config, CancellationToken ct = default)
    {
        // 1. Create MCP client via stdio transport
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "system-harness",
            Command = config.McpServerCommand,
            Arguments = config.McpServerArgs,
        });
        var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: ct);

        // 2. Get available tools
        var tools = await mcpClient.ListToolsAsync(cancellationToken: ct);

        // 3. Create AI client (OpenAI-compatible endpoint)
        var openAiOptions = new OpenAIClientOptions { Endpoint = new Uri(config.AiEndpoint) };
        var openAiClient = new OpenAIClient(new ApiKeyCredential(config.AiApiKey), openAiOptions);

        IChatClient chatClient = new ChatClientBuilder(
                openAiClient.GetChatClient(config.AiModel).AsIChatClient())
            .UseFunctionInvocation()
            .Build();

        return new McpAIAgent(mcpClient, chatClient, tools, config.ScenarioTimeoutSeconds);
    }

    public async Task<ScenarioResult> RunAsync(Scenario scenario, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_scenarioTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, Scenarios.SystemPrompt),
                new(ChatRole.User, scenario.Task),
            };

            var response = await _chatClient.GetResponseAsync(
                messages,
                new ChatOptions { Tools = [.. _tools], MaxOutputTokens = 2048 },
                linkedCts.Token);

            sw.Stop();

            int toolCallCount = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .Count();

            return new ScenarioResult(
                scenario.Name,
                Success: true,
                Response: response.Text ?? "(no response)",
                ToolCallCount: toolCallCount,
                Elapsed: sw.Elapsed,
                Error: null);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            sw.Stop();
            return new ScenarioResult(
                scenario.Name,
                Success: false,
                Response: string.Empty,
                ToolCallCount: 0,
                Elapsed: sw.Elapsed,
                Error: $"Timeout after {_scenarioTimeoutSeconds}s");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ScenarioResult(
                scenario.Name,
                Success: false,
                Response: string.Empty,
                ToolCallCount: 0,
                Elapsed: sw.Elapsed,
                Error: ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _mcpClient.DisposeAsync();
        if (_chatClient is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (_chatClient is IDisposable disposable)
            disposable.Dispose();
    }
}
