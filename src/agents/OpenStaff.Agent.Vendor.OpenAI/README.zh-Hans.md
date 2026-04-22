# OpenStaff.Agent.Vendor.OpenAI

## 作用

本项目实现 OpenStaff 的 `openai` 厂商智能体提供器。它通过官方 `OpenAI` SDK 和 `Microsoft.Extensions.AI` 适配层创建基于 OpenAI GPT 系列模型的 `AIAgent`。当角色需要直接连接 OpenAI 或 OpenAI 兼容网关，而不是走内置的通用多协议路径时，就会使用这个项目。

## 与 OpenStaff 的集成方式

- `OpenAIAgentProvider` 同时实现 `IAgentProvider` 和 `IVendorAgentProvider`。
- `OpenStaffApplicationModule` 将它注册为单例；当 `AgentRole.ProviderType` 为 `openai` 时，`AgentFactory` 会把角色路由到这里。
- `AgentRoleAppService` 调用 `GetConfigSchema()` 和 `GetModelsAsync()`，为应用中的厂商角色表单和模型下拉框提供数据。
- `ProviderResolver` 会在 `CreateAgentAsync()` 之前解出 `ResolvedProvider.ApiKey` 与 `ResolvedProvider.BaseUrl`，供本提供器使用。

## 认证与运行时前提

- 已解析的 Provider 账号中必须包含 API Key。
- 实际模型读取自 `AgentRole.Config` 里的 `model` 字段，而不是 `role.ModelName`；默认值是 `gpt-4o`。
- 运行时需要能够访问配置好的 OpenAI 终结点，或兼容 OpenAI Chat API 的代理/网关。

## SDK 与协议行为

- 关键依赖：`Microsoft.Agents.AI`、`Microsoft.Agents.AI.OpenAI`、`OpenAI`。
- `CreateAgentAsync()` 使用 `ApiKeyCredential` 构造 `OpenAIClient`。
- 如果 `ResolvedProvider.BaseUrl` 有值，会原样写入 `OpenAIClientOptions.Endpoint`。这是刻意设计，用来兼容反向代理和 OpenAI 兼容网关。
- 随后通过 `GetChatClient(model).AsIChatClient()` 取得聊天客户端，再包装成 `ChatClientAgent`。
- 模型发现优先使用 `IModelDataSource` 提供的动态元数据；如果插件不可用，则回退到本地 `FallbackModels` 列表。

## 重要类型

- `OpenAIAgentProvider`：提供器实现，也是创建智能体的主入口。
- `VendorModel` / `FallbackModels`：动态模型数据源不可用时使用的静态模型元数据。
- `AgentConfigSchema` / `AgentConfigField`：暴露给前端的动态配置契约。

## 注意事项

- 创建智能体时当前不会使用 `AgentContext`。
- 代码不会读取 `role.ModelName`，因此要确保 `role.Config` 中的 `model` 与预期一致。
- `BaseUrl` 会被直接透传，地址格式不正确时会在运行时失败，而不会在这里被纠正。
