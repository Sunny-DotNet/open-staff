# OpenStaff.Application

## 项目用途
此项目是 OpenStaff 的应用层实现，负责把提供商集成、智能体编排、会话执行、启动种子任务以及各类应用服务实现，统一组织在 `OpenStaff.Application.Contracts` 定义的契约之后。

## 架构定位
- 使用方：`OpenStaff.HttpApi.Host` 宿主。
- 实现内容：`OpenStaff.Application.Contracts` 中定义的公开应用服务。
- 依赖方向：基础设施、智能体厂商实现、提供商实现、市场模块与模型数据插件。
- 边界职责：这里承载应用协调与用例逻辑；HTTP 传输层在 `OpenStaff.HttpApi`，宿主与启动在 `OpenStaff.HttpApi.Host`。

## 关键命名空间与组件
- `Projects`：`ProjectAppService` 与 `ProjectService` 负责项目生命周期、工作区初始化、启动、导入导出和 README 读取。
- `Sessions`：`SessionAppService`、`SessionRunner`、`SessionStreamManager`、`ProjectGroupExecutionService`、`ProjectGroupCapabilityService` 负责栈式帧执行、暂停恢复、事件缓冲与会话历史查询。
- `Orchestration`：`OrchestrationService` 与 `AgentMcpToolService` 管理项目级智能体缓存、Provider 解析、运行时预热和 MCP 工具绑定。
- `Providers`：`ProviderAccountAppService`、`ProviderAccountService`、`ProviderResolver`、`ApiKeyResolver`、`ScopedProviderResolverProxy` 负责把账户配置和密钥解析成可执行提供商上下文。
- `Auth`：`DeviceAuthAppService`、`GitHubDeviceAuthService`、`CopilotTokenService` 负责 GitHub 设备授权与 Copilot 令牌交换。
- `AgentRoles` 与 `Agents`：负责角色管理、测试对话、项目智能体分配、事件流和定向消息。
- `Files`、`Tasks`、`Monitor`、`ModelData`、`McpServers`、`Settings`、`Marketplace`：其余公开应用服务的具体实现。
- `Seeding`：启动期 Hosted Service 现在主要负责 MCP 硬切重置、能力种子和运行时预加载，不再播种嵌入式内置角色。
- `OpenStaffApplicationModule`：应用层 DI 组合根。

## 重要依赖
- `OpenStaff.Application.Contracts`：本项目实现的公开接口与 DTO。
- `OpenStaff.Infrastructure`：EF Core 持久化、加密、Git 集成和底层服务。
- `OpenStaff.Agent.*` 及各厂商程序集：AI 智能体提供器与运行时适配器。
- `OpenStaff.Provider.*`：提供商抽象与各厂商实现。
- `OpenStaff.Marketplace.*`：市场发现与安装逻辑。
- `ModelContextProtocol` 与 `System.Reactive`：MCP 连接和回放/流式支持。
- `Microsoft.AspNetCore.App`：DI、Hosted Service、HTTP Client Factory 与后台运行时基础能力。

## DI 与运行时职责
`OpenStaffApplicationModule` 是应用行为的主要装配点。
- 将各厂商智能体提供器同时注册为 `IAgentProvider` 与 `IVendorAgentProvider`。
- 注册 `OrchestrationService`、`SessionRunner`、`SessionStreamManager`、`ProjectGroupExecutionService`、`ProjectGroupCapabilityService` 与平台层 `McpHub` 等单例运行时组件。
- 注册 `ProjectService`、`ProjectAgentService`、`SettingsService`、`ProviderAccountService`、`ProviderResolver` 以及全部应用服务实现的 Scoped 生命周期。
- 通过 `AddHttpClient` 注册 `GitHubDeviceAuthService`。
- 以 Hosted Service 启动 `McpHardResetService`、角色能力种子与 MCP 预加载等启动任务。

## 运行时与 API 行为
虽然此项目本身不承载 HTTP 端点，但大部分接口背后的运行时逻辑都在这里。
- 创建会话时会按项目场景复用已有活跃会话，并在后台启动首轮执行。
- `SessionRunner` 负责帧栈推进、等待用户输入后的恢复、取消令牌以及项目群组执行。
- `SessionStreamManager` 会在内存中维护活跃会话事件，向订阅方回放，并在会话完成或取消时持久化。
- `OrchestrationService` 会缓存项目级智能体运行时，并为内置角色加载 MCP 工具。
- Provider 解析会组合账户配置、解密后的参数、环境变量以及 Copilot 令牌交换结果。
- `FileAppService` 会把文件读取限制在工作区根目录下，避免路径穿越。

## 构建与运行
```powershell
dotnet build src\application\OpenStaff.Application\OpenStaff.Application.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host
```

## 维护说明
- 不要把控制器职责放进此程序集；应通过契约交给 `OpenStaff.HttpApi` 暴露。
- 新增用例时优先通过 `OpenStaffApplicationModule` 注册。
- 这里的运行时组件必须适合单例和后台执行场景。
