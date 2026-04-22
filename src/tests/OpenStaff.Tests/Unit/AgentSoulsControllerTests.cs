using Microsoft.AspNetCore.Mvc;
using Moq;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;
using OpenStaff.HttpApi.Controllers;

namespace OpenStaff.Tests.Unit;

public class AgentSoulsControllerTests
{
    [Fact]
    public async Task GetOptions_ReturnsOk_WithCatalogPayload()
    {
        var catalog = new AgentSoulCatalogDto
        {
            Traits =
            [
                new AgentSoulOptionDto
                {
                    Key = "adaptable",
                    Label = "适应力强的"
                }
            ]
        };
        var apiService = new Mock<IAgentSoulApiService>();
        apiService
            .Setup(service => service.GetOptionsAsync("zh-CN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalog);

        var controller = new AgentSoulsController(apiService.Object);

        var result = await controller.GetOptions("zh-CN", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(catalog, ok.Value);
    }
}
