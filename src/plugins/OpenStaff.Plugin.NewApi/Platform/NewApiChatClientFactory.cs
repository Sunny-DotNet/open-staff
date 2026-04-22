using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agents;
using OpenStaff.Provider;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Platform;

internal sealed class NewApiChatClientFactory(IServiceProvider serviceProvider) : DefaultChatClientFactoryBase(serviceProvider), IChatClientFactory
{
    protected override async Task<ModelInfo> GetModelAsync(string modelId)
    {
        var protocol = CreateInstance<NewApiProtocol>();
        var currentProviderDetail = GetRequiredService<ICurrentProviderDetail>();
        if (currentProviderDetail.Current is { })
        {

            var configurationResult = await protocol.LoadConfigurationAsync(currentProviderDetail.Current.AccountId);
            protocol.Initialize(configurationResult.Configuration);
        }
        var models = await protocol.ModelsAsync();
        return models.FirstOrDefault(m => m.ModelSlug == modelId) ?? throw new InvalidOperationException($"Model with id {modelId} not found.");
    }
}
