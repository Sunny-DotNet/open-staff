using Microsoft.AspNetCore.Mvc;
using Moq;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;
using OpenStaff.HttpApi.Controllers;

namespace OpenStaff.Tests.Unit;

public class AgentRolesControllerTests
{
    [Fact]
    public async Task GetVendorModelCatalog_ReturnsNotFound_WhenVendorCatalogIsUnavailable()
    {
        var apiService = new Mock<IAgentRoleApiService>();
        apiService
            .Setup(service => service.GetVendorModelCatalogAsync("anthropic", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorModelCatalogDto?)null);

        var controller = new AgentRolesController(apiService.Object);

        var result = await controller.GetVendorModelCatalog("anthropic", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetVendorProviderConfiguration_ReturnsOk_WithVendorConfigurationPayload()
    {
        var configuration = new VendorProviderConfigurationDto
        {
            ProviderType = "github-copilot",
            DisplayName = "GitHub Copilot",
            AvatarDataUri = "https://example.com/copilot.png",
            Properties =
            [
                new VendorProviderConfigurationPropertyDto
                {
                    Name = "Streaming",
                    FieldType = "boolean",
                    DefaultValue = true
                }
            ],
            Configuration = new Dictionary<string, object?>
            {
                ["Streaming"] = false
            }
        };
        var apiService = new Mock<IAgentRoleApiService>();
        apiService
            .Setup(service => service.GetVendorProviderConfigurationAsync("github-copilot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        var controller = new AgentRolesController(apiService.Object);

        var result = await controller.GetVendorProviderConfiguration("github-copilot", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(configuration, ok.Value);
    }

    [Fact]
    public async Task PreviewImport_ReturnsOk_WithPreviewPayload()
    {
        var preview = new PreviewAgentRoleTemplateImportResultDto
        {
            Role = new AgentRoleTemplatePreviewDto
            {
                Name = "Monica",
            },
        };
        var apiService = new Mock<IAgentRoleApiService>();
        apiService
            .Setup(service => service.PreviewTemplateImportAsync(It.IsAny<PreviewAgentRoleTemplateImportInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var controller = new AgentRolesController(apiService.Object);

        var result = await controller.PreviewImport(new PreviewAgentRoleTemplateImportInput
        {
            Content = "{ \"name\": \"Monica\" }",
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(preview, ok.Value);
    }
}

