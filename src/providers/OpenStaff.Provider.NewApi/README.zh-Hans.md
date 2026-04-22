# OpenStaff.Provider.NewApi

`OpenStaff.Provider.NewApi` 用于把 NewApi / OneAPI 风格的网关接入 Provider 系统。与面向单一厂商的 Provider 包不同，这个项目描述的是一个多厂商网关，模型发现依赖网关元数据动态获取，而不是使用固定的厂商目录。

## 职责
- 通过 `OpenStaffProviderNewApiModule` 注册 `newapi` 协议键。
- 调用已配置网关的 `/api/pricing` 端点发现可用模型。
- 将网关中的 endpoint 名称映射为 OpenStaff 内部协议标记，例如 Chat Completions、Responses、Anthropic Messages 和 Google Generate Content。
- 在可能的情况下，结合共享 `IModelDataSource` 规范化模型名与厂商名。

## 配置与环境模型
`NewApiProtocolEnv` 继承自 `ProtocolApiKeyEnvironmentBase`，暴露以下字段：
- `BaseUrl`（默认空字符串；未配置时不会进行发现）
- `ApiKeyFromEnv`（默认 `false`）
- `ApiKeyEnvName`（默认 `NEW_API_AUTH_TOKEN`）
- `ApiKey`（敏感字段）

虽然是网关协议，但它的环境对象依然由抽象层处理，因此敏感字段处理和元数据架构与其他 Provider 保持一致。

## 认证预期
环境模型中包含 API Key 相关字段，因为系统其他位置在真正调用网关时可能需要认证。但 `ModelsAsync()` 目前对 `{BaseUrl}/api/pricing` 发起的是一个不带认证头的 `GET` 请求。

这意味着：
- 如果网关公开 pricing 接口，模型发现可以直接工作；
- 如果 pricing 接口需要认证，发现过程会失败并返回空列表；
- 当前配置的 `ApiKey` 更偏向下游运行时使用的元数据，而不是此发现路径会主动发送的凭据。

## 关键类型
- `OpenStaffProviderNewApiModule`
- `NewApiProtocol`
- `NewApiProtocolEnv`

## 与 Provider 抽象层的关系
- 依赖 `ProviderAbstractionsModule`。
- 通过 `ProviderOptions` 注册 `NewApiProtocol`。
- 继承的是 `ProtocolBase<NewApiProtocolEnv>`，而不是 `VendorProtocolBase<TProtocolEnv>`，因为它需要解析网关返回的 JSON，而不是读取单厂商目录。
- 对共享 `IModelDataSource` 的复用主要发生在标准化模型与厂商标识的阶段。

## 值得注意的细节
- 当 endpoint 元数据缺失或无法识别时，协议会回退到 `OpenAIChatCompletions`。
- 厂商规范化同时使用共享目录中的精确匹配和包含关系模糊匹配，并带有 `zhipuai-coding-plan` 到 `zai` 的硬编码替换。
- 如果网关模型无法匹配到标准目录，协议会保留原始模型名，并使用尽力而为的厂商 slug，而不是直接丢弃该模型。
- `Logo` 当前为空字符串。
