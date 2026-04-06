using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.Seeding;

/// <summary>
/// 启动时种子内置 MCP Server 定义
/// </summary>
public class McpSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpSeedService> _logger;

    public McpSeedService(IServiceProvider serviceProvider, ILogger<McpSeedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var builtinServers = GetBuiltinServers();
        var existingNames = await db.McpServers
            .Where(s => s.Source == McpSources.Builtin)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);

        var toAdd = builtinServers.Where(s => !existingNames.Contains(s.Name)).ToList();
        if (toAdd.Count > 0)
        {
            db.McpServers.AddRange(toAdd);
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} built-in MCP servers", toAdd.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static List<McpServer> GetBuiltinServers() =>
    [
        new()
        {
            Name = "Filesystem",
            Description = "读写本地文件系统，支持文件/目录的创建、读取、搜索等操作",
            Icon = "folder",
            Category = McpCategories.Filesystem,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-filesystem",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem",
            DefaultConfig = """{"command":"npx","args":["-y","@modelcontextprotocol/server-filesystem","/path/to/workspace"]}"""
        },
        new()
        {
            Name = "GitHub",
            Description = "GitHub API 操作：仓库管理、Issue/PR 操作、代码搜索等",
            Icon = "github",
            Category = McpCategories.DevTools,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-github",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/github",
            DefaultConfig = """{"command":"npx","args":["-y","@modelcontextprotocol/server-github"],"env":{"GITHUB_PERSONAL_ACCESS_TOKEN":""}}"""
        },
        new()
        {
            Name = "Brave Search",
            Description = "使用 Brave Search API 进行网页搜索和本地搜索",
            Icon = "search",
            Category = McpCategories.Search,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-brave-search",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/brave-search",
            DefaultConfig = """{"command":"npx","args":["-y","@modelcontextprotocol/server-brave-search"],"env":{"BRAVE_API_KEY":""}}"""
        },
        new()
        {
            Name = "Puppeteer",
            Description = "浏览器自动化：网页截图、点击、表单填写、JavaScript 执行",
            Icon = "chrome",
            Category = McpCategories.Browser,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-puppeteer",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/puppeteer",
            DefaultConfig = """{"command":"npx","args":["-y","@modelcontextprotocol/server-puppeteer"]}"""
        },
        new()
        {
            Name = "SQLite",
            Description = "SQLite 数据库操作：查询、建表、数据分析",
            Icon = "database",
            Category = McpCategories.Database,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-sqlite",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/sqlite",
            DefaultConfig = """{"command":"uvx","args":["mcp-server-sqlite","--db-path","/path/to/db.sqlite"]}"""
        },
        new()
        {
            Name = "PostgreSQL",
            Description = "PostgreSQL 数据库操作：查询、Schema 分析",
            Icon = "database",
            Category = McpCategories.Database,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-postgres",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/postgres",
            DefaultConfig = """{"command":"npx","args":["-y","@modelcontextprotocol/server-postgres","postgresql://user:pass@localhost:5432/db"]}"""
        },
        new()
        {
            Name = "Memory",
            Description = "基于知识图谱的持久化记忆系统",
            Icon = "brain",
            Category = McpCategories.Memory,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-memory",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/memory",
            DefaultConfig = """{"command":"npx","args":["-y","@modelcontextprotocol/server-memory"]}"""
        },
        new()
        {
            Name = "Fetch",
            Description = "HTTP 请求工具：获取网页内容并转换为 Markdown",
            Icon = "globe",
            Category = McpCategories.Search,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-fetch",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/fetch",
            DefaultConfig = """{"command":"uvx","args":["mcp-server-fetch"]}"""
        },
        new()
        {
            Name = "Sequential Thinking",
            Description = "结构化思考工具：支持多步推理、假设修正和思维分支",
            Icon = "lightbulb",
            Category = McpCategories.General,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-sequential-thinking",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/sequentialthinking",
            DefaultConfig = """{"command":"npx","args":["-y","@modelcontextprotocol/server-sequential-thinking"]}"""
        },
        new()
        {
            Name = "Everything",
            Description = "Windows 文件搜索工具（基于 Everything SDK）",
            Icon = "search",
            Category = McpCategories.Filesystem,
            TransportType = McpTransportTypes.Stdio,
            Source = McpSources.Builtin,
            NpmPackage = "@modelcontextprotocol/server-everything",
            Homepage = "https://github.com/modelcontextprotocol/servers/tree/main/src/everything",
            DefaultConfig = """{"command":"npx","args":["-y","@modelcontextprotocol/server-everything"]}"""
        }
    ];
}
