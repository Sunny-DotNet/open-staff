# OpenStaff.ServiceDefaults

## Purpose and responsibilities
`OpenStaff.ServiceDefaults` is the shared .NET Aspire bootstrap package for service hosts in this repository. It centralizes cross-cutting host defaults such as OpenTelemetry, health checks, service discovery, and resilient `HttpClient` setup.

## Architectural position
- Target framework: `net10.0`
- Shared project (`IsAspireSharedProject=true`)
- Intended to be referenced by runnable service applications, not used as a business-logic library
- Lives at the host/bootstrap layer rather than the domain or infrastructure layers

## Key namespaces and components
- `Microsoft.Extensions.Hosting.Extensions`
  - `AddServiceDefaults<TBuilder>()`
    - Applies telemetry, health checks, service discovery, and default `HttpClient` policies
  - `ConfigureOpenTelemetry<TBuilder>()`
    - Adds OpenTelemetry logging, metrics, and tracing
  - `AddDefaultHealthChecks<TBuilder>()`
    - Registers a `self` health check tagged with `live`
  - `MapDefaultEndpoints(WebApplication)`
    - Maps `/health` and `/alive` in development environments

## Important dependencies
- Framework reference: `Microsoft.AspNetCore.App`
- `Microsoft.Extensions.Http.Resilience`
- `Microsoft.Extensions.ServiceDiscovery`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Instrumentation.Runtime`

## Runtime and host behavior
- `AddServiceDefaults()` enables service discovery and applies the standard resilience handler plus service discovery to `HttpClient`.
- `ConfigureOpenTelemetry()` captures ASP.NET Core, `HttpClient`, and runtime metrics/traces, and includes formatted log messages and scopes.
- OTLP export is enabled only when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured.
- `MapDefaultEndpoints()` exposes `/health` and `/alive` only in development, keeping those endpoints out of production by default.
- The extensions live in the `Microsoft.Extensions.Hosting` namespace so consuming hosts can opt in with familiar startup code.
- This project has no standalone process; its behavior appears only when another application calls the extension methods.

## Build and usage commands
Run these from the repository root:

```powershell
dotnet build src\hosts\OpenStaff.ServiceDefaults\OpenStaff.ServiceDefaults.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj
```

The second command is how you observe these defaults in a real host, because this project itself is not directly runnable.
