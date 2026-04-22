# OpenStaff.Application

## Purpose
This project contains the concrete application-layer implementation for OpenStaff. It wires provider integrations, agent orchestration, session execution, seeding, and app-service implementations behind the contracts defined in `OpenStaff.Application.Contracts`.

## Architectural position
- Consumed by: the `OpenStaff.HttpApi.Host` host.
- Implements: `OpenStaff.Application.Contracts`.
- Depends on: infrastructure, agent vendors, provider vendors, marketplace modules, and model-data plugins.
- Boundary: application coordination and use-case logic; transport lives in `OpenStaff.HttpApi`, hosting lives in `OpenStaff.HttpApi.Host`.

## Key namespaces and components
- `Projects`: `ProjectAppService` and `ProjectService` handle project lifecycle, workspace initialization, start/export/import, and README lookup.
- `Sessions`: `SessionAppService`, `SessionRunner`, `SessionStreamManager`, `ProjectGroupExecutionService`, and `ProjectGroupCapabilityService` implement stacked frame execution, pause/resume, event buffering, and session history queries.
- `Orchestration`: `OrchestrationService` and `AgentMcpToolService` manage project-scoped agent caching, provider resolution, runtime warm-up, and MCP tool binding.
- `Providers`: `ProviderAccountAppService`, `ProviderAccountService`, `ProviderResolver`, `ApiKeyResolver`, and `ScopedProviderResolverProxy` create executable provider contexts from stored accounts and secrets.
- `Auth`: `DeviceAuthAppService`, `GitHubDeviceAuthService`, and `CopilotTokenService` implement GitHub device auth and Copilot token exchange.
- `AgentRoles` and `Agents`: role management, test chat, project agent assignment, event feeds, and direct agent messaging.
- `Files`, `Tasks`, `Monitor`, `ModelData`, `McpServers`, `Settings`, `Marketplace`: concrete implementations for the remaining public application services.
- `Seeding`: startup hosted services now focus on MCP hard reset, capability seeding, and runtime preload tasks instead of seeding embedded builtin roles.
- `OpenStaffApplicationModule`: the application-layer composition root.

## Important dependencies
- `OpenStaff.Application.Contracts`: public interfaces and DTOs implemented here.
- `OpenStaff.Infrastructure`: EF Core persistence, encryption, git integration, and backing services.
- `OpenStaff.Agent.*` plus vendor assemblies: AI agent providers and runtime adapters.
- `OpenStaff.Provider.*`: provider abstractions and vendor-specific provider implementations.
- `OpenStaff.Marketplace.*`: marketplace discovery and installation logic.
- `ModelContextProtocol` and `System.Reactive`: MCP connectivity and replay/stream support.
- `Microsoft.AspNetCore.App`: DI, hosted services, HTTP client factory, and background runtime primitives.

## DI and runtime responsibilities
`OpenStaffApplicationModule` is the composition point for application behavior.
- Registers vendor agent providers as both `IAgentProvider` and `IVendorAgentProvider`.
- Registers singleton runtimes such as `OrchestrationService`, `SessionRunner`, `SessionStreamManager`, `ProjectGroupExecutionService`, `ProjectGroupCapabilityService`, and the platform `McpHub`.
- Registers scoped use-case services such as `ProjectService`, `ProjectAgentService`, `SettingsService`, `ProviderAccountService`, `ProviderResolver`, and all app-service implementations.
- Adds `GitHubDeviceAuthService` through `AddHttpClient`.
- Starts hosted services such as `McpHardResetService`, role capability seeding, and MCP preload coordination.

## Runtime and API behavior
Although this project does not host HTTP endpoints, it drives most runtime behavior behind them.
- Session creation can reuse an active session per project scene and starts execution in the background.
- `SessionRunner` manages frame stacks, awaiting-user-input resumes, cancellation tokens, and project-group execution.
- `SessionStreamManager` keeps active session events in memory, replays them to subscribers, and persists them when sessions complete or cancel.
- `OrchestrationService` caches project-scoped agent runtimes and loads MCP tools for builtin roles.
- Provider resolution combines stored account data, decrypted config, environment variables, and Copilot token exchange.
- `FileAppService` constrains reads to the workspace root to prevent path traversal.

## Build and run
```powershell
dotnet build src\application\OpenStaff.Application\OpenStaff.Application.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host
```

## Notes for maintainers
- Keep controller concerns out of this assembly; expose them through contracts and `OpenStaff.HttpApi`.
- Register new use cases through `OpenStaffApplicationModule`.
- Runtime components here must remain safe for singleton and background execution scenarios.
