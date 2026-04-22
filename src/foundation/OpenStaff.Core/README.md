# OpenStaff.Core

## Purpose and responsibilities
`OpenStaff.Core` is the foundation library for the OpenStaff solution. It defines the domain model, agent and orchestration contracts, notification abstractions, plugin interfaces, and the lightweight module system that higher layers build on.

## Architectural position
- Target framework: `net10.0`
- Has no project-to-project dependencies inside this repository
- Sits below infrastructure, agents, and API layers
- Contains contracts and shared types only; it does not start a host, talk to a database, or perform I/O on its own

## Key namespaces and components
- `OpenStaff.Core.Modularity`
  - `OpenStaffModule`
  - `DependsOnAttribute`
  - `ModuleLoader`
  - `ModuleServiceCollectionExtensions`
  - Provides dependency-ordered service registration and application initialization
- `OpenStaff.Core.Agents`
  - `AgentContext`
  - `RoleConfig`
  - Tool registry, prompt loader, and provider resolver interfaces
  - Carries runtime project, role, account, API key, language, and scene information for agent execution
- `OpenStaff.Core.Models`
  - Project, task, checkpoint, and agent-role entities
  - Chat session, frame, message, and event entities
  - Provider-account and MCP-related entities
- `OpenStaff.Core.Orchestration`
  - `TaskGraph`
  - `TaskNode`
  - `IOrchestrator`
  - `TaskGraph` computes ready tasks from dependency state and detects cycles
- `OpenStaff.Core.Notifications`
  - `INotificationService`
  - `Channels` helpers for `global`, `project:{id}`, and `session:{id}`
- `OpenStaff.Core.Plugins`
  - `IPlugin`
  - `IAgentPlugin`
  - `PluginManifest`
- `OpenStaff.Options`
  - `OpenStaffOptions`, including the default working directory under `%USERPROFILE%\.staff`

## Important dependencies
- `Microsoft.Agents.AI`
- `Microsoft.Extensions.Configuration.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Options`
- `Microsoft.Extensions.Options.ConfigurationExtensions`

## Runtime and host behavior
- `OpenStaffCoreModule` is the root OpenStaff module and registers the default `OpenStaffOptions`.
- Consumers load modules with `AddOpenStaffModules<TStartupModule>(configuration)` and then run module initialization with `UseOpenStaffModules()`.
- `AgentContext.Language` defaults to `zh-CN`, so downstream services can make language-aware decisions without adding localization state of their own.
- This project is a shared library; runtime behavior comes from the applications that reference it.

## Build and validation commands
Run these from the repository root:

```powershell
dotnet build src\foundation\OpenStaff.Core\OpenStaff.Core.csproj
dotnet test src\tests\OpenStaff.Tests\OpenStaff.Tests.csproj --filter "FullyQualifiedName~OpenStaff.Tests.Unit"
```

There is no standalone `dotnet run` command for this project.
