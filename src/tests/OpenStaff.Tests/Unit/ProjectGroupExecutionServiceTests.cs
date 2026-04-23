using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class ProjectGroupExecutionServiceTests
{
    /// <summary>
    /// zh-CN: 验证显式 @提及时，入口仍统一先回到隐藏项目模型，而不是直接短路到被提及成员。
    /// en: Verifies that explicit @mentions still route back to the hidden project orchestrator instead of short-circuiting straight to the mentioned member.
    /// </summary>
    [Fact]
    public async Task ResolveDispatchTargetAsync_WithMentionedProjectAgent_ReturnsOrchestratorFirstTarget()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");

        var result = await context.Service.ResolveDispatchTargetAsync(
            project.Id,
            "@Producer 帮我写后端 API",
            CancellationToken.None);

        Assert.Equal(BuiltinRoleTypes.Secretary, result.TargetRole);
        Assert.Equal("@Producer 帮我写后端 API", result.Purpose);
        Assert.Equal("@Producer 帮我写后端 API", result.UserMessageContent);
        Assert.True(result.HasExplicitMention);
        Assert.Null(result.ProjectAgentRoleId);
        Assert.Equal("Producer", result.MentionedTarget);
        Assert.Equal("project_group_user_input", result.DispatchSource);
    }

    /// <summary>
    /// zh-CN: 验证结构化 mentions 仍会保留下来作为项目模型的编排线索，但不再直接变成成员执行目标。
    /// en: Verifies structured mentions are preserved as orchestration hints for the hidden project model instead of becoming direct member execution targets.
    /// </summary>
    [Fact]
    public async Task ResolveDispatchTargetAsync_WithStructuredMention_PreservesNaturalInput()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Ada");

        var result = await context.Service.ResolveDispatchTargetAsync(
            project.Id,
            "@Ada 开工",
            [new ConversationMention("@Ada", "project_agent_role", ProjectAgentRoleId: projectAgent.Id)],
            CancellationToken.None);

        Assert.Equal(BuiltinRoleTypes.Secretary, result.TargetRole);
        Assert.Equal("@Ada 开工", result.Purpose);
        Assert.Equal("@Ada 开工", result.UserMessageContent);
        Assert.True(result.HasExplicitMention);
        Assert.Null(result.ProjectAgentRoleId);
        Assert.Equal("Ada", result.MentionedTarget);
        Assert.Equal("project_group_user_input", result.DispatchSource);
    }

    /// <summary>
    /// zh-CN: 验证秘书分发解析会把展示摘要与结构化调度指令分离，确保 UI 文本和真实调度载荷各自可用。
    /// en: Verifies that secretary-dispatch extraction separates the display summary from the structured dispatch payload so both UI text and actual routing instructions remain usable.
    /// </summary>
    [Fact]
    public void TryExtractSecretaryDispatch_ReturnsSummaryAndDispatches()
    {
        using var context = new TestContext();

        var extracted = context.Service.TryExtractSecretaryDispatch(
            "我来安排一下。\n\n<openstaff_project_dispatch>{\"dispatches\":[{\"targetRole\":\"producer\",\"task\":\"请编写后端 API\"}]}</openstaff_project_dispatch>",
            out var displayContent,
            out var dispatches);

        Assert.True(extracted);
        Assert.Equal("我来安排一下。", displayContent);
        var dispatch = Assert.Single(dispatches);
        Assert.Equal("producer", dispatch.TargetRole);
        Assert.Equal("请编写后端 API", dispatch.Task);
    }

    [Fact]
    public void TryExtractOrchestratorResult_WithNativeJson_ReturnsStructuredPlan()
    {
        using var context = new TestContext();

        var extracted = context.Service.TryExtractOrchestratorResult(
            "{\"replyMode\":\"dispatch_only\",\"dispatches\":[{\"targetRole\":\"Sophie\",\"task\":\"Reply in the visible project group with exactly this text and nothing else: backend dispatch ok 4\"},{\"targetRole\":\"圆圆\",\"task\":\"Reply in the visible project group with exactly this text and nothing else: review dispatch ok 4\"}]}",
            out var result);

        Assert.True(extracted);
        Assert.NotNull(result);
        Assert.Equal(ProjectGroupOrchestratorReplyMode.DispatchOnly, result!.ReplyMode);
        Assert.Collection(
            result.Dispatches,
            first =>
            {
                Assert.Equal("Sophie", first.TargetRole);
                Assert.Equal("Reply in the visible project group with exactly this text and nothing else: backend dispatch ok 4", first.Task);
            },
            second =>
            {
                Assert.Equal("圆圆", second.TargetRole);
                Assert.Equal("Reply in the visible project group with exactly this text and nothing else: review dispatch ok 4", second.Task);
            });
    }

    [Fact]
    public void TryExtractOrchestratorResult_WithTaggedFallback_ReturnsStructuredPlan()
    {
        using var context = new TestContext();

        var extracted = context.Service.TryExtractOrchestratorResult(
            "<openstaff_project_orchestrator_result>{\"replyMode\":\"secretary_reply_and_dispatch\",\"secretaryReply\":\"我来安排。\",\"dispatches\":[{\"targetRole\":\"producer\",\"task\":\"请补齐接口实现\"}]}</openstaff_project_orchestrator_result>",
            out var result);

        Assert.True(extracted);
        Assert.NotNull(result);
        Assert.Equal(ProjectGroupOrchestratorReplyMode.SecretaryReplyAndDispatch, result!.ReplyMode);
        Assert.Equal("我来安排。", result.SecretaryReply);
        var dispatch = Assert.Single(result.Dispatches);
        Assert.Equal("producer", dispatch.TargetRole);
        Assert.Equal("请补齐接口实现", dispatch.Task);
    }

    /// <summary>
    /// zh-CN: 验证秘书指定已知角色时，服务会补全项目代理标识和来源标签，便于后续排队与审计。
    /// en: Verifies that when the secretary targets a known role, the service fills in the project-agent identifier and dispatch source for later queueing and auditing.
    /// </summary>
    [Fact]
    public async Task ResolveSecretaryDispatchAsync_WithKnownRole_ResolvesProjectAgent()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");

        var target = await context.Service.ResolveSecretaryDispatchAsync(
            project.Id,
            new ProjectGroupDispatchInstruction { TargetRole = "producer", Task = "请编写后端 API" },
            CancellationToken.None);

        Assert.Equal("producer", target.TargetRole);
        Assert.Equal(projectAgent.Id, target.ProjectAgentRoleId);
        Assert.Equal("project_group_secretary_dispatch", target.DispatchSource);
        Assert.Null(target.UnresolvedMessage);
    }

    /// <summary>
    /// zh-CN: 验证秘书广播给“所有人”时，会为每个项目代理生成目标并标记广播来源。
    /// en: Verifies that broadcasting to "everyone" produces a dispatch target for every project agent and marks each one with the broadcast source.
    /// </summary>
    [Fact]
    public async Task ResolveSecretaryDispatchesAsync_WithBroadcastTarget_ReturnsAllProjectAgents()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");
        await context.AddProjectAgentAsync(project.Id, roleType: "architect", name: "Architect");

        var targets = await context.Service.ResolveSecretaryDispatchesAsync(
            project.Id,
            new ProjectGroupDispatchInstruction { TargetRole = "所有人", Task = "请同步评审当前方案" },
            CancellationToken.None);

        Assert.Equal(2, targets.Count);
        Assert.All(targets, target =>
        {
            Assert.NotNull(target.ProjectAgentRoleId);
            Assert.Equal("project_group_secretary_broadcast", target.DispatchSource);
        });
    }

    /// <summary>
    /// zh-CN: 验证普通成员继续广播分发时，也会保留成员来源标签，便于后续运行时区分是秘书分发还是成员接力。
    /// en: Verifies that when a regular member broadcasts follow-up work, the emitted targets keep a member-scoped source tag so later runtime context can distinguish it from secretary dispatch.
    /// </summary>
    [Fact]
    public async Task ResolveDispatchesAsync_WithMemberBroadcastTarget_ReturnsMemberBroadcastSource()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");
        await context.AddProjectAgentAsync(project.Id, roleType: "architect", name: "Architect");

        var targets = await context.Service.ResolveDispatchesAsync(
            project.Id,
            new ProjectGroupDispatchInstruction { TargetRole = "所有人", Task = "请同步当前实现方案" },
            "project_group_member_dispatch",
            CancellationToken.None);

        Assert.Equal(2, targets.Count);
        Assert.All(targets, target =>
        {
            Assert.NotNull(target.ProjectAgentRoleId);
            Assert.Equal("project_group_member_broadcast", target.DispatchSource);
        });
    }

    /// <summary>
    /// zh-CN: 验证能力请求解析会提取展示文本、工具列表和申请原因，方便秘书后续决定是否补齐能力。
    /// en: Verifies that capability-request extraction captures the display text, tool list, and reason so the secretary can decide how to satisfy the request.
    /// </summary>
    [Fact]
    public void TryExtractCapabilityRequest_ReturnsToolsAndReason()
    {
        using var context = new TestContext();

        var extracted = context.Service.TryExtractCapabilityRequest(
            "我目前缺少文件能力。\n\n<openstaff_capability_request>{\"requiredTools\":[\"file_system\"],\"reason\":\"需要创建和修改项目文件\"}</openstaff_capability_request>",
            out var displayContent,
            out var request);

        Assert.True(extracted);
        Assert.Equal("我目前缺少文件能力。", displayContent);
        Assert.NotNull(request);
        Assert.Equal("需要创建和修改项目文件", request!.Reason);
        Assert.Equal(["file_system"], request.RequiredTools);
    }

    /// <summary>
    /// zh-CN: 验证目标代理忙碌时，新任务只会进入排队状态并记录运行时元数据，而不会错误抢占执行租约。
    /// en: Verifies that when the target agent is busy, a new task stays queued with runtime metadata instead of incorrectly stealing an execution lease.
    /// </summary>
    [Fact]
    public async Task QueueTaskAsync_WhenAgentBusy_LeavesTaskPending()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");
        var session = await context.AddSessionAsync(project.Id);
        var activeFrame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "现有任务");
        var activeQueue = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            activeFrame.Id,
            new ProjectGroupDispatchTarget("producer", "现有任务", "@Producer 现有任务", true, projectAgent.Id, "Producer"),
            CancellationToken.None);
        Assert.NotNull(activeQueue.Lease);

        var frame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "帮我写后端 API");

        var queueResult = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget("producer", "帮我写后端 API", "@Producer 帮我写后端 API", true, projectAgent.Id, "Producer"),
            CancellationToken.None);

        var task = await context.Db.Tasks.AsNoTracking().SingleAsync(item => item.Id == queueResult.TaskId);
        var updatedFrame = await context.Db.ChatFrames.AsNoTracking().SingleAsync(f => f.Id == frame.Id);
        var queuedEvent = await context.Db.AgentEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventType == EventTypes.TaskQueued && e.Content.Contains(task.Title));

        Assert.Null(queueResult.Lease);
        Assert.NotNull(queueResult.QueueAcknowledgement);
        Assert.Equal(TaskItemStatus.Pending, task.Status);
        Assert.Equal(projectAgent.Id, task.AssignedProjectAgentRoleId);
        Assert.Equal(task.Id, updatedFrame.TaskId);
        var metadata = TaskItemRuntimeMetadata.TryParse(task.Metadata);
        Assert.NotNull(metadata);
        Assert.Equal(session.Id, metadata!.SessionId);
        Assert.Equal(frame.Id, metadata.FrameId);
        Assert.Equal(SessionSceneTypes.ProjectGroup, metadata.Scene);
        Assert.Equal("project_group_mention", metadata.Source);
        Assert.Equal(projectAgent.Id, metadata.TargetProjectAgentRoleId);
        Assert.Equal(TaskItemStatus.Pending, metadata.LastStatus);
        Assert.NotNull(queuedEvent);
    }

    /// <summary>
    /// zh-CN: 验证仅有脏忙碌状态而无活动任务时，新任务会立即接管执行，而不是被永久排队。
    /// en: Verifies that a dirty busy status without any active task is self-healed so the new task starts immediately instead of staying queued forever.
    /// </summary>
    [Fact]
    public async Task QueueTaskAsync_WhenBusyStatusIsStale_StartsTaskImmediately()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer", status: AgentStatus.Working, currentTask: "陈旧任务");
        var session = await context.AddSessionAsync(project.Id);
        var frame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "帮我修复状态");

        var queueResult = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget("producer", "帮我修复状态", "@Producer 帮我修复状态", true, projectAgent.Id, "Producer"),
            CancellationToken.None);

        var task = await context.Db.Tasks.AsNoTracking().SingleAsync();
        var updatedAgent = await context.Db.ProjectAgentRoles.AsNoTracking().SingleAsync(agent => agent.Id == projectAgent.Id);

        Assert.NotNull(queueResult.Lease);
        Assert.Null(queueResult.QueueAcknowledgement);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Equal(AgentStatus.Working, updatedAgent.Status);
        Assert.Equal(task.Title, updatedAgent.CurrentTask);
    }

    /// <summary>
    /// zh-CN: 验证项目群调度前会恢复遗留的排队任务，避免智能体因为脏状态永久显示“没空”。
    /// en: Verifies that stale queued ProjectGroup work is recovered before dispatch so an agent does not stay permanently unavailable.
    /// </summary>
    [Fact]
    public async Task RecoverStaleLeasesAsync_WhenPendingTaskExists_StartsOldestQueuedTask()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "reviewer", name: "Reviewer", status: AgentStatus.Working, currentTask: "陈旧任务");
        var session = await context.AddSessionAsync(project.Id);
        var frame = await context.AddFrameAsync(session.Id, targetRole: "reviewer", purpose: "审查贪食蛇项目");
        var queuedTask = new TaskItem
        {
            ProjectId = project.Id,
            Title = "审查贪食蛇项目 index.html 的完整代码质量",
            Description = "@Reviewer 审查贪食蛇项目",
            AssignedProjectAgentRoleId = projectAgent.Id,
            Status = TaskItemStatus.Pending,
            Metadata = JsonSerializer.Serialize(new TaskItemRuntimeMetadata
            {
                SessionId = session.Id,
                FrameId = frame.Id,
                Scene = SessionSceneTypes.ProjectGroup,
                Source = "project_group_mention",
                MentionedProjectAgentRoleId = projectAgent.Id,
                TargetProjectAgentRoleId = projectAgent.Id,
                LastStatus = TaskItemStatus.Pending
            })
        };
        context.Db.Tasks.Add(queuedTask);
        frame.TaskId = queuedTask.Id;
        await context.Db.SaveChangesAsync();

        var recoveredLeases = await context.Service.RecoverStaleLeasesAsync(
            project.Id,
            [projectAgent.Id],
            CancellationToken.None);

        var recoveredLease = Assert.Single(recoveredLeases);
        var updatedTask = await context.Db.Tasks.AsNoTracking().SingleAsync(task => task.Id == queuedTask.Id);
        var updatedAgent = await context.Db.ProjectAgentRoles.AsNoTracking().SingleAsync(agent => agent.Id == projectAgent.Id);
        var metadata = TaskItemRuntimeMetadata.TryParse(updatedTask.Metadata);

        Assert.Equal(queuedTask.Id, recoveredLease.TaskId);
        Assert.Equal(frame.Id, recoveredLease.FrameId);
        Assert.Equal(TaskItemStatus.InProgress, updatedTask.Status);
        Assert.Equal(AgentStatus.Working, updatedAgent.Status);
        Assert.Equal(updatedTask.Title, updatedAgent.CurrentTask);
        Assert.NotNull(metadata);
        Assert.Equal(TaskItemStatus.InProgress, metadata!.LastStatus);
    }

    /// <summary>
    /// zh-CN: 验证任务完成后，同一代理的下一条排队任务会被自动拉起，保持单代理串行执行模型。
    /// en: Verifies that when a task completes, the next queued task for the same agent is automatically started, preserving the single-agent serial execution model.
    /// </summary>
    [Fact]
    public async Task CompleteTaskAsync_StartsNextQueuedTaskForSameAgent()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");
        var session = await context.AddSessionAsync(project.Id);

        var firstFrame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "第一个任务");
        var firstQueue = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            firstFrame.Id,
            new ProjectGroupDispatchTarget("producer", "第一个任务", "@Producer 第一个任务", true, projectAgent.Id, "Producer"),
            CancellationToken.None);

        Assert.NotNull(firstQueue.Lease);

        var secondFrame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "第二个任务");
        var secondQueue = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            secondFrame.Id,
            new ProjectGroupDispatchTarget("producer", "第二个任务", "@Producer 第二个任务", true, projectAgent.Id, "Producer"),
            CancellationToken.None);

        Assert.Null(secondQueue.Lease);
        Assert.NotNull(secondQueue.QueueAcknowledgement);

        var completion = await context.Service.CompleteTaskAsync(
            firstQueue.Lease!,
            success: true,
            allowRetry: false,
            result: "第一个任务完成",
            CancellationToken.None);

        var tasks = await context.Db.Tasks
            .AsNoTracking()
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
        var updatedAgent = await context.Db.ProjectAgentRoles.AsNoTracking().SingleAsync(a => a.Id == projectAgent.Id);

        Assert.NotNull(completion.NextLease);
        Assert.Equal(tasks[1].Id, completion.NextLease!.TaskId);
        Assert.Equal(secondFrame.Id, completion.NextLease.FrameId);
        Assert.Equal(session.Id, completion.NextLease.SessionId);
        Assert.Null(completion.SecretaryNotice);
        Assert.Equal(TaskItemStatus.Done, tasks[0].Status);
        Assert.Equal(TaskItemStatus.InProgress, tasks[1].Status);
        Assert.Equal(AgentStatus.Working, updatedAgent.Status);
        Assert.Equal(tasks[1].Title, updatedAgent.CurrentTask);
    }

    /// <summary>
    /// zh-CN: 验证在仍允许自动重试的失败场景下，服务会返回原租约并累计尝试次数，而不是立即阻塞任务。
    /// en: Verifies that a retryable failure returns the same lease and increments the attempt count instead of blocking the task immediately.
    /// </summary>
    [Fact]
    public async Task CompleteTaskAsync_OnFailureBeforeMaxAttempts_ReturnsSameLeaseForRetry()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");
        var session = await context.AddSessionAsync(project.Id);
        var frame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "需要自动重试的任务");

        var queueResult = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget("producer", "需要自动重试的任务", "@Producer 需要自动重试的任务", true, projectAgent.Id, "Producer"),
            CancellationToken.None);

        var completion = await context.Service.CompleteTaskAsync(
            queueResult.Lease!,
            success: false,
            allowRetry: true,
            result: "第一次失败",
            CancellationToken.None);

        var task = await context.Db.Tasks.AsNoTracking().SingleAsync();
        var retryEvent = await context.Db.AgentEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventType == EventTypes.TaskRetry);

        Assert.NotNull(completion.NextLease);
        Assert.Equal(queueResult.Lease!.TaskId, completion.NextLease!.TaskId);
        Assert.Null(completion.SecretaryNotice);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Contains("\"AttemptCount\":1", task.Metadata);
        Assert.NotNull(retryEvent);
    }

    /// <summary>
    /// zh-CN: 验证“无惩罚重试”会复用原租约但不增加尝试次数，适用于能力补齐后立即继续执行的场景。
    /// en: Verifies that a retry without penalty reuses the same lease without incrementing attempts, which fits capability-refresh scenarios that continue immediately.
    /// </summary>
    [Fact]
    public async Task CompleteTaskAsync_WithRetryWithoutPenalty_ReturnsSameLeaseWithoutIncrementingAttempts()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");
        var session = await context.AddSessionAsync(project.Id);
        var frame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "等待能力刷新后重试的任务");

        var queueResult = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget("producer", "等待能力刷新后重试的任务", "@Producer 等待能力刷新后重试的任务", true, projectAgent.Id, "Producer"),
            CancellationToken.None);

        var completion = await context.Service.CompleteTaskAsync(
            queueResult.Lease!,
            success: false,
            allowRetry: true,
            result: "能力已补齐，准备立即重试",
            CancellationToken.None,
            secretaryNoticeOverride: "秘书已刷新能力，正在继续执行。",
            retryWithoutPenalty: true);

        var task = await context.Db.Tasks.AsNoTracking().SingleAsync();
        var retryEvent = await context.Db.AgentEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventType == EventTypes.TaskRetry);

        Assert.NotNull(completion.NextLease);
        Assert.Equal("秘书已刷新能力，正在继续执行。", completion.SecretaryNotice);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.DoesNotContain("\"AttemptCount\":1", task.Metadata);
        Assert.Null(retryEvent);
    }

    /// <summary>
    /// zh-CN: 验证被阻塞的任务在手动恢复后会重新进入执行中，并留下可审计的人工恢复事件。
    /// en: Verifies that a blocked task re-enters the in-progress state when manually resumed and leaves an auditable retry event for the manual resume.
    /// </summary>
    [Fact]
    public async Task ResumeBlockedTaskAsync_ReturnsLeaseAndMarksTaskInProgress()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");
        var session = await context.AddSessionAsync(project.Id);
        var frame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "等待手动恢复的任务");

        var queueResult = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget("producer", "等待手动恢复的任务", "@Producer 等待手动恢复的任务", true, projectAgent.Id, "Producer"),
            CancellationToken.None);

        await context.Service.CompleteTaskAsync(
            queueResult.Lease!,
            success: false,
            allowRetry: false,
            result: "等待用户补齐能力",
            CancellationToken.None,
            secretaryNoticeOverride: "请用户补齐能力后恢复任务。");

        var resumed = await context.Service.ResumeBlockedTaskAsync(project.Id, queueResult.TaskId, CancellationToken.None);
        var task = await context.Db.Tasks.AsNoTracking().SingleAsync();
        var retryEvent = await context.Db.AgentEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventType == EventTypes.TaskRetry);

        Assert.NotNull(resumed);
        Assert.Equal(queueResult.Lease!.FrameId, resumed!.Lease.FrameId);
        Assert.Equal(session.Id, resumed.Lease.SessionId);
        Assert.Equal(queueResult.TaskId, resumed.Lease.TaskId);
        Assert.Equal(projectAgent.Id, resumed.Lease.ProjectAgentRoleId);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.NotNull(retryEvent);
        Assert.NotNull(retryEvent!.Metadata);
        using var retryMetadataDocument = JsonDocument.Parse(retryEvent.Metadata!);
        Assert.Equal("manual_resume", retryMetadataDocument.RootElement.GetProperty("reason").GetString());
    }

    /// <summary>
    /// zh-CN: 验证达到最终失败阈值后不会再续租任务，而是返回提示秘书重新评估的通知。
    /// en: Verifies that once the final failure threshold is reached, the task is not leased again and instead produces a notice for the secretary to reassess the next step.
    /// </summary>
    [Fact]
    public async Task CompleteTaskAsync_OnFinalFailure_ReturnsSecretaryNotice()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");
        var session = await context.AddSessionAsync(project.Id);
        var frame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "最终失败的任务");

        var queueResult = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget("producer", "最终失败的任务", "@Producer 最终失败的任务", true, projectAgent.Id, "Producer"),
            CancellationToken.None);

        ProjectGroupTaskCompletionResult? completion = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            completion = await context.Service.CompleteTaskAsync(
                queueResult.Lease!,
                success: false,
                allowRetry: true,
                result: $"第 {attempt + 1} 次失败",
                CancellationToken.None);
        }

        var task = await context.Db.Tasks.AsNoTracking().SingleAsync();

        Assert.NotNull(completion);
        Assert.Null(completion!.NextLease);
        Assert.Equal(TaskItemStatus.Blocked, task.Status);
        Assert.Contains("秘书将重新评估下一步", completion.SecretaryNotice);
    }

    /// <summary>
    /// zh-CN: 验证显式禁用重试时，任务会立刻进入阻塞状态并把说明交还给秘书处理。
    /// en: Verifies that when retries are explicitly disabled, the task is blocked immediately and control returns to the secretary with an explanatory notice.
    /// </summary>
    [Fact]
    public async Task CompleteTaskAsync_WithRetryDisabled_BlocksTaskImmediately()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync();
        var projectAgent = await context.AddProjectAgentAsync(project.Id, roleType: "producer", name: "Producer");
        var session = await context.AddSessionAsync(project.Id);
        var frame = await context.AddFrameAsync(session.Id, targetRole: "producer", purpose: "缺少能力的任务");

        var queueResult = await context.Service.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget("producer", "缺少能力的任务", "@Producer 缺少能力的任务", true, projectAgent.Id, "Producer"),
            CancellationToken.None);

        var completion = await context.Service.CompleteTaskAsync(
            queueResult.Lease!,
            success: false,
            allowRetry: false,
            result: "缺少文件系统能力",
            CancellationToken.None,
            secretaryNoticeOverride: "Producer 申请额外能力：file_system。请由秘书决定下一步。");

        var task = await context.Db.Tasks.AsNoTracking().SingleAsync();
        var metadata = TaskItemRuntimeMetadata.TryParse(task.Metadata);

        Assert.Null(completion.NextLease);
        Assert.Equal(TaskItemStatus.Blocked, task.Status);
        Assert.Equal("Producer 申请额外能力：file_system。请由秘书决定下一步。", completion.SecretaryNotice);
        Assert.NotNull(metadata);
        Assert.Equal(TaskItemStatus.Blocked, metadata!.LastStatus);
        Assert.Equal("缺少文件系统能力", metadata.LastError);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly SqliteConnection _connection;

        /// <summary>
        /// zh-CN: 初始化隔离的内存数据库和执行服务，为项目群排队与重试语义提供稳定的测试底座。
        /// en: Initializes an isolated in-memory database and execution service that provides a stable foundation for project-group queueing and retry semantics.
        /// </summary>
        public TestContext()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            Services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection))
                .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
                .AddScoped<IProjectAgentRoleRepository, ProjectAgentRoleRepository>()
                .AddScoped<IChatFrameRepository, ChatFrameRepository>()
                .AddScoped<ITaskItemRepository, TaskItemRepository>()
                .AddScoped<IAgentEventRepository, AgentEventRepository>()
                .BuildServiceProvider();

            Db = Services.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();

            Service = new ProjectGroupExecutionService(
                Services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<ProjectGroupExecutionService>.Instance);
        }

        public ServiceProvider Services { get; }
        public AppDbContext Db { get; }
        public ProjectGroupExecutionService Service { get; }

        /// <summary>
        /// zh-CN: 创建一个处于执行阶段的最小项目，使调度与排队测试能够直接围绕真实项目标识运行。
        /// en: Creates a minimal running project so the dispatch and queueing tests can operate around a real project identifier.
        /// </summary>
        public async Task<Project> AddProjectAsync()
        {
            var project = new Project
            {
                Name = "Project Group Test",
                Status = ProjectStatus.Active,
                Phase = ProjectPhases.Running,
                Language = "zh-CN"
            };

            Db.Projects.Add(project);
            await Db.SaveChangesAsync();
            return project;
        }

        /// <summary>
        /// zh-CN: 为项目挂接一个带角色信息的代理，可按需要指定忙闲状态和当前任务来模拟占用情况。
        /// en: Attaches a role-backed agent to the project and optionally sets its busy state and current task to simulate occupancy.
        /// </summary>
        public async Task<ProjectAgentRole> AddProjectAgentAsync(
            Guid projectId,
            string roleType,
            string name,
            string status = AgentStatus.Idle,
            string? currentTask = null)
        {
            var role = new AgentRole
            {
                Name = name,
                JobTitle = roleType,
                IsActive = true
            };
            Db.AgentRoles.Add(role);

            var agent = new ProjectAgentRole
            {
                ProjectId = projectId,
                AgentRole = role,
                Status = status,
                CurrentTask = currentTask
            };
            Db.ProjectAgentRoles.Add(agent);
            await Db.SaveChangesAsync();
            return agent;
        }

        /// <summary>
        /// zh-CN: 创建项目群会话，作为排队任务和聊天帧归属的根上下文。
        /// en: Creates a project-group session that acts as the root context for queued tasks and chat frames.
        /// </summary>
        public async Task<ChatSession> AddSessionAsync(Guid projectId)
        {
            var session = new ChatSession
            {
                ProjectId = projectId,
                Scene = SessionSceneTypes.ProjectGroup,
                Status = SessionStatus.Active,
                InitialInput = "session"
            };

            Db.ChatSessions.Add(session);
            await Db.SaveChangesAsync();
            return session;
        }

        /// <summary>
        /// zh-CN: 为测试任务创建活动聊天帧，用来承载调度目标、用途和后续任务关联。
        /// en: Creates an active chat frame for a test task so it can carry the dispatch target, purpose, and later task association.
        /// </summary>
        public async Task<ChatFrame> AddFrameAsync(Guid sessionId, string targetRole, string purpose)
        {
            var frame = new ChatFrame
            {
                SessionId = sessionId,
                Depth = 0,
                Purpose = purpose,
                Status = FrameStatus.Active
            };

            Db.ChatFrames.Add(frame);
            await Db.SaveChangesAsync();
            return frame;
        }

        /// <summary>
        /// zh-CN: 清理内存数据库和服务容器，确保排队状态不会跨测试残留。
        /// en: Cleans up the in-memory database and service provider so queue state cannot leak across tests.
        /// </summary>
        public void Dispose()
        {
            Db.Dispose();
            Services.Dispose();
            _connection.Dispose();
        }
    }
}

