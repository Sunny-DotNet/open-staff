# OpenStaff.Provider.Tests

## 测试范围

该项目用于验证 Provider 家族的协议发现与元数据行为。它不会对 Provider 做纯 Mock，而是直接启动真实的模块图，解析 `IProtocolFactory`，并检查各个已注册协议是否能够被创建，以及是否暴露出预期的模型和配置元数据。

## 主要测试套件与夹具

- **`ProtocolModelsTests`**
  - 实现了 `IAsyncLifetime`
  - 通过 `AddOpenStaffModules<ProviderTestModule>()` 构建新的 DI 容器
  - 调用 `UseOpenStaffModules()`
  - 初始化共享的 `IModelDataSource`
  - 然后复用 `IProtocolFactory` 执行断言
- **`ProviderTestModule`**
  - 聚合了以下生产模块：
    - OpenAI
    - Anthropic
    - Google
    - NewApi
    - GitHub Copilot

当前断言重点覆盖：

- 所有已注册协议都可以被实例化
- Vendor 协议（`openai`、`anthropic`、`google`）都能从共享模型数据源返回至少一个模型
- `github-copilot` 在不走真实认证流程的情况下也应存在
- `newapi` 在未配置 `BaseUrl` 时仍然可发现，并返回空模型列表
- 所有已注册协议都应暴露协议元数据与 `EnvSchema`

## 对生产项目的依赖

`OpenStaff.Provider.Tests.csproj` 直接引用了：

- `..\OpenStaff.Provider.Abstractions\OpenStaff.Provider.Abstractions.csproj`
- `..\OpenStaff.Provider.OpenAI\OpenStaff.Provider.OpenAI.csproj`
- `..\OpenStaff.Provider.Anthropic\OpenStaff.Provider.Anthropic.csproj`
- `..\OpenStaff.Provider.Google\OpenStaff.Provider.Google.csproj`
- `..\OpenStaff.Provider.NewApi\OpenStaff.Provider.NewApi.csproj`
- `..\OpenStaff.Provider.GitHubCopilot\OpenStaff.Provider.GitHubCopilot.csproj`
- `..\..\OpenStaff.Core\OpenStaff.Core.csproj`

实际运行时，这个测试项目也会一并覆盖这些 Provider 模块带入的模块系统和模型目录装配链路。

## 运行方式

在仓库根目录执行：

```powershell
dotnet test src\providers\OpenStaff.Provider.Tests\OpenStaff.Provider.Tests.csproj
```

只运行协议测试套件：

```powershell
dotnet test src\providers\OpenStaff.Provider.Tests\OpenStaff.Provider.Tests.csproj --filter "FullyQualifiedName~ProtocolModelsTests"
```

## 这些测试要保护什么

这些测试主要用于防止以下方面发生回归：

- Provider 模块注册或协议工厂发现失败
- Vendor 模型目录通过共享数据源加载失败
- 配置与管理流程依赖的协议元数据缺失
- 对于无需真实凭据或完整端点配置也应可发现的协议，其默认行为被意外破坏
