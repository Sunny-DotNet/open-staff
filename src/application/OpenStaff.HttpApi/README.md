# OpenStaff.HttpApi

## Purpose
This project is the HTTP transport layer for OpenStaff. It exposes thin ASP.NET Core MVC controllers that translate HTTP requests into calls on `OpenStaff.Application.Contracts` interfaces.

## Architectural position
- Loaded into the web host by `OpenStaff.HttpApi.Host`.
- Depends on contracts, not application implementations or persistence.
- Sits between external HTTP clients and the application layer.
- Does not own SignalR hosting, startup orchestration, database access, or agent execution.

## Key namespaces and components
- `OpenStaffHttpApiModule`: registers controllers from this assembly and configures JSON cycle handling.
- `ProjectsController`: project CRUD, workspace initialization, start, README retrieval, export, and import.
- `SessionsController`: session creation, message send, event lookup, frame messages, cancel, pop-frame, active-scene lookup, and paged chat messages.
- `TasksController`: task CRUD, resume-blocked actions, and task timeline queries.
- `AgentsController` and `AgentRolesController`: project agent assignment, agent event feeds, direct agent messages, role CRUD, vendor model lookup, and test chat.
- `ProviderAccountsController`: provider account CRUD, available-model listing, and GitHub device-auth flow.
- `McpServersController` and `MarketplaceController`: MCP definitions/configurations/bindings plus marketplace source, search, and install endpoints.
- `FilesController`: workspace file tree, file content, diffs, and checkpoints.
- `MonitorController`, `ModelDataController`, `SettingsController`, and `ProtocolsController`: health and stats, model catalog, system settings, and provider protocol metadata.

## Important dependencies
- `OpenStaff.Application.Contracts`: injected app-service interfaces and DTOs used by every controller.
- `Microsoft.AspNetCore.App`: MVC base types, routing, model binding, and JSON options.
- Transitive provider/protocol abstractions are used by `ProtocolsController` to expose protocol metadata.

## DI and runtime responsibilities
`OpenStaffHttpApiModule` is intentionally small.
- Calls `AddControllers()`.
- Loads this assembly with `AddApplicationPart(typeof(OpenStaffHttpApiModule).Assembly)`.
- Sets `ReferenceHandler.IgnoreCycles` for JSON serialization.
- Leaves all concrete service registration to `OpenStaff.Application` and all host-level concerns to `OpenStaff.HttpApi.Host`.

## HTTP behavior
- Routes are organized by resource families under `/api/...`.
- Controllers translate application-layer results into `CreatedAtAction`, `Ok`, `NoContent`, `NotFound`, and `BadRequest` responses.
- Some endpoints convert upstream provider failures into `502 Bad Gateway`, especially provider model listing and GitHub device authorization.
- This project does not implement real-time streaming; SignalR endpoints live in `OpenStaff.HttpApi.Host`.

## Build and run
```powershell
dotnet build src\application\OpenStaff.HttpApi\OpenStaff.HttpApi.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host
```

## Notes for maintainers
- Keep controllers thin; add new use cases by extending contracts and their application-layer implementations.
- Do not add persistence or orchestration logic to this assembly.
