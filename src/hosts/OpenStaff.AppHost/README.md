# OpenStaff.AppHost

## Purpose and responsibilities
`OpenStaff.AppHost` is the .NET Aspire development host for this solution. Its job is to compose the runnable application graph for local development rather than to provide shared library functionality.

## Architectural position
- Target framework: `net10.0`
- Executable Aspire host (`IsAspireHost=true`)
- Top-level local orchestration entry point
- Coordinates backend and frontend processes for the developer environment

## Key components
- `Program.cs`
  - `DistributedApplication.CreateBuilder(args)` creates the Aspire application model
  - `AddProject<Projects.OpenStaff_HttpApi_Host>("api")` registers the backend API project
  - `AddViteApp("web", "../../../web/apps/web-antd")` starts the Ant Design Vue frontend from the rebuilt single-app workspace
  - `.WithReference(api)` wires frontend service discovery and dependency metadata to the API
  - `.WithEnvironment("VITE_OPENSTAFF_PROXY_TARGET", api.GetEndpoint("http"))` injects the backend base address used by the Vite proxy
  - `.WithEndpoint("http", e => { e.Port = 5666; e.IsProxied = false; })` exposes the frontend dev server on port `5666`
  - `.WithExternalHttpEndpoints()` makes the Vite endpoint reachable outside Aspire's internal mesh
  - `.WaitFor(api)` delays the frontend until the API is ready

## Important dependencies
- `Aspire.AppHost.Sdk` `13.2.1`
- `Aspire.Hosting.AppHost`
- Project reference: `..\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj`

## Runtime and host behavior
- This host starts the API project and the Vite frontend together for local development.
- The app host does not provision a database container; the current setup relies on the API/infrastructure default SQLite behavior.
- The frontend is launched from `web\apps\web-antd`, and Aspire injects `VITE_PORT=5666` plus the backend proxy target.
- The workspace still assumes `pnpm install` has already been executed before the app host starts.
- Port `5666` is explicitly assigned to the frontend endpoint and is not proxied.
- Because this is an Aspire host, runtime dashboards and supporting endpoints follow Aspire's normal local-development behavior.

## Build and run commands
Run these from the repository root unless noted otherwise:

```powershell
cd web
pnpm install
cd ..
dotnet build src\hosts\OpenStaff.AppHost\OpenStaff.AppHost.csproj
dotnet run --project src\hosts\OpenStaff.AppHost\OpenStaff.AppHost.csproj
```

If you only need the backend process, run `dotnet run --project src\hosts\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj` instead.
