# OpenStaff.Agent.Vendor.Anthropic

## 作用

本项目实现 OpenStaff 的 `anthropic` 厂商智能体提供器。它通过 Anthropic SDK 集成创建 Claude 系列 `AIAgent`，而不是走内置 Provider 的通用路径。这个项目的职责，是为 Anthropic 角色提供独立的 ProviderType、厂商模型发现能力，以及直接由 SDK 驱动的运行时行为。

## 与 OpenStaff 的集成方式

- `AnthropicAgentProvider` 同时实现 `IAgentProvider` 和 `IVendorAgentProvider`。
- `OpenStaffApplicationModule` 将它注册为单例；当 `AgentRole.ProviderType` 为 `anthropic` 时，`AgentFactory` 会把角色路由到这里。
- `AgentRoleAppService` 调用 `GetConfigSchema()` 与 `GetModelsAsync()`，让界面可以渲染 Anthropic 专属的角色配置和模型选项。
- 在调用 `CreateAgentAsync()` 前，`ProviderResolver` 需要先把所选 Provider 账号解析成带有 `ResolvedProvider.ApiKey` 的运行时对象。

## 认证与运行时前提

- 必须能够解析出 Anthropic API Key。
- 生效模型来自 `AgentRole.Config` 中的 `model` 字段，而不是 `role.ModelName`；默认值是 `claude-sonnet-4-20250514`。
- 当前实现默认直接连接 Anthropic 官方服务，不会读取 `ResolvedProvider` 中的自定义 BaseUrl。

## SDK 与协议行为

- 关键依赖：`Microsoft.Agents.AI.Anthropic`，其中提供了此处使用的 `AnthropicClient` 集成能力。
- `CreateAgentAsync()` 通过 `AnthropicClient { ApiKey = apiKey }` 创建客户端。
- 随后直接调用 `client.AsAIAgent(model: ..., name: ..., instructions: ...)` 生成智能体，因此 Anthropic 侧的协议细节由该适配层处理，而不是通过 `ChatClientAgent` 包装。
- 模型发现优先使用 `IModelDataSource` 的动态元数据；当数据源未就绪时，回退到本地 `FallbackModels` 列表。

## 重要类型

- `AnthropicAgentProvider`：本项目唯一的运行时类，也是 Claude 厂商入口。
- `VendorModel` / `FallbackModels`：Claude 家族的本地回退模型目录。
- `AgentConfigSchema` / `AgentConfigField`：提供给前端的动态配置定义。

## 注意事项

- `ResolvedProvider.BaseUrl` 会被忽略，因此当前实现不支持 Anthropic 兼容代理或其他托管终结点。
- `role.ModelName` 不会参与创建过程，实际以 `role.Config` 为准。
- 构造函数保存了 `ILoggerFactory`，但当前创建出来的 Anthropic 智能体并不会接收这个日志工厂。
