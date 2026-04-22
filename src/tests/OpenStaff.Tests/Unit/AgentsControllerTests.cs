using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Moq;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;
using OpenStaff.HttpApi.Controllers;

namespace OpenStaff.Tests.Unit;

public class AgentsControllerTests
{
    [Fact]
    public async Task GetRuntimePreview_ReturnsNotFound_WhenNotDevelopment()
    {
        var agentApiService = new Mock<IAgentApiService>(MockBehavior.Strict);
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(env => env.EnvironmentName).Returns(Environments.Production);

        var controller = new AgentsController(agentApiService.Object, hostEnvironment.Object);

        var result = await controller.GetRuntimePreview(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        agentApiService.Verify(
            service => service.GetRuntimePreviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRuntimePreview_ForwardsRequest_WhenDevelopment()
    {
        var projectId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var preview = new AgentRuntimePreviewDto
        {
            ProjectId = projectId,
            ProjectAgentRoleId = agentId,
            AgentRoleId = Guid.NewGuid(),
            RoleName = "Monica",
            Prompt = "preview"
        };

        var agentApiService = new Mock<IAgentApiService>();
        agentApiService
            .Setup(service => service.GetRuntimePreviewAsync(projectId, agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(env => env.EnvironmentName).Returns(Environments.Development);

        var controller = new AgentsController(agentApiService.Object, hostEnvironment.Object);

        var result = await controller.GetRuntimePreview(projectId, agentId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<AgentRuntimePreviewDto>(ok.Value);
        Assert.Same(preview, value);
    }
}
