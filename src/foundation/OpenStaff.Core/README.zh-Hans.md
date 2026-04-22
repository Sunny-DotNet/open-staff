# OpenStaff.Core

## 用途与职责
`OpenStaff.Core` 是 OpenStaff 的基础类库，负责定义领域模型、智能体与编排契约、通知抽象、插件接口，以及供上层使用的轻量级模块系统。

## 架构位置
- 目标框架：`net10.0`
- 在仓库内不依赖其他项目
- 位于基础设施、智能体实现和 API 之下
- 只提供契约与共享类型，不直接启动宿主、访问数据库或执行外部 I/O

## 关键命名空间与组件
- `OpenStaff.Core.Modularity`
  - `OpenStaffModule`
  - `DependsOnAttribute`
  - `ModuleLoader`
  - `ModuleServiceCollectionExtensions`
  - 提供按依赖拓扑排序的服务注册与应用初始化机制
- `OpenStaff.Core.Agents`
  - `AgentContext`
  - `RoleConfig`
  - 工具注册表、提示词加载器、供应商解析器等接口
  - 承载智能体执行所需的项目、角色、账号、API Key、语言和场景上下文
- `OpenStaff.Core.Models`
  - 项目、任务、检查点、智能体角色等实体
  - 会话、执行帧、消息、事件等聊天相关实体
  - 供应商账号与 MCP 相关实体
- `OpenStaff.Core.Orchestration`
  - `TaskGraph`
  - `TaskNode`
  - `IOrchestrator`
  - `TaskGraph` 用于根据依赖关系计算可执行任务并检测环路
- `OpenStaff.Core.Notifications`
  - `INotificationService`
  - `Channels`，统一生成 `global`、`project:{id}`、`session:{id}` 频道名
- `OpenStaff.Core.Plugins`
  - `IPlugin`
  - `IAgentPlugin`
  - `PluginManifest`
- `OpenStaff.Options`
  - `OpenStaffOptions`，默认工作目录位于 `%USERPROFILE%\.staff`

## 重要依赖
- `Microsoft.Agents.AI`
- `Microsoft.Extensions.Configuration.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Options`
- `Microsoft.Extensions.Options.ConfigurationExtensions`

## 运行时与宿主行为
- `OpenStaffCoreModule` 是 OpenStaff 的根模块，负责注册默认的 `OpenStaffOptions`。
- 上层宿主通过 `AddOpenStaffModules<TStartupModule>(configuration)` 加载模块，再通过 `UseOpenStaffModules()` 执行模块初始化。
- `AgentContext.Language` 默认值为 `zh-CN`，方便下游组件直接进行语言感知处理。
- 该项目本身只是共享类库，真正的运行时行为由引用它的应用决定。

## 构建与验证命令
以下命令在仓库根目录执行：

```powershell
dotnet build src\foundation\OpenStaff.Core\OpenStaff.Core.csproj
dotnet test src\tests\OpenStaff.Tests\OpenStaff.Tests.csproj --filter "FullyQualifiedName~OpenStaff.Tests.Unit"
```

该项目没有独立的 `dotnet run` 启动命令。
