using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.AgentSouls.Services;
using OpenStaff.Application.Contracts.Settings;
using OpenStaff.ApiServices;
using OpenStaff.Core.Agents;
using OpenStaff.Dtos;
using OpenStaff.Entities;

namespace OpenStaff;

/// <summary>
/// zh-CN: 定义运行时系统提示词生成器的统一接口。
/// en: Defines the unified contract for runtime system-prompt generation.
/// </summary>
public interface IAgentPromptGenerator
{
    /// <summary>
    /// zh-CN: 构建角色在当前上下文下应使用的完整系统提示词。
    /// en: Builds the complete system prompt that a role should use for the current context.
    /// </summary>
    Task<string> PromptBuildAsync(AgentRole agentRole, AgentContext agentContext, CancellationToken cancellationToken);
}

/// <summary>
/// zh-CN: 组装多层系统提示词，并在单例生命周期下安全读取作用域服务。
/// en: Composes layered system prompts and safely reads scoped services from a singleton lifetime.
/// </summary>
internal class AgentPromptGenerator : IAgentPromptGenerator
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// zh-CN: 使用作用域工厂初始化提示词生成器，以便在单例生命周期中安全读取 Scoped 服务。
    /// en: Initializes the prompt generator with a scope factory so scoped services can be read safely from a singleton lifetime.
    /// </summary>
    public AgentPromptGenerator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// zh-CN: 按“全局 → 项目 → 角色 → 场景”的顺序组装完整提示词。
    /// en: Builds the full prompt in the order global -> project -> role -> scene.
    /// </summary>
    public async Task<string> PromptBuildAsync(AgentRole agentRole, AgentContext agentContext, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        // zh-CN: 提示词层级固定叠加，后面的层可以基于前面的上下文进一步收窄行为。
        // en: Prompt layers are applied in a fixed order so later layers can refine behavior using earlier context.
        builder.AppendLine(await BuildGlobalPromptAsync());
        builder.AppendLine();

        if (agentContext.ProjectId.HasValue)
        {
            builder.AppendLine(BuildProjectPrompt(agentContext.Project));
            builder.AppendLine();
        }

        builder.AppendLine(await BuildRolePromptAsync(agentRole));
        builder.AppendLine();
        AppendRoleInstructions(builder, agentRole);

        if (agentContext.Scene.HasValue)
        {
            builder.AppendLine(BuildScenePrompt(agentContext));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// zh-CN: 每次构建都重新读取全局设置，确保提示词反映最新配置。
    /// en: Reloads global settings for each build so prompts reflect the latest configuration.
    /// </summary>
    private async Task<string> BuildGlobalPromptAsync()
    {
        // zh-CN: 生成器是单例，因此这里显式创建 scope 来读取 Scoped 的设置服务。
        // en: The generator is a singleton, so it creates an explicit scope to read the scoped settings service.
        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsApiService>();
        var kv = await settingsService.GetAllAsync();

        var builder = new StringBuilder();
        builder.AppendLine("--Global Configuration--");
        builder.AppendLine("(You are now a member of a team, and you will work with countless other agents to assist users in solving problems.)");

        var index = 1;
        if (kv.TryGetValue(SystemSettingsKeys.TeamName, out var teamName))
            builder.AppendLine($"{index++}.Team Name:\t{teamName}");
        if (kv.TryGetValue(SystemSettingsKeys.TeamDescription, out var teamDescription) && !string.IsNullOrWhiteSpace(teamDescription))
            builder.AppendLine($"{index++}.Team Background:\t{teamDescription}");
        if (kv.TryGetValue(SystemSettingsKeys.Language, out var language))
            builder.AppendLine($"{index++}.Global Context Dialogue Language:\t{language}(Please use this language to communicate.)");
        if (kv.TryGetValue(SystemSettingsKeys.Timezone, out var timezone))
            builder.AppendLine($"{index++}.Global Context Dialogue Time Zone:\t{timezone}");
        if (kv.TryGetValue(SystemSettingsKeys.UserName, out var userName))
            builder.AppendLine($"{index++}.How you address users:\t{userName}");

        return builder.ToString();
    }

    /// <summary>
    /// zh-CN: 仅提取项目中已填写的核心信息，生成供运行时拼接的项目配置片段。
    /// en: Builds the project configuration block from only the populated project fields so the runtime prompt stays concise.
    /// </summary>
    private static string BuildProjectPrompt(Project project)
    {
        var builder = new StringBuilder();
        builder.AppendLine("--Project Configuration--");
        builder.AppendLine("(You have now been assigned to a project and become a member of this project team.)");

        var index = 1;
        if (!string.IsNullOrWhiteSpace(project.Name))
            builder.AppendLine($"{index++}.Project Name:\t{project.Name}");
        if (!string.IsNullOrWhiteSpace(project.Status))
            builder.AppendLine($"{index++}.Project Status:\t{project.Status}");
        if (!string.IsNullOrWhiteSpace(project.Phase))
            builder.AppendLine($"{index++}.Project Stage:\t{project.Phase}");
        if (!string.IsNullOrWhiteSpace(project.Description))
            builder.AppendLine($"{index++}.Project Description:\t{project.Description}");
        // 把项目成员说明固定挂在项目层，而不是散落在某个场景分支里，
        // 这样项目群聊、项目私聊等所有项目上下文都会拿到同一份团队画像。
        index = AppendProjectAgents(builder, project, index);

        return builder.ToString();
    }

    /// <summary>
    /// zh-CN: 将角色身份信息与灵魂配置整理成基础角色说明，供后续指令层叠加。
    /// en: Converts role identity and soul settings into the base role-description block that later prompt layers build on.
    /// </summary>
    private async Task<string> BuildRolePromptAsync(AgentRole role)
    {
        var builder = new StringBuilder();
        builder.AppendLine("--Role Description--");
        builder.AppendLine("(Below is your role description)");

        var index = 1;
        if (!string.IsNullOrWhiteSpace(role.Name))
            builder.AppendLine($"{index++}.Name:\t{role.Name}");
        var englishJobTitle = AgentJobTitleCatalog.ToEnglish(role.JobTitle);
        if (!string.IsNullOrWhiteSpace(englishJobTitle))
            builder.AppendLine($"{index++}.Job:\t{englishJobTitle}");
        if (!string.IsNullOrWhiteSpace(role.Description))
            builder.AppendLine($"{index++}.Role Description:\t{role.Description}");

        var soul = await ResolvePromptSoulAsync(role.Soul);
        if (soul is not null)
        {
            if (!string.IsNullOrWhiteSpace(soul.Style))
                builder.AppendLine($"{index++}.Communication Style:\t{soul.Style}");
            if (soul.Traits.Count > 0)
                builder.AppendLine($"{index++}.Personality Traits:\t{string.Join(", ", soul.Traits)}");
            if (soul.Attitudes.Count > 0)
                builder.AppendLine($"{index++}.Work Attitude:\t{string.Join(", ", soul.Attitudes)}");
            if (!string.IsNullOrWhiteSpace(soul.Custom))
                builder.AppendLine($"{index++}.Other:\t{soul.Custom}");
        }

        return builder.ToString();
    }

    private async Task<PromptSoulValues?> ResolvePromptSoulAsync(AgentSoul? soul)
    {
        if (soul is null)
            return null;

        using var scope = _scopeFactory.CreateScope();
        var soulService = scope.ServiceProvider.GetService<IAgentSoulService>();
        if (soulService is null)
        {
            return new PromptSoulValues(
                ResolvePromptValues(soul.Traits),
                NormalizePromptValue(soul.Style),
                ResolvePromptValues(soul.Attitudes),
                NormalizePromptValue(soul.Custom));
        }

        return new PromptSoulValues(
            await ResolvePromptValuesAsync(soul.Traits, soulService.PersonalityTraits),
            await soulService.CommunicationStyles.ResolveAliasAsync(soul.Style, "en"),
            await ResolvePromptValuesAsync(soul.Attitudes, soulService.WorkAttitudes),
            NormalizePromptValue(soul.Custom));
    }

    private static List<string> ResolvePromptValues(IEnumerable<string>? values)
        => values?
            .Select(NormalizePromptValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList()
           ?? [];

    private static async Task<List<string>> ResolvePromptValuesAsync(
        IEnumerable<string>? values,
        IAgentSoulHttpService service)
    {
        var results = new List<string>();
        if (values is null)
            return results;

        foreach (var value in values)
        {
            var resolved = await service.ResolveAliasAsync(value, "en");
            if (!string.IsNullOrWhiteSpace(resolved))
                results.Add(resolved);
        }

        return results;
    }

    private static string? NormalizePromptValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record PromptSoulValues(
        List<string> Traits,
        string? Style,
        List<string> Attitudes,
        string? Custom);

    /// <summary>
    /// zh-CN: 仅为内置来源的角色追加嵌入式系统指令，避免把无关模板错误附加到外部角色。
    /// en: Appends embedded system instructions only for builtin-sourced roles so unrelated templates are not attached to external roles.
    /// </summary>
    private static void AppendRoleInstructions(StringBuilder builder, AgentRole role)
    {
        // zh-CN: 只有内置角色或来源标记为 Builtin 的角色才会拼接嵌入式内置指令。
        // en: Only builtin roles, or roles explicitly sourced from builtin, receive embedded builtin instructions.
        if (!role.IsBuiltin && role.Source != AgentSource.Builtin)
            return;
        if (!string.IsNullOrWhiteSpace(role.Description))
        {
            builder.AppendLine("--Builtin Role Instructions--");
            builder.AppendLine("(The following instructions are embedded for this builtin role)");
            builder.AppendLine(role.Description.Trim());
            builder.AppendLine();
        }
    }

    /// <summary>
    /// zh-CN: 根据运行场景追加操作约束与结构化输出协议，调用方需先保证 <see cref="AgentContext.Scene" /> 已解析。
    /// en: Adds scene-specific operating rules and structured-output contracts; callers must ensure <see cref="AgentContext.Scene" /> has already been resolved.
    /// </summary>
    private static string BuildScenePrompt(AgentContext agentContext)
    {
        var scene = agentContext.Scene!.Value;
        var builder = new StringBuilder();
        builder.AppendLine("--Scenario Description--");
        builder.AppendLine("(The following is a description of the dialogue scenario)");

        switch (scene)
        {
            case SceneType.Test:
                builder.AppendLine("\tYou are in a direct 1:1 conversation with the user.");
                builder.AppendLine("\tThe user is testing the interaction quality, which may include runtime behavior, prompt quality, or environment stability.");
                break;
            case SceneType.TeamGroup:
                builder.AppendLine("\tYou are in the team group chat.");
                AppendGroupChatReplyStyle(builder);
                break;
            case SceneType.ProjectBrainstorm:
                builder.AppendLine("\tYou are in a direct project-brainstorm conversation with the user.");
                builder.AppendLine("\tYour goal is not casual chat. Your goal is to continuously refine and update .staff/project-brainstorm.md in the project workspace.");
                builder.AppendLine("\tStart each reply with natural-language feedback to the user, then append exactly one structured status block at the end.");
                builder.AppendLine("\tUse exactly the following status block format:");
                builder.AppendLine("\t<openstaff_brainstorm_state>");
                builder.AppendLine("\t{\"documentMarkdown\":\"The full Markdown content of .staff/project-brainstorm.md\",\"phase\":\"brainstorming or ready_to_start\"}");
                builder.AppendLine("\t</openstaff_brainstorm_state>");
                builder.AppendLine("\tdocumentMarkdown must contain the full document, not a diff and not a summary.");
                builder.AppendLine("\tSet phase to ready_to_start only when the requirements are clear enough for the user to click \"Start Project\". Otherwise keep brainstorming.");
                builder.AppendLine("\tDo not output JSON outside the structured status block.");
                AppendCurrentRequirementsDocument(builder, agentContext.Project);
                break;
            case SceneType.ProjectGroup:
                builder.AppendLine("\tYou are in the project team group chat.");
                builder.AppendLine("\tThis chat is for project execution. The user may assign work by mentioning you directly; otherwise the secretary usually receives the task first.");
                builder.AppendLine("\tFocus your reply on the current assignment and give a clear outcome, next step, or blocker.");
                builder.AppendLine("\tIf the task is complete, report the deliverable directly. If it is blocked or incomplete, say so explicitly and do not pretend it is done.");
                builder.AppendLine("\tOnly the concise result belongs in the group chat. Execution details are tracked separately by the system.");
                AppendGroupChatReplyStyle(builder);
                AppendProjectGroupRoutingContext(builder, agentContext);
                if (IsSecretaryRole(agentContext.Role))
                {
                    builder.AppendLine("\tYou are acting as the dispatching secretary for the project group chat.");
                    builder.AppendLine("\tWhen the user does not explicitly mention a member, reply in natural language first.");
                    builder.AppendLine("\tAppend a structured dispatch block only when follow-up execution should be assigned to a project member.");
                    AppendProjectDispatchContract(builder);
                    builder.AppendLine("\tOnly dispatch to members that are already assigned to this project. Do not invent role names.");
                    builder.AppendLine("\ttask must be the final task instruction for the target agent, not a note, summary, or simulated UI chat format.");
                }
                else
                {
                    builder.AppendLine("\tIf you have finished your current stage and another member must continue, send a short natural-language handoff first.");
                    builder.AppendLine("\tAppend a structured dispatch block only when a real handoff is needed. If you should continue the work yourself, do not emit it.");
                    AppendProjectDispatchContract(builder);
                    builder.AppendLine("\tOnly dispatch to members that are already assigned to this project. Do not invent role names.");
                    builder.AppendLine("\ttask must be the final task instruction for the target agent, not a note, summary, or simulated UI chat format.");
                    builder.AppendLine("\tIf you are blocked because you lack required tools, MCP connections, or permissions, explain the blocker in one short natural-language sentence first.");
                    AppendCapabilityRequestContract(builder);
                    builder.AppendLine("\tDo not emit both a dispatch block and a capability-request block in the same reply. Choose the single block that best matches the real execution state.");
                }
                break;
            case SceneType.Private:
                builder.AppendLine("\tYou are in a direct 1:1 conversation with the user.");
                break;
        }

        return builder.ToString();
    }

    /// <summary>
    /// zh-CN: 统一收口群聊回复风格，避免不同群聊场景对“短消息式回答”的约束出现漂移。
    /// en: Centralizes the group-chat reply style so different group scenes stay aligned on short chat-like responses.
    /// </summary>
    private static void AppendGroupChatReplyStyle(StringBuilder builder)
    {
        builder.AppendLine("\tGroup-chat replies must stay concise and feel like real short chat messages.");
        builder.AppendLine("\tPrefer a direct conclusion, confirmation, question, or blocker.");
        builder.AppendLine("\tKeep the reply within about 2 lines and 70 characters when the task allows.");
    }

    private static void AppendProjectGroupRoutingContext(StringBuilder builder, AgentContext agentContext)
    {
        var originalInput = GetExtraConfigValue(agentContext, "openstaff_original_input");
        var executionPurpose = GetExtraConfigValue(agentContext, "openstaff_execution_purpose");
        var targetRole = GetExtraConfigValue(agentContext, "openstaff_target_role");
        var initiatorRole = GetExtraConfigValue(agentContext, "openstaff_initiator_role");
        var dispatchSource = GetExtraConfigValue(agentContext, "openstaff_dispatch_source");
        var dispatchContext = GetExtraConfigValue(agentContext, "openstaff_dispatch_context");

        if (!string.IsNullOrWhiteSpace(originalInput))
        {
            builder.AppendLine($"\tOriginal group message: {originalInput}");
        }

        if (!string.IsNullOrWhiteSpace(executionPurpose) && !string.Equals(originalInput, executionPurpose, StringComparison.Ordinal))
        {
            builder.AppendLine($"\tNormalized execution task: {executionPurpose}");
        }

        if (!string.IsNullOrWhiteSpace(targetRole))
        {
            builder.AppendLine($"\tCurrent target role: {targetRole}");
        }

        if (!string.IsNullOrWhiteSpace(initiatorRole))
        {
            builder.AppendLine($"\tCurrent initiator role: {initiatorRole}");
        }

        if (!string.IsNullOrWhiteSpace(dispatchSource))
        {
            builder.AppendLine($"\tDispatch source: {dispatchSource}");
        }

        if (!string.IsNullOrWhiteSpace(dispatchContext))
        {
            builder.AppendLine($"\tWhy you received this task: {dispatchContext}");
        }
    }

    private static void AppendProjectDispatchContract(StringBuilder builder)
    {
        builder.AppendLine("\tUse exactly the following dispatch block format when you need to hand work to another member:");
        builder.AppendLine("\t<openstaff_project_dispatch>");
        builder.AppendLine("\t{\"dispatches\":[{\"targetRole\":\"producer\",\"task\":\"The final task instruction for the target agent\"}]}");
        builder.AppendLine("\t</openstaff_project_dispatch>");
        builder.AppendLine("\tDo not output JSON outside the structured dispatch block.");
    }

    private static void AppendCapabilityRequestContract(StringBuilder builder)
    {
        builder.AppendLine("\tUse exactly the following capability-request block format only when missing capabilities truly block execution:");
        builder.AppendLine("\t<openstaff_capability_request>");
        builder.AppendLine("\t{\"requiredTools\":[\"file_system\"],\"reason\":\"Need to create and edit project files\"}");
        builder.AppendLine("\t</openstaff_capability_request>");
        builder.AppendLine("\tDo not output JSON outside the structured capability-request block.");
    }

    private static string? GetExtraConfigValue(AgentContext agentContext, string key)
    {
        return agentContext.ExtraConfig.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;
    }

    private static bool IsSecretaryRole(AgentRole role)
    {
        return AgentJobTitleCatalog.IsSecretary(role.JobTitle)
            || AgentJobTitleCatalog.IsSecretary(role.Name);
    }

    /// <summary>
    /// zh-CN: 在头脑风暴场景下把当前 .staff/project-brainstorm.md 注入提示词，并限制长度以保护上下文窗口。
    /// en: Injects the current .staff/project-brainstorm.md into brainstorm prompts while trimming it to a safe size so it does not exhaust the context window.
    /// </summary>
    private static void AppendCurrentRequirementsDocument(StringBuilder builder, Project? project)
    {
        if (project == null || string.IsNullOrWhiteSpace(project.WorkspacePath))
            return;

        var readmePath = Path.Combine(project.WorkspacePath, ".staff", "project-brainstorm.md");
        if (!File.Exists(readmePath))
            return;

        var content = File.ReadAllText(readmePath);
        if (string.IsNullOrWhiteSpace(content))
            return;

        const int maxCharacters = 24_000;
        if (content.Length > maxCharacters)
        {
            // zh-CN: 将现有需求文档裁剪到安全长度，避免把整个上下文窗口都消耗在历史文档上。
            // en: Truncate the existing requirements document to a safe length so it does not consume the entire context window.
            content = content[..maxCharacters] + "\n\n<!-- TRUNCATED -->";
        }

        builder.AppendLine();
        builder.AppendLine("--Current .staff/project-brainstorm.md Document--");
        builder.AppendLine("(When you update documentMarkdown, produce a new complete version based on this content.)");
        builder.AppendLine("```md");
        builder.AppendLine(content);
        builder.AppendLine("```");
    }

    /// <summary>
    /// zh-CN: 把项目成员整理成精简摘要，帮助任意项目场景快速理解团队组成与当前分工。
    /// en: Renders a compact project-member summary so any project-scoped prompt can understand the team composition and current assignments.
    /// </summary>
    private static int AppendProjectAgents(StringBuilder builder, Project? project, int index)
    {
        var agents = project?.AgentRoles?
            .Where(agent => agent.AgentRole != null)
            .OrderBy(agent => agent.CreatedAt)
            .ToList();

        if (agents == null || agents.Count == 0)
            return index;

        builder.AppendLine($"{index++}.Project Team Summary:");
        foreach (var agent in agents)
        {
            builder.AppendLine($"\t- {BuildProjectAgentSummary(agent)}");
        }

        return index;
    }

    /// <summary>
    /// zh-CN: 将项目成员转换成单行摘要，既让模型知道“谁负责什么”，又避免把整份角色配置塞进上下文。
    /// en: Converts a project member into a one-line summary so the model learns who owns what without dumping the full role config into context.
    /// </summary>
    private static string BuildProjectAgentSummary(ProjectAgentRole agent)
    {
        var role = agent.AgentRole!;
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(role.Name))
            parts.Add($"Name={NormalizePromptField(role.Name, 80)}");
        var englishJobTitle = AgentJobTitleCatalog.ToEnglish(role.JobTitle);
        if (!string.IsNullOrWhiteSpace(englishJobTitle))
            parts.Add($"Job={NormalizePromptField(englishJobTitle, 80)}");
        if (!string.IsNullOrWhiteSpace(role.Description))
            parts.Add($"Role Description={NormalizePromptField(role.Description, 240)}");
        parts.Add($"Status={NormalizePromptField(agent.Status, 40)}");
        if (!string.IsNullOrWhiteSpace(agent.CurrentTask))
            parts.Add($"Current Task={NormalizePromptField(agent.CurrentTask, 160)}");

        return string.Join("; ", parts);
    }

    /// <summary>
    /// zh-CN: 把多行字段压成适合提示词的单行摘要，并限制长度，避免某个成员说明过长挤占上下文。
    /// en: Flattens multi-line fields into single-line prompt text and caps length so one member description cannot crowd out the rest of the context.
    /// </summary>
    private static string NormalizePromptField(string value, int maxLength)
    {
        var normalized = string.Join(" ", value
            .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (normalized.Length <= maxLength)
            return normalized;

        return normalized[..maxLength] + "...";
    }
}

