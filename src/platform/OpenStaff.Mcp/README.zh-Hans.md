# OpenStaff.Mcp 需求设计

## 1. 文档目标

本文档定义 `src\platform\OpenStaff.Mcp` 的一版完整需求与架构方案。目标不是描述“如何适配当前仓库里已有代码”，而是定义一个**自身可独立运作**、逻辑自洽、职责清晰、实现简洁的 MCP 模块。

该模块需要在内部闭环解决以下能力：

- MCP 搜索
- MCP 安装
- MCP 卸载
- `stdio` 型 MCP 的本地受管下载、安装、运行时解析

外部宿主可以是 Web API、CLI、桌面端或其它应用层，但这些宿主都只负责调用 `OpenStaff.Mcp`，不应承担其核心规则。

---

## 2. 设计原则

### 2.1 独立闭环

`OpenStaff.Mcp` 必须能独立完成：

- 市场源搜索与聚合
- 安装来源选择
- 本地安装目录管理
- manifest 写入与读取
- 运行时启动规格解析
- 卸载安全检查

### 2.2 核心规则内聚

以下规则必须留在模块内部，不散落到外层：

- 什么叫“已安装”
- 什么叫“可运行”
- `stdio` 的启动入口如何解析
- 什么情况下允许卸载
- 本地受管目录如何组织
- 版本、状态、错误如何记录

### 2.3 外部适配器要薄

HTTP、数据库、文件系统、下载器、包管理器、压缩包解压都属于适配层。适配层只提供能力，不拥有业务规则。

### 2.4 受管优先

`stdio` 型 MCP 首版**不允许**依赖机器上已有的全局命令。所有可执行入口必须来自 OpenStaff 受管安装目录。

### 2.5 安全优先

卸载首版采用保守策略：只要 MCP 仍被配置实例或绑定引用，就禁止卸载。

---

## 3. 范围

### 3.1 本期范围

#### A. 搜索

- 支持从多个 MCP 来源统一搜索
- 支持关键字、分类、来源、传输类型过滤
- 支持标记是否已安装
- 支持表达一个 MCP 的多个安装通道

#### B. 安装

- 支持 remote 型安装
- 支持 `stdio` 型安装
- `stdio` 首版支持三类来源：
  - npm
  - PyPI
  - GitHub Release / zip

#### C. 卸载

- 支持对已安装 MCP 执行卸载检查
- 支持移除本地受管产物
- 支持移除安装记录
- 若仍被引用，则拒绝卸载

#### D. 运行时解析

- 从安装记录与 manifest 解析出可直接运行的 `RuntimeSpec`
- 为 `stdio` 输出：
  - command
  - args
  - env
  - workingDirectory

### 3.2 明确不在本期范围

- 直接复用系统全局命令作为 `stdio` 入口
- 强制卸载（force uninstall）
- 自动升级策略
- 多版本并发激活切换 UI
- 安装源认证体系
- 沙箱执行与权限隔离

---

## 4. 模块定位

`OpenStaff.Mcp` 应被设计为 MCP 领域模块，而不是单纯的控制器配套项目。

建议内部结构：

```text
OpenStaff.Mcp
└─ Mcp
   ├─ Models
   ├─ Sources
   ├─ Services
   ├─ PackageManagers
   ├─ Persistence
   └─ Cli
```

### 4.1 Mcp

`Mcp` 作为模块内部唯一的主业务根目录，统一收纳 MCP 的模型、来源、服务、包管理器、持久化和命令行能力，避免横向分散成多套概念层。

### 4.2 Models

负责领域模型和值对象，例如：

- `CatalogEntry`
- `InstallChannel`
- `InstalledMcp`
- `RuntimeSpec`
- `UninstallCheckResult`

这里定义模块内部的稳定语义，不直接掺杂 HTTP DTO 或数据库实体细节。

### 4.3 Sources

负责 MCP 搜索来源接入与统一折叠，例如：

- internal
- registry
- 后续可扩展的第三方目录源

`Sources` 输出统一的目录条目与安装通道，不把来源差异泄露到上层服务。

### 4.4 Services

负责核心用例编排，例如：

- 搜索
- 安装
- 卸载
- 运行时解析
- 安装状态检查

`Services` 应直接围绕 MCP 业务动作组织，而不是围绕 HTTP、EF 或控制器划分。

### 4.5 PackageManagers

负责 `stdio` 安装来源中的包管理器与安装执行器，例如：

- npm 安装器
- PyPI 安装器
- GitHub Release / zip 下载解包安装器

这里聚焦“如何把安装通道落到受管目录”，不承载更高层的业务决策。

### 4.6 Persistence

负责安装记录、manifest 索引、状态持久化、目录定位和锁文件等存储能力。

它的职责是提供模块自己的持久化抽象，而不是直接等同于某个具体 ORM。

### 4.7 Cli

负责模块内的命令行能力，例如：

- 包管理器调用包装
- 诊断命令
- 修复/校验命令

`Cli` 是模块的基础设施能力之一，不是外部宿主层。

---

## 5. 核心领域模型

### 5.1 CatalogSource

表示一个 MCP 搜索来源。

关键字段：

- `Key`
- `DisplayName`
- `Priority`
- `Capabilities`

### 5.2 CatalogEntry

表示搜索结果中的一个 MCP 条目。

关键字段：

- `EntryId`
- `SourceKey`
- `Name`
- `DisplayName`
- `Description`
- `Category`
- `Version`
- `Homepage`
- `RepositoryUrl`
- `TransportTypes`
- `InstallChannels`
- `IsInstalled`
- `InstalledState`

说明：

- `CatalogEntry` 不能再被压扁成只有 `npmPackage` 和 `pypiPackage` 两个字段。
- 一条 MCP 可以同时具备多个安装通道。

### 5.3 InstallChannel

表示一个 MCP 条目的具体安装通道。

通道类型首版支持：

- `remote`
- `npm`
- `pypi`
- `github-release`
- `zip`

关键字段：

- `ChannelType`
- `TransportType`
- `Version`
- `EntrypointHint`
- `PackageIdentifier`
- `ArtifactUrl`
- `Checksum`
- `Metadata`

### 5.4 InstallRequest

表示一次安装请求。

关键字段：

- `CatalogEntryId`
- `SourceKey`
- `SelectedChannel`
- `RequestedVersion`
- `InstallRoot`
- `OverwriteExisting`

### 5.5 InstalledMcp

表示本地已安装 MCP 的聚合根。

关键字段：

- `InstallId`
- `Name`
- `SourceKey`
- `ChannelType`
- `TransportType`
- `Version`
- `InstallState`
- `ManifestPath`
- `InstallDirectory`
- `CreatedAt`
- `UpdatedAt`
- `LastError`

### 5.6 ManagedArtifact

表示本地受管安装产物。

关键字段：

- `ArtifactType`
- `RelativePath`
- `Checksum`
- `Size`
- `CreatedAt`

### 5.7 RuntimeSpec

表示运行时启动规格。

#### remote 型

- `TransportType`
- `Url`
- `Headers`

#### `stdio` 型

- `TransportType`
- `Command`
- `Arguments`
- `EnvironmentVariables`
- `WorkingDirectory`

### 5.8 UninstallCheckResult

表示卸载前检查结果。

关键字段：

- `CanUninstall`
- `BlockingReasons`
- `ReferencedByConfigs`
- `ReferencedByProjectBindings`
- `ReferencedByRoleBindings`

---

## 6. 搜索需求

### 6.1 搜索目标

用户搜索 MCP 时，不应该只看到“某个市场返回的一行文本”，而应看到一个可安装、可运行、可判断状态的目录条目。

### 6.2 搜索输入

支持：

- `keyword`
- `category`
- `source`
- `transportType`
- `page`
- `pageSize`
- `cursor`

### 6.3 搜索输出

每个结果至少返回：

- 基本元数据
- 支持的传输类型
- 可用安装通道
- 是否已安装
- 若已安装，对应版本和状态

### 6.4 搜索行为要求

#### A. 多来源统一

模块应允许聚合多个来源，但每个来源保留独立身份。

#### B. 去重

首版去重原则：

- 默认**不跨来源强行合并**
- 同一来源内若同名多版本重复出现，由来源适配器负责“最新优先”或显式版本展示

#### C. 已安装标记

搜索层必须能叠加本地安装状态，而不是交给上层自己做二次拼接。

#### D. 安装通道完整保留

若一个条目同时具备：

- remote endpoint
- npm
- PyPI

搜索结果必须完整保留三种候选通道，供安装阶段选择。

---

## 7. 安装需求

### 7.1 安装类型

#### 7.1.1 Remote 安装

远程安装不下载本地包，只保存：

- endpoint 信息
- transport 信息
- 运行时必要元数据

#### 7.1.2 `stdio` 安装

`stdio` 安装必须：

- 下载或安装到 OpenStaff 受管目录
- 生成 manifest
- 解析出运行时入口
- 可被后续 `ResolveRuntime` 直接消费

### 7.2 `stdio` 来源要求

#### npm

安装器负责：

- 拉取指定包版本
- 安装到受管目录
- 找到实际可执行入口或模板入口

#### PyPI

安装器负责：

- 下载 wheel / sdist 或通过受管 Python 环境安装
- 建立可运行入口
- 写入 manifest

#### GitHub Release / zip

安装器负责：

- 下载指定 release 资产或 zip 包
- 解压到受管目录
- 基于 metadata 解析可执行入口

### 7.3 安装状态机

首版最少需要如下状态：

- `pending`
- `downloading`
- `extracting`
- `installing`
- `resolving-runtime`
- `ready`
- `failed`
- `uninstalling`

要求：

- 每次状态迁移都可持久化
- 失败时记录 `LastError`
- 非 `ready` 状态不允许被判定为可运行

### 7.4 安装结果

安装成功后必须产出：

- 安装记录
- manifest
- 本地安装目录
- 运行时入口信息
- 可供搜索层回显的已安装状态

---

## 8. 受管目录设计

### 8.1 根目录

首版需要一个统一受管目录，例如：

```text
<OpenStaffData>\mcp\
```

### 8.2 目录结构

建议：

```text
<OpenStaffData>\mcp\
├─ installs\
│  ├─ npm\
│  │  └─ <package>\<version>\
│  ├─ pypi\
│  │  └─ <package>\<version>\
│  └─ github-release\
│     └─ <owner>\<repo>\<version>\
├─ cache\
│  ├─ downloads\
│  └─ extracts\
├─ manifests\
│  └─ <install-id>.json
└─ locks\
```

### 8.3 目录约束

- 目录结构必须稳定，可重复解析
- manifest 与安装目录必须双向可追踪
- 下载缓存与最终安装目录分离
- 同一安装目标需支持锁保护，避免并发覆盖

---

## 9. Manifest 规范

manifest 是 `stdio` 运行与维护的核心文件。

建议结构：

```json
{
  "installId": "guid-or-stable-id",
  "name": "filesystem",
  "sourceKey": "registry",
  "channelType": "npm",
  "transportType": "stdio",
  "version": "1.2.3",
  "installDirectory": "installs/npm/@modelcontextprotocol/server-filesystem/1.2.3",
  "runtime": {
    "command": "node",
    "args": ["dist/index.js"],
    "workingDirectory": ".",
    "env": {}
  },
  "artifacts": [],
  "createdAt": "utc",
  "updatedAt": "utc"
}
```

### 9.1 manifest 最低要求

- 能唯一标识一条安装记录
- 能恢复运行时入口
- 能指向实际安装目录
- 能记录产物列表
- 能记录版本与来源

### 9.2 manifest 使用场景

- 启动前运行时解析
- 搜索页显示已安装状态
- 卸载时清理目录
- 失败后修复与诊断

---

## 10. 运行时解析需求

`ResolveRuntime` 是模块对外的核心用例之一。

### 10.1 输入

- `InstallId` 或等价安装标识

### 10.2 输出

- 若为 remote：
  - endpoint runtime spec
- 若为 `stdio`：
  - command
  - args
  - env
  - working directory

### 10.3 约束

- 不能依赖系统 PATH 去猜测命令
- 不能在运行时重新搜索安装入口
- 运行时规格必须来自已持久化 manifest
- 若 manifest 损坏或目录缺失，应返回明确失败

---

## 11. 卸载需求

### 11.1 卸载前检查

卸载必须先执行引用检查。

阻塞来源：

- `McpServerConfig`
- `ProjectAgentMcpBinding`
- `AgentRoleMcpBinding`

只要任意一项仍引用该 MCP，就返回不可卸载。

### 11.2 卸载行为

当且仅当检查通过后，模块才允许：

- 删除 manifest
- 删除本地安装目录
- 删除缓存关联项
- 删除安装记录

### 11.3 首版不支持

- force uninstall
- 自动级联删除引用
- 无提示强制清理

---

## 12. 用例接口

模块对外应优先暴露与宿主无关的用例接口，而不是 HTTP 专用 DTO。

建议最小接口集：

- `SearchCatalog`
- `GetCatalogEntry`
- `ListInstalled`
- `GetInstalled`
- `Install`
- `CheckUninstall`
- `Uninstall`
- `ResolveRuntime`
- `RepairInstall`

说明：

- `RepairInstall` 虽然不一定首版实现，但应在设计上预留，因为受管目录、manifest 与实际文件可能出现偏差。

---

## 13. 存储抽象

模块应显式区分两类存储：

### 13.1 元数据存储

保存：

- 安装记录
- 状态
- 错误
- 来源与版本

### 13.2 文件存储

保存：

- 下载缓存
- 解压缓存
- 最终安装目录
- manifest
- 锁文件

要求：

- 元数据存储与文件存储解耦
- 不把“是否已安装”的判定只建立在数据库记录上
- 最终已安装状态需要数据库记录与 manifest/目录状态共同确认

---

## 14. 错误模型

模块应提供明确错误类型，而不是简单依赖字符串异常。

建议至少区分：

- `SourceUnavailable`
- `CatalogEntryNotFound`
- `InstallChannelNotSupported`
- `ArtifactDownloadFailed`
- `ArtifactExtractFailed`
- `PackageInstallFailed`
- `RuntimeResolutionFailed`
- `ManifestCorrupted`
- `InstallBlocked`
- `UninstallBlocked`

要求：

- 错误必须可定位到阶段
- 错误必须可返回给宿主层展示
- 错误必须能落到安装记录里

---

## 15. 测试要求

首版实现时至少需要覆盖以下测试面：

### 15.1 领域测试

- 安装状态机
- 卸载阻塞规则
- manifest 解析与校验
- 运行时规格解析

### 15.2 应用用例测试

- 搜索聚合
- 安装通道选择
- 安装成功/失败编排
- 卸载前检查

### 15.3 基础设施测试

- npm / PyPI / GitHub Release 安装器
- 下载与解压
- 目录锁
- manifest 读写

---

## 16. 对现有仓库的集成建议

虽然本模块按“独立可运作”设计，但落地到当前仓库时建议采用以下方式：

- `OpenStaff.Mcp` 提供核心接口与实现
- `OpenStaff.Application` 只做应用服务包装
- `OpenStaff.HttpApi` 只做端点映射
- 现有 `MarketplaceController` / `McpServersController` 后续可重定向到新的模块用例

重点不是兼容旧结构，而是让旧结构退化为薄适配层。

---

## 17. 建议的第一批交付物

实现阶段建议按以下顺序推进：

1. 领域模型与状态机
2. manifest 规范与受管目录
3. `ResolveRuntime`
4. 搜索聚合模型
5. npm / PyPI / GitHub Release 安装器
6. 卸载检查与卸载执行
7. 宿主适配层

---

## 18. 最终结论

`OpenStaff.Mcp` 首版应被定义为一个**独立的 MCP 安装与运行管理模块**，而不是“市场搜索接口 + 一张数据库表”的轻量包装。

它必须保证：

- 搜索结果是可安装模型，不是扁平展示数据
- `stdio` 安装是受管安装，不依赖全局命令
- 运行时规格来自 manifest，而不是运行时猜测
- 卸载有明确安全边界
- 外层宿主保持薄，核心规则全部回收到模块内部

---

## 19. 当前实现状态

当前 `OpenStaff.Mcp` 已经具备一套可直接接入的独立实现主干，核心目录如下：

```text
OpenStaff.Mcp
└─ Mcp
   ├─ Cli
   ├─ Exceptions
   ├─ Models
   ├─ PackageManagers
   ├─ Persistence
   ├─ Services
   └─ Sources
```

当前模块已实现：

- 目录搜索聚合服务
- 安装记录与 manifest 文件存储
- 受管目录布局与安装锁
- remote 安装
- `stdio` 安装器：
  - npm
  - PyPI
  - GitHub Release / zip
- `ResolveRuntime`
- 卸载检查与卸载执行
- 轻量级 `RepairInstall`

当前模块已经**内置官方 MCP Registry 源**，但仍然**不会自动知道宿主里的引用关系**。也就是说：

- `registry.modelcontextprotocol.io` 已默认接入，可直接搜索和安装
- `IMcpReferenceInspector` 仍可由宿主注册，用来阻止被外部绑定引用的卸载

如果宿主不注册任何引用检查器，卸载阶段就只会按模块内部状态执行，不会感知外部绑定关系。

---

## 20. 宿主如何接入

### 20.1 项目引用

宿主项目引用 `src\platform\OpenStaff.Mcp\OpenStaff.Mcp.csproj`。

在 OpenStaff 模块体系内，推荐在宿主模块上声明依赖：

```csharp
using OpenStaff.Core.Modularity;

namespace OpenStaff;

[DependsOn(typeof(OpenStaffMcpModule))]
public class MyHostModule : OpenStaffModule
{
}
```

### 20.2 模块自动注册的服务

`OpenStaffMcpModule` 默认会注册以下实现：

| 接口 | 默认实现 | 作用 |
| --- | --- | --- |
| `IMcpDataDirectoryLayout` | `McpDataDirectoryLayout` | 受管目录布局 |
| `IInstalledMcpMetadataStore` | `FileInstalledMcpMetadataStore` | 安装记录元数据 |
| `IMcpManifestStore` | `FileMcpManifestStore` | manifest 读写 |
| `IInstallLockManager` | `FileInstallLockManager` | 安装锁 |
| `IArtifactDownloader` | `HttpClientArtifactDownloader` | 下载器 |
| `IZipExtractor` | `ZipArchiveExtractor` | zip 解压 |
| `ICommandRunner` | `ProcessCommandRunner` | 命令执行 |
| `IInstallChannelInstaller` | `NpmInstallChannelInstaller` | npm 安装器 |
| `IInstallChannelInstaller` | `PyPiInstallChannelInstaller` | PyPI 安装器 |
| `IInstallChannelInstaller` | `ZipInstallChannelInstaller` | GitHub Release / zip 安装器 |
| `IMcpCatalogService` | `McpCatalogService` | 搜索聚合 |
| `IMcpClientFactory` | `McpClientFactory` | 基于官方 `ModelContextProtocol.Core` 客户端 API 创建 `McpClient` |
| `IInstalledMcpService` | `InstalledMcpService` | 已安装列表查询 |
| `IMcpInstallationService` | `McpInstallationService` | 安装编排 |
| `IMcpRuntimeResolver` | `McpRuntimeResolver` | 运行时解析 |
| `IMcpUninstallService` | `McpUninstallService` | 卸载检查/执行 |
| `IMcpRepairService` | `McpRepairService` | 轻量修复 |

此外，模块默认还会注册一个目录来源：

| 接口 | 默认实现 | 作用 |
| --- | --- | --- |
| `IMcpCatalogSource` | `OfficialRegistryCatalogSource` | 对接官方 `registry.modelcontextprotocol.io` |

宿主可以继续注册自己的实现去替换默认行为，例如：

- 自定义元数据存储（数据库而不是文件）
- 自定义下载器
- 自定义 Zip 解压器
- 自定义命令执行器
- 自定义安装器
- 自定义目录来源（如果你不想用官方 registry）

### 20.3 与 `ModelContextProtocol.Core` 包的职责边界

当前实现已经直接引用官方 `ModelContextProtocol.Core` 包，并将它用于**协议连接层**。

职责划分建议保持为：

- `OpenStaff.Mcp` 负责：
  - 搜索
  - 安装
  - manifest
  - 受管目录
  - 运行时规格解析
- `ModelContextProtocol.Core` 负责：
  - `StdioClientTransport`
  - `HttpClientTransport`
  - `McpClient`

也就是说，模块不再自己定义一套协议客户端，而是把 `RuntimeSpec` 交给官方 SDK 去连接。

这意味着模块当前已经收敛到客户端最小依赖，不再依赖主包里的 hosting / server 扩展。

---

## 21. 配置说明

模块读取配置节：

```json
{
  "OpenStaff": {
    "Mcp": {
      "DataRootPath": "C:\\OpenStaff\\mcp",
      "BootstrapNpmCommand": "npm",
      "BootstrapPythonCommand": "python",
      "RegistryBaseUrl": "https://registry.modelcontextprotocol.io",
      "ManagedNodeExecutablePath": "C:\\OpenStaff\\toolchain\\node\\node.exe",
      "DefaultRequestHeaders": {
        "User-Agent": "OpenStaff.Mcp"
      }
    }
  }
}
```

### 21.1 配置项说明

| 配置项 | 必填 | 说明 |
| --- | --- | --- |
| `DataRootPath` | 否 | 数据根目录；为空时默认使用本地应用数据目录下的 `OpenStaff\mcp` |
| `BootstrapNpmCommand` | 条件必填 | 执行 npm 安装时使用的命令，默认是 `npm` |
| `BootstrapPythonCommand` | 条件必填 | 创建虚拟环境和 pip 安装时使用的命令，默认是 `python` |
| `RegistryBaseUrl` | 否 | MCP Registry API 根地址，默认 `https://registry.modelcontextprotocol.io` |
| `ManagedNodeExecutablePath` | npm / JS zip 必填 | npm 安装或 `.js` 入口的 zip 安装在生成运行时规格时必须提供 |
| `DefaultRequestHeaders` | 否 | 下载远程产物时默认附带的请求头 |

### 21.2 重要约束

1. npm 安装并不会把 `npm` 当作最终运行命令；最终运行时需要 `ManagedNodeExecutablePath`
2. PyPI 安装会在受管目录下创建 `.venv`
3. zip / GitHub Release 安装如果入口是 `.js`，同样需要 `ManagedNodeExecutablePath`

---

## 22. 受管目录结构

默认数据根目录为：

```text
%LocalAppData%\OpenStaff\mcp
```

目录布局如下：

```text
<DataRoot>\
├─ installs\
├─ metadata\
├─ manifests\
├─ cache\
│  ├─ downloads\
│  └─ extracts\
└─ locks\
```

### 22.1 各目录用途

| 目录 | 说明 |
| --- | --- |
| `installs` | 最终受管安装目录 |
| `metadata` | 每条安装记录一个 JSON 元数据文件 |
| `manifests` | 每条安装对应一个 manifest |
| `cache\downloads` | 下载缓存 |
| `cache\extracts` | 解压缓存 |
| `locks` | 安装锁文件 |

### 22.2 “已安装”判定

当前实现里，一个 MCP 是否可用，至少受以下信息共同影响：

- 安装记录是否存在
- `InstallState` 是否为 `ready`
- manifest 是否存在
- 对于 `stdio`，安装目录是否存在

---

## 23. 宿主可选扩展点

### 23.1 `IMcpCatalogSource`

默认情况下，你**不需要自己实现目录来源**，因为模块已经内置了官方 registry。

只有在你想替换或追加其它来源时，才需要实现 `IMcpCatalogSource`：

```csharp
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Sources;

public sealed class MyCatalogSource : IMcpCatalogSource
{
    public string SourceKey => "my-source";
    public string DisplayName => "My Source";
    public int Priority => 100;

    public Task<IReadOnlyList<CatalogEntry>> SearchAsync(
        CatalogSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<CatalogEntry>>(
        [
            new CatalogEntry
            {
                EntryId = "filesystem",
                SourceKey = SourceKey,
                Name = "filesystem",
                DisplayName = "Filesystem",
                Description = "Managed filesystem MCP",
                Category = "filesystem",
                Version = "1.0.0",
                TransportTypes = [McpTransportType.Stdio],
                InstallChannels =
                [
                    new InstallChannel
                    {
                        ChannelId = "npm",
                        ChannelType = McpChannelType.Npm,
                        TransportType = McpTransportType.Stdio,
                        PackageIdentifier = "@modelcontextprotocol/server-filesystem"
                    }
                ]
            }
        ]);
    }

    public Task<CatalogEntry?> GetByIdAsync(
        string entryId,
        CancellationToken cancellationToken = default)
    {
        // 通常应与 SearchAsync 返回一致
        throw new NotImplementedException();
    }
}
```

然后在宿主里注册：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.AddSingleton<IMcpCatalogSource, MyCatalogSource>();
}
```

### 23.2 `IMcpReferenceInspector`

宿主通过实现 `IMcpReferenceInspector` 告诉模块“某个安装是否仍被外部配置引用”：

```csharp
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Services;

public sealed class MyReferenceInspector : IMcpReferenceInspector
{
    public Task<McpReferenceInspectionResult> InspectAsync(
        InstalledMcp installedMcp,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new McpReferenceInspectionResult
        {
            ReferencedByConfigs = [],
            ReferencedByProjectBindings = [],
            ReferencedByRoleBindings = []
        });
    }
}
```

如果检查器返回任意阻塞信息，`UninstallAsync` 会拒绝卸载。

### 23.3 最小接入建议

如果你只想要最小可用方案，宿主只需要：

1. 配好 `RegistryBaseUrl`
2. 配好 `ManagedNodeExecutablePath`（如果要装 npm / `.js` 类型 MCP）
3. 直接调用 `IMcpCatalogService` 搜索
4. 调用 `IMcpInstallationService` 安装
5. 调用 `IMcpClientFactory` 创建客户端

---

## 24. 目录模型与安装通道怎么组织

### 24.1 `CatalogSearchQuery`

搜索入参：

| 字段 | 说明 |
| --- | --- |
| `Keyword` | 关键字 |
| `Category` | 分类 |
| `SourceKey` | 指定来源 |
| `TransportType` | 过滤传输类型 |
| `Page` | 页码 |
| `PageSize` | 每页数量 |
| `Cursor` | 游标；如果提供，优先按游标分页 |

### 24.2 `CatalogEntry`

一个目录条目会携带：

- 基础元数据
- 传输类型列表
- 安装通道列表
- 是否已安装
- 已安装状态
- 已安装版本

### 24.3 `InstallChannel`

每个条目可以暴露多个安装通道。当前支持：

- `remote`
- `npm`
- `pypi`
- `github-release`
- `zip`

通道核心字段：

| 字段 | 说明 |
| --- | --- |
| `ChannelId` | 条目内唯一通道 ID |
| `ChannelType` | 通道类型 |
| `TransportType` | 运行传输类型 |
| `Version` | 通道默认版本 |
| `EntrypointHint` | 安装器用于解析入口的提示 |
| `PackageIdentifier` | npm / PyPI 包标识 |
| `ArtifactUrl` | remote endpoint 或 zip 产物地址 |
| `Checksum` | 可选校验值 |
| `Metadata` | 安装器消费的扩展字段 |

---

## 25. 各类安装通道怎么写

### 25.1 remote 通道

`remote` 通道不下载产物，只写安装记录和 manifest。

最小示例：

```csharp
new InstallChannel
{
    ChannelId = "remote",
    ChannelType = McpChannelType.Remote,
    TransportType = McpTransportType.StreamableHttp,
    ArtifactUrl = "https://example.com/mcp"
}
```

如果需要显式设置 endpoint 和 header，可使用以下 metadata：

| Key | 值类型 | 说明 |
| --- | --- | --- |
| `endpoint.url` | string | 远程 endpoint |
| `endpoint.headers` | JSON object string | 远程请求头 |

### 25.2 npm 通道

最小示例：

```csharp
new InstallChannel
{
    ChannelId = "npm",
    ChannelType = McpChannelType.Npm,
    TransportType = McpTransportType.Stdio,
    PackageIdentifier = "@modelcontextprotocol/server-filesystem"
}
```

说明：

- 安装器会执行 `npm install --prefix <installDir> --no-save ...`
- 会读取包的 `package.json` 里的 `bin`
- 如果 `bin` 有多个入口，需要提供 `EntrypointHint`
- 最终运行时命令来自 `ManagedNodeExecutablePath`

### 25.3 PyPI 通道

最小示例：

```csharp
new InstallChannel
{
    ChannelId = "pypi",
    ChannelType = McpChannelType.Pypi,
    TransportType = McpTransportType.Stdio,
    PackageIdentifier = "mcp-server-fetch"
}
```

说明：

- 安装器会先创建 `.venv`
- 然后使用受管 Python 执行 `pip install`
- 会探测 `console_scripts`
- 如果存在多个 console script，需要提供 `EntrypointHint`

### 25.4 GitHub Release / zip 通道

最小示例：

```csharp
new InstallChannel
{
    ChannelId = "zip",
    ChannelType = McpChannelType.Zip,
    TransportType = McpTransportType.Stdio,
    ArtifactUrl = "https://example.com/releases/server.zip",
    EntrypointHint = "server.exe"
}
```

如果不是简单的 `.exe` 或 `.js` 入口，建议显式提供运行时元数据。

当前约定的 metadata 键：

| Key | 值类型 | 说明 |
| --- | --- | --- |
| `runtime.command` | string | 运行命令 |
| `runtime.arguments` | JSON array string | 参数数组 |
| `runtime.environment` | JSON object string | 环境变量 |
| `runtime.workingDirectory` | string | 工作目录 |
| `runtime.commandRelativeToInstallDirectory` | bool string | 命令是否相对安装目录 |
| `runtime.argumentsRelativeToInstallDirectory` | JSON array string | 需要相对安装目录解析的参数下标 |

`.js` 入口示例：

```csharp
new InstallChannel
{
    ChannelId = "zip-js",
    ChannelType = McpChannelType.Zip,
    TransportType = McpTransportType.Stdio,
    ArtifactUrl = "https://example.com/server.zip",
    EntrypointHint = "dist/index.js"
}
```

这种情况下最终仍然需要 `ManagedNodeExecutablePath`。

---

## 26. 典型调用流程

### 26.1 搜索

```csharp
var result = await catalogService.SearchCatalogAsync(new CatalogSearchQuery
{
    Keyword = "filesystem",
    TransportType = McpTransportType.Stdio,
    Page = 1,
    PageSize = 20
}, ct);
```

### 26.2 查看单个条目

```csharp
var entry = await catalogService.GetCatalogEntryAsync("my-source", "filesystem", ct);
```

### 26.3 安装

```csharp
var installed = await installationService.InstallAsync(new InstallRequest
{
    SourceKey = "my-source",
    CatalogEntryId = "filesystem",
    SelectedChannelId = "npm",
    RequestedVersion = "1.0.0"
}, ct);
```

### 26.4 查询已安装列表

```csharp
var installedItems = await installedMcpService.ListInstalledAsync(ct);
```

### 26.5 解析运行时

```csharp
var runtime = await runtimeResolver.ResolveRuntimeAsync(installed.InstallId, ct);
```

### 26.6 直接创建官方 `McpClient`

```csharp
await using var client = await mcpClientFactory.CreateForInstallAsync(
    installed.InstallId,
    clientName: installed.DisplayName,
    cancellationToken: ct);

var tools = await client.ListToolsAsync(cancellationToken: ct);
```

对于 `remote`，返回结果主要关注：

- `TransportType`
- `Url`
- `Headers`

对于 `stdio`，返回结果主要关注：

- `Command`
- `Arguments`
- `EnvironmentVariables`
- `WorkingDirectory`

### 26.7 卸载前检查

```csharp
var check = await uninstallService.CheckUninstallAsync(installed.InstallId, ct);
if (!check.CanUninstall)
{
    // 呈现 blocking reasons
}
```

### 26.8 卸载

```csharp
await uninstallService.UninstallAsync(installed.InstallId, ct);
```

### 26.9 修复

```csharp
var repair = await repairService.RepairInstallAsync(installed.InstallId, ct);
```

---

## 27. manifest 与运行时解析

安装成功后，模块会把最终运行时信息写入 manifest。

一个典型的 `stdio` manifest 大致如下：

```json
{
  "installId": "f9fbb6f1-2fda-4af0-84e5-0dbec9d785d8",
  "catalogEntryId": "filesystem",
  "name": "filesystem",
  "displayName": "Filesystem",
  "sourceKey": "my-source",
  "channelType": "Npm",
  "transportType": "Stdio",
  "version": "1.0.0",
  "installDirectory": "installs\\npm\\@modelcontextprotocol\\server-filesystem\\1.0.0",
  "runtime": {
    "transportType": "Stdio",
    "command": "C:\\OpenStaff\\toolchain\\node\\node.exe",
    "arguments": [
      "node_modules\\@modelcontextprotocol\\server-filesystem\\dist\\index.js"
    ],
    "workingDirectory": ".",
    "commandRelativeToInstallDirectory": false,
    "workingDirectoryRelativeToInstallDirectory": true,
    "argumentsRelativeToInstallDirectory": [0]
  }
}
```

`ResolveRuntimeAsync` 的规则是：

1. 先读取安装记录
2. 要求安装状态必须是 `ready`
3. 再读取 manifest
4. 对 `stdio` 把相对路径展开为绝对路径
5. 不依赖系统 PATH 猜测入口

---

## 28. 错误与行为约束

当前模块已经定义并使用以下错误方向：

- 目录条目不存在
- 安装通道不支持
- 运行时解析失败
- 卸载被阻塞

实际接入时建议宿主：

1. 把这些异常映射成稳定的 API 错误码或 UI 提示
2. 不要在宿主层重新发明“已安装”“可运行”“是否允许卸载”的规则
3. 把来源差异留在 `IMcpCatalogSource` 内部，不要污染模块外层接口

---

## 29. 推荐接入方式

建议把 `OpenStaff.Mcp` 当成一个**独立领域模块**使用：

1. 宿主注册来源
2. 宿主注册引用检查器
3. 宿主只调用 `IMcpCatalogService`、`IMcpInstallationService`、`IMcpRuntimeResolver`、`IMcpUninstallService`
4. 宿主不直接操作 manifest、安装目录、状态流转

如果宿主未来需要：

- 接数据库
- 接企业内部 MCP 市场
- 接鉴权下载器
- 接额外包管理器

都应该通过替换或扩展 `OpenStaff.Mcp` 的抽象完成，而不是把规则重新搬回宿主层。
