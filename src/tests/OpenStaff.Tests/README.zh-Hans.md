# OpenStaff.Tests

## 测试范围

该项目承载 OpenStaff 主要的服务端测试。当前有效测试主要位于 `Unit\` 目录，重点覆盖领域逻辑，以及围绕 EF Core、应用服务、编排流程、Agent 组装和 API 投影的轻量级集成行为。`E2E\` 目录目前已存在，但此项目的现有覆盖重点仍然是单元测试和基于内存环境的集成测试。

## 主要测试套件与夹具

- **Agent 组装与工具能力**
  - `AgentFactoryTests`
  - `StandardAgentTests`（当前实际包含 `BuiltinAgentProviderTests`）
  - `AgentPromptGeneratorTests`
  - `AgentMessageObserversTests`
  - `ChatClientFactoryTests`
- **编排与派发行为**
  - `TaskGraphTests`
  - `OrchestrationServiceTests`
  - `ApplicationAgentRunFactoryTests`
  - `ProjectGroupExecutionServiceTests`
  - `ProjectGroupCapabilityServiceTests`
- **应用服务与 API 合约**
  - `ProjectServiceTests`
  - `ProjectAgentServiceTests`
  - `SessionAppServiceTests`
  - `MonitorAppServiceTests`
  - `SettingsAppServiceTests`
  - `SessionsControllerTests`
  - `RuntimeProjectionContractsTests`
- **安全与协议配置**
  - `EncryptionServiceTests`
  - `ProtocolEnvSerializerTests`

夹具模式说明：

- 当前项目没有共享的 xUnit collection fixture。
- 偏服务层的测试通常会为每个用例创建独立的 `TestContext`，其中包含内存 SQLite、真实 EF Core 迁移和最小化的 `ServiceCollection` 装配。
- Agent/Provider 相关测试则通常结合轻量依赖注入和 `Moq` 来隔离协作者。

## 对生产项目的依赖

`OpenStaff.Tests.csproj` 直接引用了以下生产项目：

- `..\OpenStaff.Core\OpenStaff.Core.csproj`
- `..\agents\OpenStaff.Agent.Abstractions\OpenStaff.Agent.Abstractions.csproj`
- `..\agents\OpenStaff.Agent.Builtin\OpenStaff.Agent.Builtin.csproj`
- `..\agents\OpenStaff.Agent.Services.Adapters\OpenStaff.Agent.Services.Adapters.csproj`
- `..\OpenStaff.Application\OpenStaff.Application.csproj`
- `..\OpenStaff.Infrastructure\OpenStaff.Infrastructure.csproj`
- `..\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj`

测试项目自身还使用了 xUnit、`Microsoft.NET.Test.Sdk`、`Moq` 和 `coverlet.collector`。

## 运行方式

在仓库根目录执行：

```powershell
dotnet test src\tests\OpenStaff.Tests\OpenStaff.Tests.csproj
```

仅运行某一类测试：

```powershell
dotnet test src\tests\OpenStaff.Tests\OpenStaff.Tests.csproj --filter "FullyQualifiedName~ProjectServiceTests"
```

采集覆盖率：

```powershell
dotnet test src\tests\OpenStaff.Tests\OpenStaff.Tests.csproj --collect:"XPlat Code Coverage"
```

## 这些测试要保护什么

这些测试主要用于防止以下方面发生回归：

- Agent/Provider 注册、Prompt 加载以及工具解析失效
- 任务依赖排序、派发解析、排队、重试、运行时缓存失效等编排规则被破坏
- 项目与会话生命周期规则出现偏差，包括脑暴会话/项目群聊的入口限制与级联清理
- 运行时元数据投影到应用层/API DTO 时丢失或映射错误，影响前端与监控页面
- 协议环境配置的加密与序列化处理出错，导致敏感信息泄露或读取异常
