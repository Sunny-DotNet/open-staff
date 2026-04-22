# OpenStaff.Infrastructure

## Purpose and responsibilities
`OpenStaff.Infrastructure` supplies the concrete runtime services behind the core contracts. It owns persistence, Git integration, reversible encryption for stored secrets, project import/export, and plugin discovery.

## Architectural position
- Target framework: `net10.0`
- Depends on `OpenStaff.Core`
- Sits between application code and external resources such as SQLite files, Git repositories, and archive files
- Exposes its registrations through `OpenStaffInfrastructureModule`

## Key namespaces and components
- `OpenStaff.Infrastructure.Persistence`
  - `AppDbContext`
  - EF Core entity configurations
  - `Migrations\`
  - `AppDbContextFactory` for design-time tooling
  - Persists projects, chat sessions, frames, messages, events, checkpoints, provider accounts, MCP settings, and related metadata
- `OpenStaff.Infrastructure.Git`
  - `GitService`
  - Initializes repositories, stages and commits changes, returns diffs, and reads commit history through LibGit2Sharp
- `OpenStaff.Infrastructure.Security`
  - `EncryptionService`
  - Uses AES-256 with a SHA-256-derived key and prefixes the IV into the stored payload
- `OpenStaff.Infrastructure.Export`
  - `ProjectExporter`
  - `ProjectImporter`
  - Packages and restores `.openstaff` archives containing database snapshots and workspace files
- `OpenStaff.Infrastructure.Plugins`
  - `PluginLoader`
  - Reflects over plugin DLLs and initializes `IPlugin` implementations
- `OpenStaffInfrastructureModule`
  - Registers the DbContext, `HttpClient`, Git, export/import, plugin, and encryption services

## Important dependencies
- Project reference: `..\OpenStaff.Core\OpenStaff.Core.csproj`
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.Extensions.Http`
- `LibGit2Sharp`

## Runtime and host behavior
- `OpenStaffInfrastructureModule` depends on `OpenStaffCoreModule`.
- The current implementation always configures EF Core with the SQLite provider.
- The connection string is read from `ConnectionStrings:openstaff` or `ConnectionStrings:DefaultConnection`; if neither is present, it falls back to `%USERPROFILE%\.staff\openstaff.db`.
- `EncryptionService` reads `Security:EncryptionKey`; if it is missing, the module uses a development fallback key that must be replaced in real deployments.
- `AppDbContextFactory` keeps `dotnet ef` commands pointed at the same default SQLite file as runtime.
- `ProjectImporter` validates extracted workspace paths before writing files, so imports stay inside the target workspace root.
- This project is not an executable host; it runs inside higher-level applications such as `OpenStaff.HttpApi.Host`.

## Build and operational commands
Run these from the repository root:

```powershell
dotnet build src\infrastructure\OpenStaff.Infrastructure\OpenStaff.Infrastructure.csproj
dotnet ef migrations list --project src\infrastructure\OpenStaff.Infrastructure\OpenStaff.Infrastructure.csproj
dotnet ef database update --project src\infrastructure\OpenStaff.Infrastructure\OpenStaff.Infrastructure.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj
```

The last command is the practical way to exercise this project at runtime.
