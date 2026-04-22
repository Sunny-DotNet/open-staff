# OpenStaff.Provider.Google

`OpenStaff.Provider.Google` 为 Provider 系统注册 Google Generative Language 支持。该项目只贡献一个协议 `GoogleProtocol`，模型发现依赖共享模型目录，而不是在发现阶段直接调用 Google API。

## 职责
- 通过 `OpenStaffProviderGoogleModule` 注册 `google` 协议键。
- 声明支持 `GoogleGenerateContent` 协议族。
- 通过 `VendorProtocolBase<GoogleProtocolEnv>` 和共享 `IModelDataSource` 解析 Google 模型条目。

## 配置与环境模型
`GoogleProtocolEnv` 继承自 `ProtocolApiKeyEnvironmentBase`，因此环境配置包含：
- `BaseUrl`（默认值 `https://generativelanguage.googleapis.com/v1beta2`）
- `ApiKeyFromEnv`（默认 `false`）
- `ApiKeyEnvName`（默认 `GOOGLE_API_KEY`）
- `ApiKey`（敏感字段）

该配置对象的元数据生成、敏感字段处理和 JSON 还原均由抽象层负责。

## 认证预期
运行时请求需要使用 Google API Key。密钥可以直接存储在 `ApiKey` 中，也可以在启用 `ApiKeyFromEnv` 后从环境变量中解析。

本项目不会直接向 Google API 发起模型发现请求；它只负责向 OpenStaff 其他部分提供协议元数据与默认配置。

## 关键类型
- `OpenStaffProviderGoogleModule`
- `GoogleProtocol`
- `GoogleProtocolEnv`

## 与 Provider 抽象层的关系
- 依赖 `ProviderAbstractionsModule`。
- 通过 `ProviderOptions` 注册 `GoogleProtocol`。
- 继承 `VendorProtocolBase<TProtocolEnv>`，因此会从抽象层获得厂商目录发现逻辑以及 `IsVendor = true` 行为。

## 值得注意的细节
- `ProtocolName` 在代码中是小写字符串 `google`，更贴近共享厂商 slug，而不是常见的产品品牌写法。
- 默认基地址指向 `v1beta2` 版本的 Generative Language 接口。
- 该协议暴露的是 `GoogleGenerateContent` 标记，因此调用方需要将它视为 Google 原生协议，而不是 OpenAI 兼容协议。
