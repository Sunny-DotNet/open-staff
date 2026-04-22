using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 将模块自己的 <see cref="RuntimeSpec"/> 转换为官方 MCP SDK 的 transport 与 <see cref="McpClient"/>。
/// en: Converts the module's <see cref="RuntimeSpec"/> into official MCP SDK transports and <see cref="McpClient"/> instances.
/// </summary>
public sealed class McpClientFactory : IMcpClientFactory
{
    private readonly IMcpRuntimeResolver _runtimeResolver;
    private readonly ILoggerFactory _loggerFactory;

    public McpClientFactory(IMcpRuntimeResolver runtimeResolver, ILoggerFactory loggerFactory)
    {
        _runtimeResolver = runtimeResolver;
        _loggerFactory = loggerFactory;
    }

    public async Task<McpClient> CreateAsync(
        RuntimeSpec runtimeSpec,
        string? clientName = null,
        CancellationToken cancellationToken = default)
    {
        var transport = CreateTransport(runtimeSpec, clientName, _loggerFactory);
        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = clientName ?? "OpenStaff.Mcp",
                    Version = "1.0.0"
                }
            },
            loggerFactory: _loggerFactory,
            cancellationToken: cancellationToken);
    }

    public async Task<McpClient> CreateForInstallAsync(
        Guid installId,
        string? clientName = null,
        CancellationToken cancellationToken = default)
    {
        var runtimeSpec = await _runtimeResolver.ResolveRuntimeAsync(installId, cancellationToken);
        return await CreateAsync(runtimeSpec, clientName, cancellationToken);
    }

    private static IClientTransport CreateTransport(RuntimeSpec runtimeSpec, string? clientName, ILoggerFactory loggerFactory)
    {
        return runtimeSpec.TransportType switch
        {
            McpTransportType.Stdio => CreateStdioTransport(runtimeSpec, clientName),
            McpTransportType.Http => CreateHttpTransport(runtimeSpec, clientName, HttpTransportMode.AutoDetect, loggerFactory),
            McpTransportType.Sse => CreateHttpTransport(runtimeSpec, clientName, HttpTransportMode.Sse, loggerFactory),
            McpTransportType.StreamableHttp => CreateHttpTransport(runtimeSpec, clientName, HttpTransportMode.StreamableHttp, loggerFactory),
            _ => throw new InvalidOperationException($"Unsupported transport type '{runtimeSpec.TransportType}'.")
        };
    }

    private static IClientTransport CreateStdioTransport(RuntimeSpec runtimeSpec, string? clientName)
    {
        if (string.IsNullOrWhiteSpace(runtimeSpec.Command))
            throw new InvalidOperationException("Stdio runtime requires Command.");

        return new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = clientName,
            Command = runtimeSpec.Command,
            Arguments = [.. runtimeSpec.Arguments],
            EnvironmentVariables = new Dictionary<string, string?>(runtimeSpec.EnvironmentVariables, StringComparer.OrdinalIgnoreCase),
            WorkingDirectory = runtimeSpec.WorkingDirectory
        });
    }

    private static IClientTransport CreateHttpTransport(
        RuntimeSpec runtimeSpec,
        string? clientName,
        HttpTransportMode transportMode,
        ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(runtimeSpec.Url))
            throw new InvalidOperationException("HTTP-like runtime requires Url.");

        var headers = runtimeSpec.Headers
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.OrdinalIgnoreCase);

        return McpHttpClientTransportFactory.Create(new HttpClientTransportOptions
        {
            Name = clientName,
            Endpoint = new Uri(runtimeSpec.Url, UriKind.Absolute),
            TransportMode = transportMode,
            AdditionalHeaders = headers
        }, loggerFactory);
    }
}
