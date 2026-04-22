using OpenStaff.Core.Agents;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

public readonly record struct GitHubCopilotConversationIdentity(
    string CopilotSessionId,
    bool IsTemporary)
{
    public static GitHubCopilotConversationIdentity Create(AgentContext context)
    {
        if (context.SessionId.HasValue)
        {
            return new(
                CopilotSessionId: BuildSessionId("session", context.SessionId.Value, context.AgentInstanceId),
                IsTemporary: context.Scene == SceneType.Test && !context.ProjectId.HasValue);
        }

        if (context.ProjectId.HasValue)
        {
            return new(
                CopilotSessionId: BuildSessionId("project", context.ProjectId.Value, context.AgentInstanceId),
                IsTemporary: false);
        }

        return new(
            CopilotSessionId: BuildSessionId("transient", Guid.NewGuid(), context.AgentInstanceId),
            IsTemporary: true);
    }

    private static string BuildSessionId(string scope, Guid scopeId, Guid agentInstanceId)
        => $"openstaff-{scope}-{scopeId:N}-agent-{agentInstanceId:N}";
}
