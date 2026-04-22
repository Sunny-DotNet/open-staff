# OpenStaff.Marketplace.Registry

## 项目用途

`OpenStaff.Marketplace.Registry` 用于接入官方 MCP Registry：`https://registry.modelcontextprotocol.io`，并将其作为 OpenStaff 内部的一个市场源暴露出来。

## 在市场体系中的角色

这个项目是市场能力族中的外部市场提供方。它与本地 `internal` 来源互补，使 OpenStaff 能够浏览公共目录中的 MCP 服务并从远程市场安装条目。

## 关键组件与 Registry 数据模型

- `RegistryApiClient`
  - 封装对 `/v0/servers` 的 HTTP 调用
  - 使用专用 `HttpClient`
  - 返回反序列化后的 `RegistryResponse` 模型

- `RegistryMcpSource`
  - 实现 `IMcpMarketplaceSource`
  - 将 Registry 返回值映射为 `MarketplaceServerInfo`
  - 在上游 API 尚未支持关键字参数前，于客户端执行关键字过滤
  - 在调用 `GetByIdAsync` 时通过分页遍历定位目标条目

- `RegistryModels`
  - 定义 Registry 响应 DTO，例如：
    - `RegistryResponse`
    - `RegistryServerEntry`
    - `RegistryServer`
    - `RegistryPackage`
    - `RegistryRemote`
    - 分页元数据与官方发布状态相关类型

- `OpenStaffMarketplaceRegistryModule`
  - 为 `RegistryApiClient` 配置类型化 `HttpClient`
  - 把 `RegistryMcpSource` 注册到 `MarketplaceOptions`

## 依赖关系

直接项目依赖：

- `OpenStaff.Marketplace.Abstractions`
- `Microsoft.Extensions.Http`

此外，该项目还依赖共享框架中的日志抽象。

## 集成点

- 由依赖 `MarketplaceAbstractionsModule` 的 `OpenStaffMarketplaceRegistryModule` 启用
- 被应用模块图引入后，可由 `IMarketplaceSourceFactory` 发现
- `OpenStaff.Application\Marketplace\MarketplaceAppService` 通过它完成：
  - 远程搜索
  - 安装前的远程条目解析
  - 与本地 `McpServers` 表做已安装状态对比
- 最终由 `OpenStaff.HttpApi\Controllers\MarketplaceController` 对外提供接口

## 运行与维护说明

- 默认基础地址：`https://registry.modelcontextprotocol.io`
- `HttpClient` 超时：60 秒
- User-Agent：`OpenStaff/1.0`
- 当前官方 Registry 还不支持关键字查询参数，因此名称/描述过滤在 `RegistryMcpSource` 中完成。
- 搜索结果会优先保留未被官方元数据标记为“非最新版本”的条目，以减少界面中的重复版本。
- 返回结果的标识采用 `name:version` 组合键，而不是数据库 GUID。
- `GetByIdAsync` 最多会按每页 100 条遍历 5 轮，因为 Registry 没有直接的按 ID 查询接口。
- 传输类型会同时汇总远程端点和安装包元数据；若存在 npm/PyPI 安装包，还会补充 `stdio`。
- HTTP 错误和超时会被记录为日志，并转换为空响应，而不是向上抛出来源级异常。
