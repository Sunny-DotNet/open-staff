# OpenStaff.Tests

## Test scope

This project contains the main server-side test coverage for OpenStaff. The active suites are under `Unit\` and focus on domain logic plus lightweight integration behavior around EF Core, application services, orchestration, agent composition, and API-facing projections. The `E2E\` folder exists, but the current coverage in this project is centered on unit-style and in-memory integration tests.

## Major suites and fixtures

- **Agent composition and tooling**
  - `AgentFactoryTests`
  - `StandardAgentTests` (currently contains `BuiltinAgentProviderTests`)
  - `AgentPromptGeneratorTests`
  - `AgentMessageObserversTests`
  - `ChatClientFactoryTests`
- **Orchestration and dispatch behavior**
  - `TaskGraphTests`
  - `OrchestrationServiceTests`
  - `ApplicationAgentRunFactoryTests`
  - `ProjectGroupExecutionServiceTests`
  - `ProjectGroupCapabilityServiceTests`
- **Application services and API contracts**
  - `ProjectServiceTests`
  - `ProjectAgentServiceTests`
  - `SessionAppServiceTests`
  - `MonitorAppServiceTests`
  - `SettingsAppServiceTests`
  - `SessionsControllerTests`
  - `RuntimeProjectionContractsTests`
- **Security and protocol configuration**
  - `EncryptionServiceTests`
  - `ProtocolEnvSerializerTests`

Fixture pattern notes:

- There are no shared xUnit collection fixtures in this project today.
- Service-heavy suites usually build a per-test `TestContext` with in-memory SQLite, real EF Core migrations, and minimal `ServiceCollection` wiring.
- Agent/provider tests use lightweight dependency injection plus `Moq` to isolate collaborators.

## Dependencies on production projects

`OpenStaff.Tests.csproj` references the following production projects directly:

- `..\OpenStaff.Core\OpenStaff.Core.csproj`
- `..\agents\OpenStaff.Agent.Abstractions\OpenStaff.Agent.Abstractions.csproj`
- `..\agents\OpenStaff.Agent.Builtin\OpenStaff.Agent.Builtin.csproj`
- `..\agents\OpenStaff.Agent.Services.Adapters\OpenStaff.Agent.Services.Adapters.csproj`
- `..\OpenStaff.Application\OpenStaff.Application.csproj`
- `..\OpenStaff.Infrastructure\OpenStaff.Infrastructure.csproj`
- `..\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj`

The test project itself uses xUnit, `Microsoft.NET.Test.Sdk`, `Moq`, and `coverlet.collector`.

## How to run

Run from the repository root:

```powershell
dotnet test src\tests\OpenStaff.Tests\OpenStaff.Tests.csproj
```

Run a focused suite:

```powershell
dotnet test src\tests\OpenStaff.Tests\OpenStaff.Tests.csproj --filter "FullyQualifiedName~ProjectServiceTests"
```

Collect coverage:

```powershell
dotnet test src\tests\OpenStaff.Tests\OpenStaff.Tests.csproj --collect:"XPlat Code Coverage"
```

## What these tests are intended to protect

These tests are meant to catch regressions in:

- agent/provider registration, prompt loading, and tool lookup
- orchestration rules such as task dependency ordering, dispatch parsing, queueing, retries, and runtime cache invalidation
- project and session lifecycle rules, including brainstorm/group-session guards and nested cleanup behavior
- projection of runtime metadata into application/API DTOs consumed by the UI and monitoring surfaces
- encryption and serialization of protocol environment settings so secrets are handled safely
