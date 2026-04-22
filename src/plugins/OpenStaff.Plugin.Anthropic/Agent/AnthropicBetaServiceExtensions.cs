using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Anthropic.Services;

public static class AnthropicBetaServiceExtensions
{
    public static int DefaultMaxTokens { get; set; } = 4096;

    public static ChatClientAgent AsAIAgent(
        this IBetaService betaService,
        string model,
        string? instructions = null,
        string? name = null,
        string? description = null,
        IList<AITool>? tools = null,
        int? defaultMaxTokens = null,
        Func<IChatClient, IChatClient>? clientFactory = null,
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null)
    {
        var options = new ChatClientAgentOptions
        {
            Name = name,
            Description = description,
        };

        if (!string.IsNullOrWhiteSpace(instructions))
        {
            options.ChatOptions ??= new();
            options.ChatOptions.Instructions = instructions;
        }

        if (tools is { Count: > 0 })
        {
            options.ChatOptions ??= new();
            options.ChatOptions.Tools = tools;
        }

        var chatClient = betaService.AsIChatClient(model, defaultMaxTokens ?? DefaultMaxTokens);

        if (clientFactory is not null)
            chatClient = clientFactory(chatClient);

        return new ChatClientAgent(chatClient, options, loggerFactory, services);
    }

    public static ChatClientAgent AsAIAgent(
        this IBetaService betaService,
        ChatClientAgentOptions options,
        Func<IChatClient, IChatClient>? clientFactory = null,
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null)
    {
        var chatClient = betaService.AsIChatClient();

        if (clientFactory is not null)
            chatClient = clientFactory(chatClient);

        return new ChatClientAgent(chatClient, options, loggerFactory, services);
    }
}
