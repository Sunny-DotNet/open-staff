# OpenStaff.Application.Contracts

## Purpose
This project defines the public application boundary for OpenStaff. It contains the interfaces and DTOs consumed by HTTP controllers, UI clients, and any other adapter that needs a stable contract without depending on application implementation or persistence details.

## Architectural position
- Upstream consumers: `OpenStaff.HttpApi`, tests, and any client that needs serialized request and response shapes.
- Downstream implementer: `OpenStaff.Application`.
- Not responsible for: database access, orchestration execution, provider calls, HTTP routing, or SignalR hosting.

## Key namespaces and components
- `AgentRoles`, `Agents`, `Projects`, `Sessions`, `Tasks`: service interfaces and request/response DTOs for the main product flows.
- `Files`: file tree, file content, diff, and checkpoint contracts.
- `Providers` and `Auth`: provider-account management contracts and GitHub device-auth DTOs.
- `McpServers` and `Marketplace`: MCP server definition/configuration/binding contracts and marketplace search/install payloads.
- `Monitor`, `ModelData`, `Settings`: operational telemetry, model-catalog, and system-settings contracts.
- `Common\PagedResult<T>`: shared paging wrapper.
- `OpenStaffApplicationContractsModule`: module marker used by the modular startup system.

## Important dependencies
- `OpenStaff.Core`: shared enums, model concepts, and the modularity base type used by the module marker.
- `OpenStaff.Plugins.ModelDataSource`: model-catalog types referenced by `ModelData` DTOs.
- `OpenStaff.Provider.Abstractions`: keeps the contract layer aligned with provider and protocol metadata used by downstream transport code.

## DI and runtime responsibilities
This assembly intentionally has almost no runtime behavior.
- It publishes interfaces such as `IProjectAppService`, `ISessionAppService`, `IMcpServerAppService`, `IProviderAccountAppService`, and `ISettingsAppService`.
- `OpenStaffApplicationContractsModule` exists so other modules can depend on the contract surface.
- Concrete registrations live in `OpenStaff.Application`, not here.

## HTTP and API relevance
The DTOs in this project are the shapes serialized by the REST layer and related session tooling:
- project lifecycle payloads
- session, frame, event, and chat-message payloads
- task timelines and agent event feeds
- MCP server, configuration, and binding payloads
- provider account, model catalog, monitor, and settings payloads

This project does not expose controllers or SignalR hubs on its own.

## Build
```powershell
dotnet build src\application\OpenStaff.Application.Contracts\OpenStaff.Application.Contracts.csproj
```

## Notes for maintainers
- Add new public app-service interfaces here before exposing new HTTP endpoints.
- Keep DTOs transport-safe and implementation-agnostic.
- Do not add database, orchestration, or host logic to this assembly.
