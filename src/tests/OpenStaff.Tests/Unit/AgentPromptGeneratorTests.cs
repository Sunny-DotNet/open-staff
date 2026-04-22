using Microsoft.Extensions.DependencyInjection;
using OpenStaff;
using OpenStaff.AgentSouls.Dtos;
using OpenStaff.AgentSouls.Services;
using OpenStaff.ApiServices;
using OpenStaff.Application.Contracts.Settings;
using OpenStaff.Core.Agents;
using OpenStaff.Dtos;
using OpenStaff.Entities;

namespace OpenStaff.Tests.Unit;

public class AgentPromptGeneratorTests
{
    /// <summary>
    /// zh-CN: 验证提示词构造器会合并内置角色提示、全局系统设置以及当前场景说明。
    /// en: Verifies the prompt generator merges the builtin role prompt, global system settings, and current scene instructions.
    /// </summary>
    [Fact]
    public async Task PromptBuildAsync_IncludesBuiltinPromptAndGlobalSettings()
    {
        using var services = new ServiceCollection()
            .AddScoped<ISettingsApiService>(_ => new FakeSettingsApiService())
            .BuildServiceProvider();

        var generatorType = typeof(IAgentPromptGenerator).Assembly.GetType("OpenStaff.AgentPromptGenerator", throwOnError: true)!;
        var generator = (IAgentPromptGenerator)Activator.CreateInstance(
            generatorType,
            services.GetRequiredService<IServiceScopeFactory>())!;

        var role = new AgentRole
        {
            IsBuiltin = true,
            Source = AgentSource.Builtin,
            JobTitle = BuiltinRoleTypes.Secretary,
            Name = "秘书",
            Description = "你是项目秘书，负责组织和协调。"
        };

        var prompt = await generator.PromptBuildAsync(
            role,
            new AgentContext
            {
                Role = role,
                Scene = SceneType.Test
            },
            CancellationToken.None);

        Assert.Contains("Team Name", prompt);
        Assert.Contains("OpenStaff", prompt);
        Assert.Contains("How you address users", prompt);
        Assert.Contains("主人", prompt);
        Assert.Contains("你是项目秘书，负责组织和协调。", prompt);
        Assert.Contains("You are in a direct 1:1 conversation with the user.", prompt);
    }

    /// <summary>
    /// zh-CN: 验证只要存在项目上下文，提示词就会稳定注入项目成员说明，而不是只在某个群聊分支里偶然出现。
    /// en: Verifies project member summaries are injected for any project-scoped context instead of only appearing in a specific group-chat branch.
    /// </summary>
    [Fact]
    public async Task PromptBuildAsync_WithProjectContext_IncludesProjectMemberSummaries()
    {
        using var services = new ServiceCollection()
            .AddScoped<ISettingsApiService>(_ => new FakeSettingsApiService())
            .BuildServiceProvider();

        var generatorType = typeof(IAgentPromptGenerator).Assembly.GetType("OpenStaff.AgentPromptGenerator", throwOnError: true)!;
        var generator = (IAgentPromptGenerator)Activator.CreateInstance(
            generatorType,
            services.GetRequiredService<IServiceScopeFactory>())!;

        var secretaryRole = new AgentRole
        {
            IsBuiltin = true,
            Source = AgentSource.Builtin,
            JobTitle = "项目秘书",
            Name = "Monica",
            Description = "负责承接用户诉求、协调团队协作并汇总结果。"
        };

        var producerRole = new AgentRole
        {
            Name = "Ada",
            JobTitle = "开发工程师",
            Description = "负责编码实现、修改文件并交付可运行结果。"
        };

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "OpenStaff",
            Status = ProjectStatus.Active,
            Phase = ProjectPhases.Running,
            Description = "多智能体协作开发平台",
            AgentRoles =
            [
                new ProjectAgentRole
                {
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Status = AgentStatus.Working,
                    CurrentTask = "整理用户需求并同步给团队",
                    AgentRole = secretaryRole
                },
                new ProjectAgentRole
                {
                    CreatedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    Status = AgentStatus.Idle,
                    CurrentTask = "等待秘书派单",
                    AgentRole = producerRole
                }
            ]
        };

        var prompt = await generator.PromptBuildAsync(
            secretaryRole,
            new AgentContext
            {
                ProjectId = project.Id,
                Project = project,
                Role = secretaryRole,
                Scene = SceneType.Private
            },
            CancellationToken.None);

        Assert.Contains("Project Team Summary", prompt);
        Assert.Contains("Name=Monica", prompt);
        Assert.Contains("Job=Secretary", prompt);
        Assert.Contains("Role Description=负责承接用户诉求、协调团队协作并汇总结果。", prompt);
        Assert.Contains("Current Task=整理用户需求并同步给团队", prompt);
        Assert.Contains("Name=Ada", prompt);
        Assert.Contains("Job=Software Engineer", prompt);
        Assert.Contains("Role Description=负责编码实现、修改文件并交付可运行结果。", prompt);
        Assert.Contains("Current Task=等待秘书派单", prompt);
    }

    /// <summary>
    /// zh-CN: 验证群聊场景都会带上“最精简、模拟聊天回复”的统一约束，防止团队群聊和项目群聊出现风格漂移。
    /// en: Verifies group-chat scenes share the same concise chat-style guidance so team and project groups do not drift apart.
    /// </summary>
    [Theory]
    [InlineData(SceneType.TeamGroup)]
    [InlineData(SceneType.ProjectGroup)]
    public async Task PromptBuildAsync_GroupScenes_RequireConciseChatStyleReplies(SceneType scene)
    {
        using var services = new ServiceCollection()
            .AddScoped<ISettingsApiService>(_ => new FakeSettingsApiService())
            .BuildServiceProvider();

        var generatorType = typeof(IAgentPromptGenerator).Assembly.GetType("OpenStaff.AgentPromptGenerator", throwOnError: true)!;
        var generator = (IAgentPromptGenerator)Activator.CreateInstance(
            generatorType,
            services.GetRequiredService<IServiceScopeFactory>())!;

        var role = new AgentRole
        {
            Name = "Monica",
            JobTitle = "项目秘书",
            Description = "负责承接用户诉求并协调团队。"
        };

        var context = new AgentContext
        {
            Role = role,
            Scene = scene
        };
        if (scene == SceneType.ProjectGroup)
        {
            context.ProjectId = Guid.NewGuid();
            context.Project = new Project { Id = context.ProjectId.Value, Name = "OpenStaff" };
        }

        var prompt = await generator.PromptBuildAsync(role, context, CancellationToken.None);

        Assert.Contains("Group-chat replies must stay concise", prompt);
        Assert.Contains("feel like real short chat messages", prompt);
        Assert.Contains("Prefer a direct conclusion, confirmation, question, or blocker.", prompt);
    }

    [Fact]
    public async Task PromptBuildAsync_ProjectGroupSecretary_IncludesDispatchProtocolAndRoutingContext()
    {
        using var services = new ServiceCollection()
            .AddScoped<ISettingsApiService>(_ => new FakeSettingsApiService())
            .BuildServiceProvider();

        var generatorType = typeof(IAgentPromptGenerator).Assembly.GetType("OpenStaff.AgentPromptGenerator", throwOnError: true)!;
        var generator = (IAgentPromptGenerator)Activator.CreateInstance(
            generatorType,
            services.GetRequiredService<IServiceScopeFactory>())!;

        var role = new AgentRole
        {
            IsBuiltin = true,
            Source = AgentSource.Builtin,
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Description = "负责承接用户诉求并协调团队。"
        };

        var prompt = await generator.PromptBuildAsync(
            role,
            new AgentContext
            {
                Role = role,
                Scene = SceneType.ProjectGroup,
                ProjectId = Guid.NewGuid(),
                Project = new Project { Name = "OpenStaff" },
                ExtraConfig = new Dictionary<string, object>
                {
                    ["openstaff_original_input"] = "@Monica 开工",
                    ["openstaff_execution_purpose"] = "开工",
                    ["openstaff_target_role"] = "secretary",
                    ["openstaff_initiator_role"] = "user",
                    ["openstaff_dispatch_source"] = "project_group_mention",
                    ["openstaff_dispatch_context"] = "The user mentioned you directly in the project group, so this task should be handled by you first."
                }
            },
            CancellationToken.None);

        Assert.Contains("Original group message: @Monica 开工", prompt);
        Assert.Contains("Normalized execution task: 开工", prompt);
        Assert.Contains("Dispatch source: project_group_mention", prompt);
        Assert.Contains("Why you received this task: The user mentioned you directly in the project group, so this task should be handled by you first.", prompt);
        Assert.Contains("<openstaff_project_dispatch>", prompt);
    }

    [Fact]
    public async Task PromptBuildAsync_ProjectGroupMember_IncludesDispatchProtocolAndContext()
    {
        using var services = new ServiceCollection()
            .AddScoped<ISettingsApiService>(_ => new FakeSettingsApiService())
            .BuildServiceProvider();

        var generatorType = typeof(IAgentPromptGenerator).Assembly.GetType("OpenStaff.AgentPromptGenerator", throwOnError: true)!;
        var generator = (IAgentPromptGenerator)Activator.CreateInstance(
            generatorType,
            services.GetRequiredService<IServiceScopeFactory>())!;

        var role = new AgentRole
        {
            Name = "Ada",
            JobTitle = "producer",
            Description = "负责执行编码任务。"
        };

        var prompt = await generator.PromptBuildAsync(
            role,
            new AgentContext
            {
                Role = role,
                Scene = SceneType.ProjectGroup,
                ProjectId = Guid.NewGuid(),
                Project = new Project { Name = "OpenStaff" },
                ExtraConfig = new Dictionary<string, object>
                {
                    ["openstaff_original_input"] = "请补上接口实现",
                    ["openstaff_execution_purpose"] = "请补上接口实现",
                    ["openstaff_target_role"] = "producer",
                    ["openstaff_initiator_role"] = "secretary",
                    ["openstaff_dispatch_source"] = "project_group_secretary_dispatch",
                    ["openstaff_dispatch_context"] = "The secretary reviewed the group context and handed the next step to you."
                }
            },
            CancellationToken.None);

        Assert.Contains("Dispatch source: project_group_secretary_dispatch", prompt);
        Assert.Contains("Why you received this task: The secretary reviewed the group context and handed the next step to you.", prompt);
        Assert.Contains("<openstaff_project_dispatch>", prompt);
        Assert.Contains("<openstaff_capability_request>", prompt);
        Assert.Contains("Do not emit both a dispatch block and a capability-request block in the same reply.", prompt);
    }

    [Fact]
    public async Task PromptBuildAsync_SoulValues_RenderEnglishAliases()
    {
        using var services = new ServiceCollection()
            .AddScoped<ISettingsApiService>(_ => new FakeSettingsApiService())
            .AddScoped<IAgentSoulService>(_ => new FakeAgentSoulService(
                personalityTraits: new FakeAgentSoulHttpService(
                [
                    new AgentSoulValue("adaptable", new Dictionary<string, string>
                    {
                        ["en"] = "Adaptable",
                        ["zh"] = "适应力强的"
                    })
                ]),
                communicationStyles: new FakeAgentSoulHttpService(
                [
                    new AgentSoulValue("formal", new Dictionary<string, string>
                    {
                        ["en"] = "Formal",
                        ["zh"] = "正式严谨的"
                    })
                ]),
                workAttitudes: new FakeAgentSoulHttpService(
                [
                    new AgentSoulValue("collaborative", new Dictionary<string, string>
                    {
                        ["en"] = "Collaborative",
                        ["zh"] = "注重协作的"
                    })
                ])))
            .BuildServiceProvider();

        var generatorType = typeof(IAgentPromptGenerator).Assembly.GetType("OpenStaff.AgentPromptGenerator", throwOnError: true)!;
        var generator = (IAgentPromptGenerator)Activator.CreateInstance(
            generatorType,
            services.GetRequiredService<IServiceScopeFactory>())!;

        var role = new AgentRole
        {
            Name = "Monica",
            JobTitle = "architect",
            Description = "负责方案设计。",
            Soul = new AgentSoul
            {
                Traits = ["适应力强的"],
                Style = "formal",
                Attitudes = ["注重协作的"],
                Custom = "保持专业"
            }
        };

        var prompt = await generator.PromptBuildAsync(
            role,
            new AgentContext
            {
                Role = role,
                Scene = SceneType.Test
            },
            CancellationToken.None);

        Assert.Contains("Job:\tArchitect", prompt);
        Assert.Contains("Communication Style:\tFormal", prompt);
        Assert.Contains("Personality Traits:\tAdaptable", prompt);
        Assert.Contains("Work Attitude:\tCollaborative", prompt);
        Assert.Contains("Other:\t保持专业", prompt);
    }

    private sealed class FakeSettingsApiService : ISettingsApiService
    {
        /// <summary>
        /// zh-CN: 返回固定的全局设置，让测试只聚焦于提示词拼装而不是设置存储细节。
        /// en: Returns fixed global settings so the test focuses on prompt composition instead of settings storage details.
        /// </summary>
        public Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, string>
            {
                [SystemSettingsKeys.TeamName] = "OpenStaff",
                [SystemSettingsKeys.UserName] = "主人",
                [SystemSettingsKeys.Language] = "zh-CN",
                [SystemSettingsKeys.Timezone] = "Asia/Shanghai"
            });

        /// <summary>
        /// zh-CN: 此测试替身只支持读取；若代码意外尝试写入设置，应立即失败以暴露错误路径。
        /// en: This test double only supports reads; any unexpected settings write should fail fast to expose the wrong path.
        /// </summary>
        public Task UpdateAsync(Dictionary<string, string> settings, CancellationToken ct = default) =>
            throw new NotSupportedException();

        /// <summary>
        /// zh-CN: 提供最小系统设置 DTO 以满足接口契约，而当前用例并不依赖系统设置详情。
        /// en: Supplies a minimal system settings DTO to satisfy the interface because this test does not depend on system-setting details.
        /// </summary>
        public Task<SystemSettingsDto> GetSystemAsync(CancellationToken ct = default) =>
            Task.FromResult(new SystemSettingsDto());

        /// <summary>
        /// zh-CN: 与普通设置更新一样，此替身不实现系统设置写入，避免测试默默走到无关分支。
        /// en: Like regular settings updates, this double does not implement system-setting writes so unrelated branches cannot pass silently.
        /// </summary>
        public Task UpdateSystemAsync(SystemSettingsDto dto, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAgentSoulService : IAgentSoulService
    {
        public FakeAgentSoulService(
            IAgentSoulHttpService personalityTraits,
            IAgentSoulHttpService communicationStyles,
            IAgentSoulHttpService workAttitudes)
        {
            PersonalityTraits = personalityTraits;
            CommunicationStyles = communicationStyles;
            WorkAttitudes = workAttitudes;
        }

        public IAgentSoulHttpService CommunicationStyles { get; }

        public IAgentSoulHttpService PersonalityTraits { get; }

        public IAgentSoulHttpService WorkAttitudes { get; }
    }

    private sealed class FakeAgentSoulHttpService : IAgentSoulHttpService
    {
        private readonly IReadOnlyCollection<AgentSoulValue> _values;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _aliases;

        public FakeAgentSoulHttpService(IReadOnlyCollection<AgentSoulValue> values)
        {
            _values = values;
            _aliases = values.ToDictionary(
                item => item.Key,
                item => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(item.Aliases, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }

        public string DefaultAliasName => "en";

        public Task<IReadOnlyCollection<AgentSoulValue>> GetAllAsync() => Task.FromResult(_values);

        public Task<IReadOnlyDictionary<string, string>> GetAllByLocaleAsync(string? locale = null)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<string> GetAsync(string key, string? locale = null)
        {
            if (!_aliases.TryGetValue(key, out var aliases))
                throw new KeyNotFoundException(key);

            if (!string.IsNullOrWhiteSpace(locale) && aliases.TryGetValue(locale!, out var localized))
                return Task.FromResult(localized);

            var separatorIndex = locale?.IndexOfAny(['-', '_']) ?? -1;
            if (separatorIndex > 0 && aliases.TryGetValue(locale![..separatorIndex], out localized))
                return Task.FromResult(localized);

            if (aliases.TryGetValue(DefaultAliasName, out var fallback))
                return Task.FromResult(fallback);

            throw new KeyNotFoundException(key);
        }
    }
}

