using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

internal sealed class GitHubCopilotTraceAgent : DelegatingAIAgent
{
    private readonly CopilotClient _copilotClient;
    private readonly IGitHubCopilotClientHost _clientHost;
    private readonly SessionConfig _baseSessionConfig;
    private readonly IReadOnlySet<string> _allowedSkillDirectories;
    private readonly IReadOnlyList<string> _configuredSkillDiscoveryDirectories;

    public GitHubCopilotTraceAgent(
        GitHubCopilotAgent innerAgent,
        CopilotClient copilotClient,
        IGitHubCopilotClientHost clientHost,
        SessionConfig baseSessionConfig,
        IReadOnlyList<string>? allowedSkillDirectories = null)
        : base(innerAgent)
    {
        _copilotClient = copilotClient;
        _clientHost = clientHost;
        _baseSessionConfig = CloneSessionConfig(baseSessionConfig);
        _allowedSkillDirectories = (allowedSkillDirectories ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Where(path => path is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _configuredSkillDiscoveryDirectories = (_baseSessionConfig.SkillDirectories ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Where(path => path is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var sessionId = ResolveSessionId(session);
        await EnsureClientStartedAsync(cancellationToken);

        await using var copilotSession = await OpenSessionAsync(sessionId, options, cancellationToken);
        var updates = Channel.CreateUnbounded<AgentResponseUpdate>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var traceState = new TraceProjectionState();
        string? tempDir = null;

        using var subscription = copilotSession.On(evt => HandleSessionEvent(evt, updates.Writer, traceState));
        try
        {
            var prompt = string.Join("\n", messages.Select(message => message.Text));
            var (attachments, createdTempDir) = await ProcessDataContentAttachmentsAsync(messages, cancellationToken);
            tempDir = createdTempDir;

            var messageOptions = new MessageOptions
            {
                Prompt = prompt
            };
            if (attachments is { Count: > 0 })
                messageOptions.Attachments = attachments;

            await copilotSession.SendAsync(messageOptions, cancellationToken);

            await foreach (var update in updates.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                yield return update;
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    internal SessionConfig CreateExecutionSessionConfig(string? sessionId, AgentRunOptions? options)
    {
        var config = CloneSessionConfig(_baseSessionConfig);
        config.SessionId = string.IsNullOrWhiteSpace(sessionId) ? config.SessionId : sessionId;
        ApplyExecutionOverrides(config, ExtractExecutionOverrides(options));
        return config;
    }

    internal ResumeSessionConfig CreateExecutionResumeConfig(AgentRunOptions? options)
    {
        var config = CloneResumeSessionConfig(_baseSessionConfig);
        ApplyExecutionOverrides(config, ExtractExecutionOverrides(options));
        return config;
    }

    internal static AgentResponseUpdate? ProjectUpdate(
        AgentResponseUpdate update,
        TraceProjectionState state)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(state);

        List<AIContent>? projectedContents = null;
        foreach (var content in update.Contents)
        {
            TrackExistingContent(content, state);

            if (content is TextContent
                or TextReasoningContent
                or FunctionCallContent
                or FunctionResultContent
                or UsageContent)
            {
                (projectedContents ??= []).Add(content);
                continue;
            }

            if (TryProjectSessionEvent(content.RawRepresentation as SessionEvent, state, out var mappedContents))
            {
                projectedContents ??= [];
                projectedContents.AddRange(mappedContents);
            }
        }

        if (projectedContents is not { Count: > 0 })
            return null;

        return CopyUpdate(update, projectedContents);
    }

    private static AgentResponseUpdate CopyUpdate(
        AgentResponseUpdate update,
        IList<AIContent> contents)
    {
        return new AgentResponseUpdate(update.Role, contents)
        {
            AdditionalProperties = update.AdditionalProperties,
            AgentId = update.AgentId,
            AuthorName = update.AuthorName,
            ContinuationToken = update.ContinuationToken,
            CreatedAt = update.CreatedAt,
            FinishReason = update.FinishReason,
            MessageId = update.MessageId,
            RawRepresentation = update.RawRepresentation,
            ResponseId = update.ResponseId
        };
    }

    private static void TrackExistingContent(AIContent content, TraceProjectionState state)
    {
        switch (content)
        {
            case TextContent when content.RawRepresentation is AssistantMessageDeltaEvent delta
                                  && !string.IsNullOrWhiteSpace(delta.Data.MessageId):
                state.MessageIdsWithDelta.Add(delta.Data.MessageId);
                break;

            case TextReasoningContent:
                state.SawAnyReasoning = true;
                break;

            case FunctionCallContent functionCall when !string.IsNullOrWhiteSpace(functionCall.CallId):
                state.ToolNamesByCallId[functionCall.CallId] = functionCall.Name;
                break;
        }
    }

    private static bool TryProjectSessionEvent(
        SessionEvent? sessionEvent,
        TraceProjectionState state,
        out List<AIContent> contents)
    {
        contents = [];
        if (sessionEvent is null)
            return false;

        switch (sessionEvent)
        {
            case AssistantReasoningDeltaEvent reasoningDelta when !string.IsNullOrWhiteSpace(reasoningDelta.Data.DeltaContent):
                if (!string.IsNullOrWhiteSpace(reasoningDelta.Data.ReasoningId))
                    state.ReasoningIdsWithDelta.Add(reasoningDelta.Data.ReasoningId);

                state.SawAnyReasoning = true;
                contents.Add(new TextReasoningContent(reasoningDelta.Data.DeltaContent)
                {
                    RawRepresentation = sessionEvent
                });
                return true;

            case AssistantReasoningEvent reasoning
                when !state.ReasoningIdsWithDelta.Contains(reasoning.Data.ReasoningId)
                     && !string.IsNullOrWhiteSpace(reasoning.Data.Content):
                state.SawAnyReasoning = true;
                contents.Add(new TextReasoningContent(reasoning.Data.Content)
                {
                    RawRepresentation = sessionEvent
                });
                return true;

            case AssistantMessageEvent assistantMessage:
                if (!string.IsNullOrWhiteSpace(assistantMessage.Data.MessageId)
                    && state.MessageIdsWithDelta.Contains(assistantMessage.Data.MessageId))
                {
                    if (!state.SawAnyReasoning && !string.IsNullOrWhiteSpace(assistantMessage.Data.ReasoningText))
                    {
                        state.SawAnyReasoning = true;
                        contents.Add(new TextReasoningContent(assistantMessage.Data.ReasoningText)
                        {
                            RawRepresentation = sessionEvent
                        });
                    }

                    return contents.Count > 0;
                }

                if (!string.IsNullOrWhiteSpace(assistantMessage.Data.Content))
                {
                    contents.Add(new TextContent(assistantMessage.Data.Content)
                    {
                        RawRepresentation = sessionEvent
                    });
                }

                if (!state.SawAnyReasoning && !string.IsNullOrWhiteSpace(assistantMessage.Data.ReasoningText))
                {
                    state.SawAnyReasoning = true;
                    contents.Add(new TextReasoningContent(assistantMessage.Data.ReasoningText)
                    {
                        RawRepresentation = sessionEvent
                    });
                }

                return contents.Count > 0;

            case ToolExecutionStartEvent toolStart:
                var toolName = ResolveToolName(toolStart.Data);
                state.ToolNamesByCallId[toolStart.Data.ToolCallId] = toolName;
                contents.Add(CreateToolCallContent(toolStart.Data, toolName, sessionEvent));
                return true;

            case ToolExecutionCompleteEvent toolComplete:
                contents.Add(CreateToolResultContent(toolComplete.Data, state, sessionEvent));
                return true;

            case SkillInvokedEvent skillInvoked:
                var skillCallId = $"skill-{skillInvoked.Id:N}";
                const string skillToolName = "skill.invoke";
                state.ToolNamesByCallId[skillCallId] = skillToolName;
                contents.Add(CreateSkillCallContent(skillCallId, skillInvoked.Data, sessionEvent));
                contents.Add(CreateSkillResultContent(skillCallId, skillInvoked.Data, sessionEvent));
                return true;

            default:
                return false;
        }
    }

    private async Task EnsureClientStartedAsync(CancellationToken cancellationToken)
    {
        if (_copilotClient.State != ConnectionState.Connected)
            await _copilotClient.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private string? ResolveSessionId(AgentSession? session)
    {
        if (session is null)
            return _baseSessionConfig.SessionId;

        return (session as GitHubCopilotAgentSession)?.SessionId
            ?? throw new InvalidOperationException(
                $"The provided session type '{session.GetType().Name}' is not compatible with this agent. Only sessions of type '{nameof(GitHubCopilotAgentSession)}' can be used by this agent.");
    }

    private async Task<CopilotSession> OpenSessionAsync(
        string? sessionId,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        CopilotSession copilotSession;
        if (!string.IsNullOrWhiteSpace(sessionId)
            && await _clientHost.SessionExistsAsync(sessionId, cancellationToken))
        {
            copilotSession = await _copilotClient.ResumeSessionAsync(
                sessionId,
                CreateExecutionResumeConfig(options),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            //var models=await _copilotClient.ListModelsAsync(cancellationToken).ConfigureAwait(false);
            //var model=models.First(x=>x.Id==_baseSessionConfig.Model);
            if (options is ChatClientAgentRunOptions agentRunOptions) {
            agentRunOptions.ChatOptions?.Reasoning = null;
            }
            copilotSession = await _copilotClient.CreateSessionAsync(
                CreateExecutionSessionConfig(sessionId, options),
                cancellationToken).ConfigureAwait(false);
        }

        await SyncSessionSkillsAsync(copilotSession, cancellationToken).ConfigureAwait(false);
        return copilotSession;
    }

    private async Task SyncSessionSkillsAsync(CopilotSession copilotSession, CancellationToken cancellationToken)
    {
        var skills = await copilotSession.Rpc.Skills.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var skill in skills.Skills)
        {
            var skillName = ReadSkillName(skill);
            if (string.IsNullOrWhiteSpace(skillName))
                continue;

            var skillPath = ReadSkillPath(skill);
            var isEnabled = ReadSkillEnabled(skill);
            var shouldEnable = IsSkillEnabledForRuntime(skillPath, _allowedSkillDirectories, _configuredSkillDiscoveryDirectories);
            if (shouldEnable && !isEnabled)
            {
                await copilotSession.Rpc.Skills.EnableAsync(skillName, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!shouldEnable && isEnabled)
            {
                await copilotSession.Rpc.Skills.DisableAsync(skillName, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void HandleSessionEvent(
        SessionEvent sessionEvent,
        ChannelWriter<AgentResponseUpdate> writer,
        TraceProjectionState state)
    {
        try
        {
            switch (sessionEvent)
            {
                case AssistantMessageDeltaEvent delta when !string.IsNullOrWhiteSpace(delta.Data.DeltaContent):
                    if (!string.IsNullOrWhiteSpace(delta.Data.MessageId))
                        state.MessageIdsWithDelta.Add(delta.Data.MessageId);

                    writer.TryWrite(CreateAssistantUpdate(
                        new TextContent(delta.Data.DeltaContent)
                        {
                            RawRepresentation = sessionEvent
                        },
                        sessionEvent.Timestamp,
                        delta.Data.MessageId,
                        delta.Data.MessageId));
                    return;

                case AssistantUsageEvent usage:
                    var usageUpdate = CreateUsageUpdate(usage, sessionEvent.Timestamp);
                    if (usageUpdate is not null)
                        writer.TryWrite(usageUpdate);
                    return;

                case SessionIdleEvent:
                    writer.TryComplete();
                    return;

                case SessionErrorEvent errorEvent:
                    writer.TryComplete(new InvalidOperationException(
                        $"Session error: {errorEvent.Data?.Message ?? "Unknown error"}"));
                    return;
            }

            var rawUpdate = new AgentResponseUpdate(ChatRole.Assistant, [new AIContent
            {
                RawRepresentation = sessionEvent
            }])
            {
                AgentId = Id,
                CreatedAt = sessionEvent.Timestamp,
                MessageId = sessionEvent switch
                {
                    AssistantMessageEvent assistantMessage => assistantMessage.Data.MessageId,
                    _ => null
                },
                ResponseId = sessionEvent switch
                {
                    AssistantMessageEvent assistantMessage => assistantMessage.Data.MessageId,
                    _ => null
                }
            };

            var projected = ProjectUpdate(rawUpdate, state);
            if (projected is not null)
                writer.TryWrite(projected);
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
        }
    }

    private AgentResponseUpdate? CreateUsageUpdate(AssistantUsageEvent usageEvent, DateTimeOffset timestamp)
    {
        var inputTokens = ToNullableInt(usageEvent.Data?.InputTokens);
        var outputTokens = ToNullableInt(usageEvent.Data?.OutputTokens);
        var cachedInputTokens = ToNullableInt(usageEvent.Data?.CacheReadTokens);

        if (inputTokens is null && outputTokens is null && cachedInputTokens is null)
            return null;

        var usage = new UsageDetails
        {
            InputTokenCount = inputTokens,
            OutputTokenCount = outputTokens,
            TotalTokenCount = inputTokens.HasValue || outputTokens.HasValue
                ? (inputTokens ?? 0) + (outputTokens ?? 0)
                : null,
            CachedInputTokenCount = cachedInputTokens
        };

        return CreateAssistantUpdate(new UsageContent(usage)
        {
            RawRepresentation = usageEvent
        }, timestamp);
    }

    private AgentResponseUpdate CreateAssistantUpdate(
        AIContent content,
        DateTimeOffset timestamp,
        string? messageId = null,
        string? responseId = null)
    {
        return new AgentResponseUpdate(ChatRole.Assistant, [content])
        {
            AgentId = Id,
            CreatedAt = timestamp,
            MessageId = messageId,
            ResponseId = responseId
        };
    }

    private static FunctionCallContent CreateToolCallContent(
        ToolExecutionStartData data,
        string toolName,
        SessionEvent sessionEvent)
    {
        return new FunctionCallContent(
            data.ToolCallId,
            toolName,
            NormalizeArguments(data.Arguments))
        {
            RawRepresentation = sessionEvent
        };
    }

    private static FunctionResultContent CreateToolResultContent(
        ToolExecutionCompleteData data,
        TraceProjectionState state,
        SessionEvent sessionEvent)
    {
        var result = data.Success ? ResolveToolResult(data.Result) : ResolveToolError(data, state);
        return new FunctionResultContent(data.ToolCallId, result)
        {
            Exception = data.Success ? null : new InvalidOperationException(result ?? "Tool execution failed."),
            RawRepresentation = sessionEvent
        };
    }

    private static FunctionCallContent CreateSkillCallContent(
        string callId,
        SkillInvokedData data,
        SessionEvent sessionEvent)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = data.Name,
            ["path"] = data.Path
        };
        if (!string.IsNullOrWhiteSpace(data.PluginName))
            arguments["pluginName"] = data.PluginName;
        if (data.AllowedTools is { Length: > 0 })
            arguments["allowedTools"] = data.AllowedTools;

        return new FunctionCallContent(callId, "skill.invoke", arguments)
        {
            RawRepresentation = sessionEvent
        };
    }

    private static FunctionResultContent CreateSkillResultContent(
        string callId,
        SkillInvokedData data,
        SessionEvent sessionEvent)
    {
        var result = $"Loaded skill '{data.Name}' from '{data.Path}'.";
        return new FunctionResultContent(callId, result)
        {
            RawRepresentation = sessionEvent
        };
    }

    private static string ResolveToolName(ToolExecutionStartData data)
    {
        if (!string.IsNullOrWhiteSpace(data.ToolName))
            return data.ToolName;

        if (!string.IsNullOrWhiteSpace(data.McpToolName))
            return data.McpToolName;

        return "unknown";
    }

    private static string ResolveToolError(ToolExecutionCompleteData data, TraceProjectionState state)
    {
        return data.Error?.Message
            ?? state.ToolNamesByCallId.GetValueOrDefault(data.ToolCallId)
            ?? "Tool execution failed.";
    }

    private static string? ResolveToolResult(ToolExecutionCompleteDataResult? result)
    {
        if (result is null)
            return null;

        if (!string.IsNullOrWhiteSpace(result.DetailedContent))
            return result.DetailedContent;

        if (!string.IsNullOrWhiteSpace(result.Content))
            return result.Content;

        if (result.Contents is null || result.Contents.Length == 0)
            return null;

        var parts = result.Contents
            .Select(content => content switch
            {
                ToolExecutionCompleteDataResultContentsItemText text => text.Text,
                ToolExecutionCompleteDataResultContentsItemTerminal terminal => terminal.Text,
                _ => JsonSerializer.Serialize(content)
            })
            .Where(part => !string.IsNullOrWhiteSpace(part));

        return string.Join(Environment.NewLine, parts);
    }

    internal static bool IsSkillEnabledForRuntime(
        string? skillPath,
        IReadOnlySet<string> allowedDirectories,
        IReadOnlyList<string>? discoveryDirectories = null)
    {
        if (allowedDirectories.Count == 0)
            return false;

        foreach (var candidateDirectory in GetComparableSkillDirectories(skillPath))
        {
            if (allowedDirectories.Contains(candidateDirectory))
                return true;

            if (MapAgentStagedSkillDirectories(candidateDirectory, discoveryDirectories)
                .Any(allowedDirectories.Contains))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadSkillName(object skill)
        => ReadSkillProperty(skill, "Name") as string;

    private static string? ReadSkillPath(object skill)
        => ReadSkillProperty(skill, "Path") as string;

    private static bool ReadSkillEnabled(object skill)
        => ReadSkillProperty(skill, "Enabled") as bool? ?? false;

    private static object? ReadSkillProperty(object skill, string propertyName)
    {
        var property = skill.GetType().GetProperty(propertyName);
        return property?.GetValue(skill);
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static IEnumerable<string> GetComparableSkillDirectories(string? skillPath)
    {
        var normalizedPath = NormalizePath(skillPath);
        if (normalizedPath is null)
            yield break;

        yield return normalizedPath;

        var parentDirectory = NormalizePath(Path.GetDirectoryName(normalizedPath));
        if (parentDirectory is not null
            && !string.Equals(parentDirectory, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            yield return parentDirectory;
        }
    }

    private static IEnumerable<string> MapAgentStagedSkillDirectories(
        string candidateDirectory,
        IReadOnlyList<string>? discoveryDirectories)
    {
        if (discoveryDirectories is not { Count: > 0 })
            yield break;

        var relativePath = TryExtractAgentStagedRelativePath(candidateDirectory);
        if (string.IsNullOrWhiteSpace(relativePath))
            yield break;

        foreach (var discoveryDirectory in discoveryDirectories)
        {
            var mappedDirectory = NormalizePath(Path.Combine(discoveryDirectory, relativePath));
            if (mappedDirectory is not null)
                yield return mappedDirectory;
        }
    }

    private static string? TryExtractAgentStagedRelativePath(string path)
    {
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var marker = $"{Path.DirectorySeparatorChar}.agents{Path.DirectorySeparatorChar}skills{Path.DirectorySeparatorChar}";
        var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;

        var relativeStartIndex = markerIndex + marker.Length;
        if (relativeStartIndex >= normalized.Length)
            return null;

        return normalized[relativeStartIndex..];
    }

    private static Dictionary<string, object?> NormalizeArguments(object? value)
    {
        if (value is null)
            return [];

        if (value is Dictionary<string, object?> dictionary)
            return new Dictionary<string, object?>(dictionary, StringComparer.Ordinal);

        if (value is Dictionary<string, object> nonNullableDictionary)
        {
            return nonNullableDictionary.ToDictionary(
                pair => pair.Key,
                pair => (object?)pair.Value,
                StringComparer.Ordinal);
        }

        if (value is IDictionary<string, object?> dictionaryWithNullableValues)
        {
            return dictionaryWithNullableValues.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
        }

        var json = JsonSerializer.SerializeToElement(value);
        if (json.ValueKind == JsonValueKind.Object)
        {
            return json.EnumerateObject().ToDictionary(
                property => property.Name,
                property => (object?)property.Value.Clone(),
                StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["value"] = json.Clone()
        };
    }

    private static int? ToNullableInt(double? value)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return null;

        return Convert.ToInt32(Math.Round(value.Value, MidpointRounding.AwayFromZero));
    }

    private static async Task<(List<UserMessageDataAttachmentsItem>? Attachments, string? TempDir)>
        ProcessDataContentAttachmentsAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken)
    {
        List<UserMessageDataAttachmentsItem>? attachments = null;
        string? tempDir = null;

        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is not DataContent dataContent)
                    continue;

                tempDir ??= Directory.CreateDirectory(
                    Path.Combine(Path.GetTempPath(), $"af_copilot_{Guid.NewGuid():N}")).FullName;

                var path = await dataContent.SaveToAsync(tempDir, cancellationToken).ConfigureAwait(false);
                attachments ??= [];
                attachments.Add(new UserMessageDataAttachmentsItemFile
                {
                    Path = path,
                    DisplayName = Path.GetFileName(path)
                });
            }
        }

        return (attachments, tempDir);
    }

    private static void CleanupTempDir(string? tempDir)
    {
        if (tempDir is null)
            return;

        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
        }
    }

    private static SessionConfig CloneSessionConfig(SessionConfig source)
    {
        return new SessionConfig
        {
            AvailableTools = source.AvailableTools is not null ? [.. source.AvailableTools] : null,
            ClientName = source.ClientName,
            Commands = source.Commands is not null ? [.. source.Commands] : null,
            ConfigDir = source.ConfigDir,
            CustomAgents = source.CustomAgents is not null ? [.. source.CustomAgents] : null,
            Agent = source.Agent,
            DisabledSkills = source.DisabledSkills is not null ? [.. source.DisabledSkills] : null,
            ExcludedTools = source.ExcludedTools is not null ? [.. source.ExcludedTools] : null,
            Hooks = source.Hooks,
            InfiniteSessions = source.InfiniteSessions,
            McpServers = source.McpServers is not null
                ? new Dictionary<string, object>(source.McpServers, source.McpServers.Comparer)
                : null,
            Model = source.Model,
            OnElicitationRequest = source.OnElicitationRequest,
            OnEvent = source.OnEvent,
            OnPermissionRequest = source.OnPermissionRequest,
            OnUserInputRequest = source.OnUserInputRequest,
            Provider = source.Provider,
            ReasoningEffort = source.ReasoningEffort,
            SessionId = source.SessionId,
            SkillDirectories = source.SkillDirectories is not null ? [.. source.SkillDirectories] : null,
            Streaming = source.Streaming,
            SystemMessage = source.SystemMessage,
            Tools = source.Tools is not null ? [.. source.Tools] : null,
            WorkingDirectory = source.WorkingDirectory
        };
    }

    private static ResumeSessionConfig CloneResumeSessionConfig(SessionConfig source)
    {
        return new ResumeSessionConfig
        {
            AvailableTools = source.AvailableTools is not null ? [.. source.AvailableTools] : null,
            ClientName = source.ClientName,
            Commands = source.Commands is not null ? [.. source.Commands] : null,
            ConfigDir = source.ConfigDir,
            CustomAgents = source.CustomAgents is not null ? [.. source.CustomAgents] : null,
            Agent = source.Agent,
            DisabledSkills = source.DisabledSkills is not null ? [.. source.DisabledSkills] : null,
            ExcludedTools = source.ExcludedTools is not null ? [.. source.ExcludedTools] : null,
            Hooks = source.Hooks,
            InfiniteSessions = source.InfiniteSessions,
            McpServers = source.McpServers is not null
                ? new Dictionary<string, object>(source.McpServers, source.McpServers.Comparer)
                : null,
            Model = source.Model,
            OnElicitationRequest = source.OnElicitationRequest,
            OnEvent = source.OnEvent,
            OnPermissionRequest = source.OnPermissionRequest,
            OnUserInputRequest = source.OnUserInputRequest,
            Provider = source.Provider,
            ReasoningEffort = source.ReasoningEffort,
            SkillDirectories = source.SkillDirectories is not null ? [.. source.SkillDirectories] : null,
            Streaming = source.Streaming,
            SystemMessage = source.SystemMessage,
            Tools = source.Tools is not null ? [.. source.Tools] : null,
            WorkingDirectory = source.WorkingDirectory
        };
    }

    private static ExecutionOverrides ExtractExecutionOverrides(AgentRunOptions? options)
    {
        var chatOptions = (options as ChatClientAgentRunOptions)?.ChatOptions;
        var tools = chatOptions?.Tools?.OfType<AIFunction>().ToList() ?? [];
        var reasoningEffort = chatOptions?.Reasoning?.Effort is { } effort
            ? effort.ToString().ToLowerInvariant()
            : null;

        return new ExecutionOverrides(tools, reasoningEffort);
    }

    private static void ApplyExecutionOverrides(SessionConfig config, ExecutionOverrides overrides)
    {
        config.Tools = MergeTools(config.Tools, overrides.Tools);
        if (!string.IsNullOrWhiteSpace(overrides.ReasoningEffort))
            config.ReasoningEffort = overrides.ReasoningEffort;
    }

    private static void ApplyExecutionOverrides(ResumeSessionConfig config, ExecutionOverrides overrides)
    {
        config.Tools = MergeTools(config.Tools, overrides.Tools);
        if (!string.IsNullOrWhiteSpace(overrides.ReasoningEffort))
            config.ReasoningEffort = overrides.ReasoningEffort;
    }

    private static ICollection<AIFunction>? MergeTools(
        ICollection<AIFunction>? existingTools,
        IReadOnlyCollection<AIFunction> additionalTools)
    {
        if (additionalTools.Count == 0)
            return existingTools;

        var merged = existingTools is { Count: > 0 }
            ? existingTools.ToList()
            : new List<AIFunction>();
        foreach (var tool in additionalTools)
            merged.Add(tool);

        return merged;
    }

    private sealed record ExecutionOverrides(
        IReadOnlyCollection<AIFunction> Tools,
        string? ReasoningEffort);
}

internal sealed class TraceProjectionState
{
    public HashSet<string> MessageIdsWithDelta { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ReasoningIdsWithDelta { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> ToolNamesByCallId { get; } = new(StringComparer.Ordinal);

    public bool SawAnyReasoning { get; set; }
}
