# OpenStaff.Provider.OpenAI

`OpenStaff.Provider.OpenAI` 为 Provider 系统补充 OpenAI 协议定义。这个项目很专注：模块负责注册 `OpenAIProtocol`，协议负责描述 OpenStaff 应该如何识别 OpenAI 账户与模型。这里的模型发现依赖共享模型目录，而不是直接请求 OpenAI API。

## 职责
- 通过 `OpenStaffProviderOpenAIModule` 注册 `openai` 协议键。
- 声明 OpenAI 同时支持 `OpenAIChatCompletions` 与 `OpenAIResponse` 两种运行时风格。
- 通过 `VendorProtocolBase<OpenAIProtocolEnv>` 从共享 `IModelDataSource` 映射 OpenAI 模型。

## 配置与环境模型
`OpenAIProtocolEnv` 继承自 `ProtocolApiKeyEnvironmentBase`，因此暴露出的环境配置字段包括：
- `BaseUrl`（默认值 `https://api.openai.com/v1`）
- `ApiKeyFromEnv`（默认 `false`）
- `ApiKeyEnvName`（默认 `OPENAI_API_KEY`）
- `ApiKey`（在抽象层中按敏感字段处理）

该环境类型的 JSON 架构生成、加密和反序列化都由抽象层统一处理。

## 认证预期
运行时需要使用 OpenAI API Key。OpenStaff 可以直接把密钥存入 `ApiKey`，也可以在启用 `ApiKeyFromEnv` 后从进程环境变量中读取。

本项目在模型发现阶段不会做 token 交换，也不会直接调用 OpenAI REST API。

## 关键类型
- `OpenStaffProviderOpenAIModule`
- `OpenAIProtocol`
- `OpenAIProtocolEnv`

## 与 Provider 抽象层的关系
- 依赖 `ProviderAbstractionsModule`。
- 通过 `ProviderOptions` 注册 `OpenAIProtocol`。
- 复用 `VendorProtocolBase<TProtocolEnv>`，因此 `IsVendor` 固定为 `true`，模型枚举来自共享厂商目录。

## 值得注意的细节
- 该协议同时声明 Chat Completions 和 Responses 支持，因此同一个 OpenAI 账户可以服务两种运行时。
- 只有在共享目录中被标记为支持文本输入、文本输出和函数调用的模型才会被暴露出来。
- `ProtocolKey` 为 `openai`，`ProtocolName` 为 `OpenAI`，`Logo` 为 `OpenAI`。
