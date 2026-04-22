using Microsoft.AspNetCore.Mvc;
using Moq;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.HttpApi.Controllers;

namespace OpenStaff.Tests.Unit;

public class SessionsControllerTests
{
    /// <summary>
    /// zh-CN: 验证控制器在指定场景下没有活动会话时，仍返回 200，并用 null 明确表示“当前无会话”。
    /// en: Verifies the controller still returns 200 and uses a null payload to represent "no current session" for the requested scene.
    /// </summary>
    [Fact]
    public async Task GetActiveByScene_ReturnsOkWithNull_WhenNoActiveSessionExists()
    {
        var sessionApiService = new Mock<ISessionApiService>();
        sessionApiService
            .Setup(service => service.GetActiveBySceneAsync(It.IsAny<GetActiveProjectSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionDto?)null);

        var controller = new SessionsController(sessionApiService.Object);

        var result = await controller.GetActiveByScene(Guid.NewGuid(), SessionSceneTypes.ProjectBrainstorm, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(ok.Value);
    }

    [Fact]
    public async Task SendMessage_ForwardsRawInputAndMentions()
    {
        var sessionApiService = new Mock<ISessionApiService>();
        SendSessionMessageRequest? capturedRequest = null;
        sessionApiService
            .Setup(service => service.SendMessageAsync(It.IsAny<SendSessionMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendSessionMessageRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new ConversationTaskOutput { TaskId = Guid.NewGuid(), SessionId = Guid.NewGuid() });

        var controller = new SessionsController(sessionApiService.Object);
        var sessionId = Guid.NewGuid();
        var mentionProjectAgentId = Guid.NewGuid();

        var result = await controller.SendMessage(
            sessionId,
            new ChatMessageRequest
            {
                Input = "开工",
                RawInput = "@Monica 开工",
                Mentions =
                [
                    new ConversationMentionDto
                    {
                        RawText = "@Monica",
                        BuiltinRole = "secretary",
                        ResolvedKind = "builtin_role",
                        ProjectAgentRoleId = mentionProjectAgentId
                    }
                ]
            },
            CancellationToken.None);

        _ = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(capturedRequest);
        Assert.Equal(sessionId, capturedRequest!.SessionId);
        Assert.Equal("开工", capturedRequest.Input);
        Assert.Equal("@Monica 开工", capturedRequest.RawInput);
        var mention = Assert.Single(capturedRequest.Mentions!);
        Assert.Equal("@Monica", mention.RawText);
        Assert.Equal("secretary", mention.BuiltinRole);
        Assert.Equal(mentionProjectAgentId, mention.ProjectAgentRoleId);
    }

    [Fact]
    public async Task Create_AllowsRawInputOnly_WhenExecutionInputIsEmpty()
    {
        var sessionApiService = new Mock<ISessionApiService>();
        sessionApiService
            .Setup(service => service.CreateAsync(It.IsAny<CreateSessionInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationTaskOutput { TaskId = Guid.NewGuid(), SessionId = Guid.NewGuid() });

        var controller = new SessionsController(sessionApiService.Object);

        var result = await controller.Create(
            new CreateSessionInput
            {
                ProjectId = Guid.NewGuid(),
                Input = string.Empty,
                RawInput = "@Monica 开工",
                Scene = SessionSceneTypes.ProjectGroup
            },
            CancellationToken.None);

        _ = Assert.IsType<OkObjectResult>(result.Result);
        sessionApiService.Verify(
            service => service.CreateAsync(
                It.Is<CreateSessionInput>(input => input.Input == string.Empty && input.RawInput == "@Monica 开工"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

