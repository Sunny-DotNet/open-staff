# OpenStaff.Marketplace.Internal

## Purpose

`OpenStaff.Marketplace.Internal` exposes the local MCP server catalog as a marketplace source. It turns rows from the application database into the normalized marketplace model defined by `OpenStaff.Marketplace.Abstractions`.

## Marketplace role

This project is the built-in marketplace provider for already-known or already-installed servers. Its source key is `internal`, and `MarketplaceAppService` treats it as the default source when no source key is supplied.

## Key data source and types

- `InternalMcpSource`
  - implements `IMcpMarketplaceSource`
  - reads from `AppDbContext`
  - searches `McpServers`
  - maps database entities to `MarketplaceServerInfo`

- `OpenStaffMarketplaceInternalModule`
  - depends on `MarketplaceAbstractionsModule`
  - registers `InternalMcpSource` through `MarketplaceOptions.AddSource<InternalMcpSource>()`

## Dependencies

Direct project dependencies:

- `OpenStaff.Marketplace.Abstractions`
- `OpenStaff.Infrastructure`

Runtime dependencies come from those projects:

- `AppDbContext` from `OpenStaff.Infrastructure.Persistence`
- `McpServer` from `OpenStaff.Core.Models`
- EF Core query support via `Microsoft.EntityFrameworkCore`

## Integration points

- registered into the marketplace source list by `OpenStaffMarketplaceInternalModule`
- consumed through `IMarketplaceSourceFactory` by `OpenStaff.Application\Marketplace\MarketplaceAppService`
- surfaced through `OpenStaff.HttpApi\Controllers\MarketplaceController`
- backed by the same database table used to persist installed MCP server definitions

## Operational notes

- Search is database-backed and applies keyword/category filtering directly in EF Core.
- Pagination is offset-based only: `Page` and `PageSize` are used; `Cursor` is ignored.
- `GetByIdAsync` expects a GUID string because local `McpServer` entities are keyed by `Guid`.
- The local entity stores a single `TransportType`; this project wraps it into a `List<string>` so the result matches the cross-source abstraction.
- Returned items are marked with `Source = "internal"` and `IsInstalled = true`, because this source reflects locally persisted servers.
- If the database is empty, this source simply returns empty search results.
