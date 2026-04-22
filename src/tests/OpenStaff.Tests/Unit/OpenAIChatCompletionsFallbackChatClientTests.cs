using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Provider;

namespace OpenStaff.Tests.Unit;

public sealed class OpenAIChatCompletionsFallbackChatClientTests
{
    [Fact]
    public async Task GetStreamingResponseAsync_DowngradesToSingleResponseWhenToolsArePresent()
    {
        var inner = new StubChatClient(
            getResponseAsync: static (_, _, _) => Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, [
                        new TextContent("final answer"),
                    new FunctionCallContent("call-1", "demo.tool", new Dictionary<string, object?>
                    {
                        ["query"] = "hello"
                    })
                ]))),
            getStreamingResponseAsync: static (_, _, _) => ThrowIfEnumerated());

        var client = new OpenAIChatCompletionsFallbackChatClient(inner, CreateLogger(), "openai-gpt-oss-120b");
        var options = new ChatOptions
        {
            Tools = [new TestTool()]
        };

        var updates = await ReadAllAsync(
            client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hello")], options));

        Assert.True(inner.ResponseCalled);
        Assert.False(inner.StreamingCalled);
        Assert.NotEmpty(updates);
        Assert.Contains(
            updates.SelectMany(update => update.Contents!.OfType<TextContent>()),
            content => content.Text == "final answer");
        Assert.Contains(
            updates.SelectMany(update => update.Contents!.OfType<FunctionCallContent>()),
            content => content.Name == "demo.tool" && content.CallId == "call-1");
    }

    [Fact]
    public async Task GetStreamingResponseAsync_UsesOriginalStreamingPathWhenNoToolsArePresent()
    {
        var inner = new StubChatClient(
            getResponseAsync: static (_, _, _) => throw new InvalidOperationException("response path should not be used"),
            getStreamingResponseAsync: static (_, _, _) => YieldStreamingUpdate());

        var client = new OpenAIChatCompletionsFallbackChatClient(inner, CreateLogger(), "openai-gpt-oss-120b");

        var updates = await ReadAllAsync(
            client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hello")], new ChatOptions()));

        Assert.False(inner.ResponseCalled);
        Assert.True(inner.StreamingCalled);
        Assert.Single(updates);
        Assert.Contains(
            updates[0].Contents!.OfType<TextContent>(),
            content => content.Text == "streaming answer");
    }

    private static ILogger<OpenAIChatCompletionsFallbackChatClient> CreateLogger()
    {
        return new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider()
            .GetRequiredService<ILogger<OpenAIChatCompletionsFallbackChatClient>>();
    }

    private static async Task<List<ChatResponseUpdate>> ReadAllAsync(IAsyncEnumerable<ChatResponseUpdate> updates)
    {
        var result = new List<ChatResponseUpdate>();
        await foreach (var update in updates)
            result.Add(update);

        return result;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> YieldStreamingUpdate()
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("streaming answer")]);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> ThrowIfEnumerated()
    {
        if (false)
            yield return new ChatResponseUpdate();

        await Task.CompletedTask;
        throw new InvalidOperationException("streaming path should not be used");
    }

    private sealed class StubChatClient(
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> getResponseAsync,
        Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> getStreamingResponseAsync)
        : IChatClient
    {
        public bool ResponseCalled { get; private set; }

        public bool StreamingCalled { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ResponseCalled = true;
            return getResponseAsync(messages, options, cancellationToken);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            StreamingCalled = true;
            return getStreamingResponseAsync(messages, options, cancellationToken);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class TestTool : AITool
    {
        public override string Name => "demo.tool";

        public override string Description => "demo";
    }
}
