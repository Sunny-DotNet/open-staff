# OpenStaff.Provider.Anthropic

`OpenStaff.Provider.Anthropic` 为 Provider 系统注册 Anthropic 支持。和 OpenAI 项目类似，它是一个厂商注册包：模块负责加入 `AnthropicProtocol`，协议负责发布 Anthropic 专属元数据与环境配置，同时复用共享模型目录做模型发现。

## 职责
- 通过 `OpenStaffProviderAnthropicModule` 注册 `anthropic` 协议键。
- 声明 Anthropic 使用 `AnthropicMessages` 协议。
- 通过 `VendorProtocolBase<AnthropicProtocolEnv>` 和共享 `IModelDataSource` 解析可用的 Anthropic 模型。

## 配置与环境模型
`AnthropicProtocolEnv` 继承自 `ProtocolApiKeyEnvironmentBase`，暴露以下字段：
- `BaseUrl`（默认值 `https://api.anthropic.com`）
- `ApiKeyFromEnv`（默认 `false`）
- `ApiKeyEnvName`（默认 `ANTHROPIC_AUTH_TOKEN`）
- `ApiKey`（按敏感字段存储）

抽象层会负责该环境对象的序列化、UI 中的敏感字段标记，以及对明文/密文持久化值的兼容处理。

## 认证预期
运行时需要 Anthropic 凭据。账户可以把 token 放在 `ApiKey` 中，也可以通过 `ApiKeyEnvName` 指示 OpenStaff 从环境变量读取。

本项目在模型发现阶段不会直接请求 Anthropic API；它主要负责定义 Anthropic 在 OpenStaff 中的表示方式。

## 关键类型
- `OpenStaffProviderAnthropicModule`
- `AnthropicProtocol`
- `AnthropicProtocolEnv`

## 与 Provider 抽象层的关系
- 依赖 `ProviderAbstractionsModule`。
- 通过 `ProviderOptions` 注册 `AnthropicProtocol`。
- 继承 `VendorProtocolBase<TProtocolEnv>`，因此会自动获得共享厂商目录查询逻辑以及 `IsVendor = true` 的行为。

## 值得注意的细节
- 该协议只声明 `AnthropicMessages`，没有额外暴露 OpenAI 兼容标记。
- 模型发现仍然依赖共享目录，因此这里的默认 `BaseUrl` 更像是配置元数据，而不是当前项目里真实发起发现请求的地址。
- `Logo` 使用 `Claude.Color`，而 `ProtocolName` 仍然是 `Anthropic`。
