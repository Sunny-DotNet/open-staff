# OpenStaff.Marketplace.Internal

## 项目用途

`OpenStaff.Marketplace.Internal` 把本地 MCP 服务目录暴露为一个市场源。它将应用数据库中的记录转换为 `OpenStaff.Marketplace.Abstractions` 定义的统一市场模型。

## 在市场体系中的角色

这个项目是内置市场提供方，用来表示系统中已经存在或已经安装到本地的 MCP 服务。它的来源键是 `internal`，并且 `MarketplaceAppService` 会在调用方未指定来源时默认使用它。

## 关键数据源与类型

- `InternalMcpSource`
  - 实现 `IMcpMarketplaceSource`
  - 通过 `AppDbContext` 读取数据
  - 查询 `McpServers`
  - 将数据库实体映射为 `MarketplaceServerInfo`

- `OpenStaffMarketplaceInternalModule`
  - 依赖 `MarketplaceAbstractionsModule`
  - 通过 `MarketplaceOptions.AddSource<InternalMcpSource>()` 注册内置市场源

## 依赖关系

直接项目依赖：

- `OpenStaff.Marketplace.Abstractions`
- `OpenStaff.Infrastructure`

运行时主要依赖：

- `OpenStaff.Infrastructure.Persistence` 中的 `AppDbContext`
- `OpenStaff.Core.Models` 中的 `McpServer`
- `Microsoft.EntityFrameworkCore` 提供的查询能力

## 集成点

- 由 `OpenStaffMarketplaceInternalModule` 注册到市场源列表
- 通过 `IMarketplaceSourceFactory` 被 `OpenStaff.Application\Marketplace\MarketplaceAppService` 调用
- 最终由 `OpenStaff.HttpApi\Controllers\MarketplaceController` 对外暴露
- 数据直接来自保存本地 MCP 服务定义的数据库表

## 运行与维护说明

- 搜索直接复用 EF Core 查询，在数据库侧完成关键字和分类过滤。
- 仅使用传统分页：依赖 `Page` 与 `PageSize`，不会处理 `Cursor`。
- `GetByIdAsync` 要求传入 GUID 字符串，因为本地 `McpServer` 实体主键是 `Guid`。
- 本地表结构只存一个 `TransportType`，这里会包装成 `List<string>`，以匹配跨来源统一模型。
- 返回结果固定带有 `Source = "internal"` 和 `IsInstalled = true`，因为这个来源本身就代表本地已持久化的服务。
- 如果数据库中没有 MCP 服务记录，该来源会返回空结果，这属于正常行为。
