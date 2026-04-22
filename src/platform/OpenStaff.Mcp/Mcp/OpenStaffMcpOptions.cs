namespace OpenStaff.Mcp;

/// <summary>
/// zh-CN: 定义 OpenStaff.Mcp 的运行选项，包括数据根目录以及安装/运行所需的引导命令。
/// en: Defines the runtime options for OpenStaff.Mcp, including the data root and the bootstrap commands used for installation/runtime preparation.
/// </summary>
public class OpenStaffMcpOptions
{
    /// <summary>
    /// zh-CN: 配置节名称。
    /// en: Configuration section name.
    /// </summary>
    public const string SectionName = "OpenStaff:Mcp";

    /// <summary>
    /// zh-CN: MCP 模块的数据根目录；未配置时使用本地应用数据目录下的 OpenStaff\mcp。
    /// en: Data root for the MCP module; defaults to OpenStaff\mcp under the local application data folder when not configured.
    /// </summary>
    public string? DataRootPath { get; set; }

    /// <summary>
    /// zh-CN: 用于执行 npm 安装的引导命令。
    /// en: Bootstrap command used to execute npm-based installations.
    /// </summary>
    public string BootstrapNpmCommand { get; set; } = "npm";

    /// <summary>
    /// zh-CN: 用于创建 Python 虚拟环境的引导命令。
    /// en: Bootstrap command used to create Python virtual environments.
    /// </summary>
    public string BootstrapPythonCommand { get; set; } = "python";

    /// <summary>
    /// zh-CN: 受管 Node 可执行文件路径；npm 或 JavaScript zip 安装在生成运行时规格时需要该路径。
    /// en: Managed Node executable path required when npm or JavaScript zip installs need to generate a compliant runtime specification.
    /// </summary>
    public string? ManagedNodeExecutablePath { get; set; }

    /// <summary>
    /// zh-CN: 静态模板目录的根地址。
    /// en: Base URL for the static MCP template catalog.
    /// </summary>
    public string TemplateCatalogBaseUrl { get; set; } = "https://mcps.gh.open-hub.cc";

    /// <summary>
    /// zh-CN: 静态模板目录缓存秒数。
    /// en: Cache lifetime in seconds for the static MCP template catalog.
    /// </summary>
    public int TemplateCatalogCacheSeconds { get; set; } = 300;

    /// <summary>
    /// zh-CN: 下载器默认附带的请求头。
    /// en: Default request headers attached by the artifact downloader.
    /// </summary>
    public Dictionary<string, string> DefaultRequestHeaders { get; set; } = [];

    /// <summary>
    /// zh-CN: 是否在宿主启动时预热全局 MCP 客户端。
    /// en: Whether enabled global MCP clients should be warmed during host startup.
    /// </summary>
    public bool EnableStartupWarmup { get; set; } = true;

    /// <summary>
    /// zh-CN: 懒加载 MCP 客户端在空闲多久后会被自动释放；常驻 warm client 不受该值影响。
    /// en: Idle timeout in seconds for lazily created MCP clients; pinned warm clients are exempt from this timeout.
    /// </summary>
    public int LazyClientIdleTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// zh-CN: 项目场景的 MCP 客户端首次成功使用后是否转为常驻复用。
    /// en: Whether project-scene MCP clients should become pinned for reuse after the first successful use.
    /// </summary>
    public bool PinProjectClientsAfterFirstUse { get; set; } = true;

    /// <summary>
    /// zh-CN: 解析最终的数据根目录。
    /// en: Resolves the final data root path.
    /// </summary>
    public string ResolveDataRootPath()
    {
        if (!string.IsNullOrWhiteSpace(DataRootPath))
            return Path.GetFullPath(DataRootPath);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "OpenStaff", "mcp");
    }
}
