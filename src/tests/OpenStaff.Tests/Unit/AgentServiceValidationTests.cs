using OpenStaff.Agent.Services;
using OpenStaff.Entities;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class AgentServiceValidationTests
{
    /// <summary>
    /// zh-CN: 验证只有 TargetRole 的请求不会再被前置校验误判为非法。
    /// 这个用例专门守住本轮修复的根因：错误文案早就说支持 TargetRole，但实现之前并没有真正接受它。
    /// en: Verifies target-role-only requests are no longer rejected by upfront validation.
    /// </summary>
    [Fact]
    public async Task CreateMessageAsync_WithTargetRoleOnly_DoesNotThrowValidationError()
    {
        using var service = new AgentService(new ThrowingRunFactory());

        var response = await service.CreateMessageAsync(
            new CreateMessageRequest(
                Scene: MessageScene.ProjectBrainstorm,
                MessageContext: new MessageContext(
                    ProjectId: Guid.NewGuid(),
                    SessionId: Guid.NewGuid(),
                    ParentMessageId: null,
                    FrameId: Guid.NewGuid(),
                    ParentFrameId: null,
                    TaskId: null,
                    ProjectAgentRoleId: null,
                    TargetRole: BuiltinRoleTypes.Secretary,
                    InitiatorRole: MessageRoles.User,
                    Extra: null),
                InputRole: Microsoft.Extensions.AI.ChatRole.User,
                Input: "继续头脑风暴"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.MessageId);
        Assert.True(service.TryGetMessageHandler(response.MessageId, out _));

        await Task.Delay(50);
        service.RemoveMessageHandler(response.MessageId);
    }

    /// <summary>
    /// zh-CN: 这里故意让 PrepareAsync 抛错，只为了证明前置校验已经放行；
    /// 一旦请求被接受，后台准备阶段是否失败就属于另一层问题，不应该再把 TargetRole-only 请求挡在入口外。
    /// en: Intentionally throws during preparation so the test only proves the upfront validation was bypassed successfully.
    /// </summary>
    private sealed class ThrowingRunFactory : IAgentRunFactory
    {
        public Task<PreparedAgentRun> PrepareAsync(CreateMessageRequest request, Guid messageId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Simulated preparation failure.");
    }
}
