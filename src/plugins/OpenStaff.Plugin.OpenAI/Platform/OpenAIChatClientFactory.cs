using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Provider;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Platform;

internal class OpenAIChatClientFactory : DefaultChatClientFactoryBase
{
    private bool _urlSkipVersionLabel = false;
    public OpenAIChatClientFactory(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }
    protected override bool GetUrlSkipVersionLabel() => _urlSkipVersionLabel;
    protected override async Task<ModelInfo> GetModelAsync(string modelId)
    {
        var protocol = CreateInstance<OpenAIProtocol>();
        var currentProviderDetail = GetRequiredService<ICurrentProviderDetail>();
        if (currentProviderDetail.Current is { }) {

            var configurationResult=await protocol.LoadConfigurationAsync(currentProviderDetail.Current.AccountId);
            protocol.Initialize(configurationResult.Configuration);
            _urlSkipVersionLabel=configurationResult.Configuration.UrlSkipVersionLabel;
        }
        var models=await protocol.ModelsAsync();
        return models.FirstOrDefault(m => m.ModelSlug == modelId) ?? throw new InvalidOperationException($"Model with id {modelId} not found.");
    }
}
