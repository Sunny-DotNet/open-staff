# OpenStaff.Marketplace.Abstractions

## 项目用途

`OpenStaff.Marketplace.Abstractions` 是 MCP 市场能力族的契约层。它本身不负责拉取市场数据，而是定义统一的搜索/详情模型，以及具体市场源接入时使用的注册机制。

## 在市场体系中的角色

这个项目是所有市场提供方的基础层：

- 本地/内置市场：`OpenStaff.Marketplace.Internal`
- 官方远程 Registry：`OpenStaff.Marketplace.Registry`

它让不同来源都能通过同一套接口暴露能力，使应用层可以在不写来源分支逻辑的前提下完成市场源枚举、搜索和安装。

## 关键抽象

- `IMcpMarketplaceSource`  
  市场源契约，统一暴露：
  - `SourceKey`
  - `DisplayName`
  - `IconUrl`
  - `SearchAsync(...)`
  - `GetByIdAsync(...)`

- `MarketplaceSearchQuery`  
  统一查询模型，包含关键字、分类、游标分页以及页码/页大小参数。

- `MarketplaceSearchResult`  
  统一搜索结果模型，同时兼容传统分页和游标分页。

- `MarketplaceServerInfo` 与 `RemoteEndpoint`  
  跨市场源统一的服务器元数据模型，包含传输类型、安装包线索、远程端点和默认配置等信息。

- `MarketplaceOptions`  
  维护已注册的市场源类型列表，各市场模块通过 `AddSource<TSource>()` 将自己加入注册表。

- `IMarketplaceSourceFactory` / `MarketplaceSourceFactory`  
  从依赖注入容器解析已配置的市场源，按需延迟实例化，并缓存已创建的实例。

## 依赖关系

直接项目依赖：

- `OpenStaff.Core`

该项目主要依赖 `OpenStaff.Core.Modularity` 提供的模块化基础设施，以及共享框架中的依赖注入和选项机制。

## 集成点

- `OpenStaff.Application\Marketplace\MarketplaceAppService` 通过 `IMarketplaceSourceFactory`：
  - 获取已注册市场源列表
  - 把搜索请求分发到指定来源
  - 在安装市场条目时解析具体市场源
- `OpenStaff.HttpApi\Controllers\MarketplaceController` 通过 `api/mcp/marketplace` 暴露对应接口
- 各具体市场模块通过配置 `MarketplaceOptions` 完成自注册

## 运行与维护说明

- 市场源实例在首次使用时创建，随后由 `MarketplaceSourceFactory` 缓存。
- `SourceKey` 必须唯一且稳定，因为调用方通过它选择市场源。
- 抽象层允许来源同时支持游标分页和页码分页，调用方不应假设每个来源的分页能力完全一致。
- 服务器标识由具体来源决定，例如某些来源使用 GUID，某些来源使用 `name:version` 组合键。
- “是否已安装”的补充逻辑不在本项目内处理，而是由应用层结合本地数据库做二次标记。
