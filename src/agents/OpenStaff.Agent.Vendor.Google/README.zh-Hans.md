# OpenStaff.Agent.Vendor.Google

## 作用

本项目实现 OpenStaff 的 `google` 厂商智能体提供器。它把 `Google.GenAI` SDK 与 `Microsoft.Extensions.AI` 聊天客户端适配层组合起来，创建由 Gemini 驱动的 `AIAgent`。这个 Provider 面向直接使用 Gemini Developer API 的场景，而不是走内置 Provider 的通用实现。

## 与 OpenStaff 的集成方式

- `GoogleAgentProvider` 同时实现 `IAgentProvider` 和 `IVendorAgentProvider`。
- `OpenStaffApplicationModule` 将它注册为单例；当 `AgentRole.ProviderType` 为 `google` 时，`AgentFactory` 会选择这里的实现。
- `AgentRoleAppService` 通过 `GetConfigSchema()` 和 `GetModelsAsync()` 为界面提供 Google 专属角色配置和模型列表。
- 在创建智能体之前，`ProviderResolver` 会通过 `ResolvedProvider.ApiKey` 提供解密后的 API Key。

## 认证与运行时前提

- 已解析的 Provider 账号中必须存在 Google API Key。
- 实际模型来自 `AgentRole.Config` 里的 `model` 字段，而不是 `role.ModelName`；默认值是 `gemini-2.5-flash`。
- 运行时固定走 Gemini Developer API 路径，因为代码使用的是 `Client(vertexAI: false, apiKey: apiKey)`。

## SDK 与协议行为

- 关键依赖：`Google.GenAI` 与 `Microsoft.Agents.AI`。
- `CreateAgentAsync()` 先用 `vertexAI: false` 构造 `Google.GenAI.Client`，再通过 `AsIChatClient(model)` 转成 `IChatClient`。
- 得到的聊天客户端会被包装成 `ChatClientAgent`，因此 OpenStaff 仍然通过统一的 `AIAgent` 抽象运行 Gemini。
- 模型发现优先使用 `IModelDataSource` 的动态元数据；若插件不可用，则回退到本地 `FallbackModels` 列表。

## 重要类型

- `GoogleAgentProvider`：Gemini Provider 的实现，也是创建智能体的入口。
- `VendorModel` / `FallbackModels`：Gemini 的回退模型元数据。
- `AgentConfigSchema` / `AgentConfigField`：暴露给前端的 Provider 配置契约。

## 注意事项

- 当前实现会忽略 `ResolvedProvider.BaseUrl`。
- 这个 Provider 不支持 Vertex AI 的项目、区域或服务账号流程；它假定使用的是普通 Gemini API Key。
- `role.ModelName` 不参与创建逻辑，实际以 `role.Config` 中的 `model` 为准。
