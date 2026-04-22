# OpenStaff.Provider.Abstractions

`OpenStaff.Provider.Abstractions` 是 OpenStaff 中用于 Provider 发现与 Provider 账户配置的共享抽象层。它本身不实现任何具体厂商协议，而是定义协议模型、环境配置约定、元数据结构，以及供各个具体 Provider 包接入的工厂服务。

## 职责
- 通过 `ProviderAbstractionsModule` 注册 `IProtocolFactory`。
- 通过 `ProviderOptions` 维护可发现的协议类型列表。
- 定义 `IProtocol`、`ProtocolBase<TProtocolEnv>` 和 `VendorProtocolBase<TProtocolEnv>`。
- 通过 `ProtocolMetadata` 与 `ProtocolEnvField` 为 API 和 UI 构建协议元数据。
- 通过 `ProtocolEnvSerializer` 序列化与反序列化 Provider 环境配置 JSON，并对敏感字段做选择性加密。
- 通过 `ModelInfo` 与 `ModelProtocolType` 统一模型发现输出。
- 将具体 Provider 模块接入共享的 `OpenStaff.Plugins.ModelDataSource` 模型目录。

## 配置与环境模型
所有 Provider 环境类型都继承自以下基类之一：
- `ProtocolEnvBase`：必须提供 `BaseUrl`。
- `ProtocolApiKeyEnvironmentBase`：在此基础上增加 `ApiKeyFromEnv`、`ApiKeyEnvName` 和加密存储的 `ApiKey`。

`ProtocolFactory.GetProtocolMetadata()` 会反射环境类型的公共属性，并输出适合 UI 渲染的字段类型：
- `string`
- 被 `[Encrypted]` 标记的字段会映射为 `secret`
- `bool`
- `number`

`ProtocolEnvSerializer` 只会在提供加密委托时加密被 `[Encrypted]` 标记的字符串属性；反序列化时也只会尝试解密“看起来像密文”的值，因此历史明文配置仍可继续使用。

## 认证预期
这个项目本身不直接对任何远程服务做认证。它只定义 Provider 如何描述凭据与端点。具体使用 API Key、OAuth Token 还是无需密钥，取决于各个具体 Provider 实现。

## 关键类型
- `ProviderAbstractionsModule`
- `IProtocol`
- `ProtocolBase<TProtocolEnv>`
- `VendorProtocolBase<TProtocolEnv>`
- `IProtocolFactory` / `ProtocolFactory`
- `ProtocolEnvSerializer`
- `ProviderOptions`
- `ModelInfo`、`ModelProtocolType`
- `ProtocolMetadata`、`ProtocolEnvField`

## 与具体 Provider 的关系
所有具体 Provider 模块都依赖 `ProviderAbstractionsModule`，并通过 `Configure<ProviderOptions>(options => options.AddProtocol<TProtocol>())` 把自己的协议注册进来。

下游模块会直接使用这一层：
- `ProtocolsController` 通过 `IProtocolFactory.GetProtocolMetadata()` 向 API 暴露协议元数据。
- Provider 账户相关服务通过 `ProtocolEnvSerializer` 序列化和解密环境配置 JSON。
- `ChatClientFactory` 通过 `CreateProtocolWithEnv(...)` 创建已注入配置的协议实例，用于判断模型与协议的兼容关系。

## 值得注意的细节
- `VendorProtocolBase<TProtocolEnv>` 并不会直接调用厂商 API，而是读取共享的 `IModelDataSource` 目录，并且只返回支持文本输入、文本输出和函数调用的模型。
- `ProtocolFactory.CreateProtocolWithEnv(...)` 使用宽松布尔转换器，因此 `"true"`、`1` 这类 JSON 值也能正确还原为 `bool`。
- `ModelInfo` 使用 `VendorSlug` 表达上游模型所属厂商信息。
