# OpenStaff.HttpApi.Host

## Purpose
This project is the ASP.NET Core host for OpenStaff. It boots the modular application, applies database migrations, initializes model-data sources, and exposes the REST and SignalR surface used by the UI.

## Architectural position
- Entry-point executable for local development and deployment.
- Composes `OpenStaff.Application`, `OpenStaff.HttpApi`, and `OpenStaff.ServiceDefaults`.
- Owns hosting concerns such as middleware, CORS, OpenAPI, SignalR, appsettings, and startup tasks.

## Key namespaces and components
- `Program.cs`: creates the web app, loads modules, runs migrations, initializes the model catalog, and maps middleware plus endpoints.
- `OpenStaffHttpApiHostModule`: registers SignalR, OpenAPI, the default CORS policy, and `INotificationService`.
- `Hubs\NotificationHub`: the single SignalR hub for channel membership and session-event streaming.
- `Services\NotificationService`: bridges `INotificationService` into SignalR groups and the in-memory session stream manager.
- `Middleware\ErrorHandlingMiddleware`: converts common exceptions to JSON error responses.
- `Middleware\LocaleMiddleware`: resolves request locale from stored settings, `Accept-Language`, or server culture.
- `appsettings.json` and `appsettings.Development.json`: host configuration, including CORS origins.
- `OpenStaff.HttpApi.Host.http`: manual HTTP request scratch file.
- `Dockerfile`: container packaging for the host process.

## Important dependencies
- `OpenStaff.Application`: application services, orchestration, session runtime, and provider integration.
- `OpenStaff.HttpApi`: MVC controller assembly loaded into the host.
- `OpenStaff.ServiceDefaults`: Aspire defaults for telemetry, service discovery, and health endpoints.
- `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore`: development-time API description and reference UI.
- `Microsoft.EntityFrameworkCore.Design`: EF Core tooling support.
- `System.Reactive`: shared with streaming infrastructure.

## DI and runtime responsibilities
- `OpenStaffHttpApiHostModule` calls `AddSignalR()`, `AddOpenApi()`, and registers the default CORS policy plus `INotificationService`.
- The default CORS policy reads `Cors:Origins` and falls back to `http://localhost:3000`.
- `Program.cs` calls `AddOpenStaffModules<OpenStaffHttpApiHostModule>()` and `UseOpenStaffModules()` so module registration and initialization logic run.
- Startup applies `AppDbContext` migrations automatically.
- Startup also initializes `IModelDataSource` so model refresh and query endpoints are ready immediately.

## HTTP, SignalR, and API behavior
- REST controllers come from `OpenStaff.HttpApi` and are mapped with `app.MapControllers()`.
- The only SignalR hub is `/hubs/notification`.
- `NotificationHub.StreamSession(sessionId)` streams `SessionEvent` objects and joins the caller to the `session:{id}` group.
- `NotificationService` also publishes channel-based notifications to project and session groups.
- OpenAPI and the Scalar reference UI are enabled only in development.
- `MapDefaultEndpoints()` exposes Aspire health and diagnostics endpoints.

## Build and run
```powershell
dotnet build src\hosts\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host
```

## Notes for maintainers
- Keep business logic in `OpenStaff.Application`; this project should stay focused on hosting and cross-cutting transport concerns.
- Keep the single-hub design unless the real-time architecture changes intentionally.
