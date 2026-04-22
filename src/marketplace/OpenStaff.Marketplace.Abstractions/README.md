# OpenStaff.Marketplace.Abstractions

## Purpose

`OpenStaff.Marketplace.Abstractions` is the contract layer for the MCP marketplace family. It does not fetch marketplace data itself. Instead, it defines the shared search/detail model and the registration mechanism that concrete sources use.

## Marketplace role

This project is the foundation for all marketplace providers:

- internal/local catalog: `OpenStaff.Marketplace.Internal`
- remote MCP Registry integration: `OpenStaff.Marketplace.Registry`

It gives both implementations a single surface so the application layer can search, enumerate sources, and install servers without source-specific branching.

## Key abstractions

- `IMcpMarketplaceSource`  
  Contract for a marketplace source. Each source exposes:
  - `SourceKey`
  - `DisplayName`
  - `IconUrl`
  - `SearchAsync(...)`
  - `GetByIdAsync(...)`

- `MarketplaceSearchQuery`  
  Shared query model with keyword, category, cursor pagination, and page/page-size support.

- `MarketplaceSearchResult`  
  Normalized result model that supports both offset paging and cursor paging.

- `MarketplaceServerInfo` and `RemoteEndpoint`  
  Unified server metadata returned from any marketplace source, including transport types, install package hints, remotes, and default config.

- `MarketplaceOptions`  
  Holds the list of registered source types. Source modules add themselves with `AddSource<TSource>()`.

- `IMarketplaceSourceFactory` / `MarketplaceSourceFactory`  
  Resolves all configured marketplace sources from DI, lazily instantiates them, and caches the created instances.

## Dependencies

Direct project dependency:

- `OpenStaff.Core`

The project uses the modular infrastructure from `OpenStaff.Core.Modularity` plus `Microsoft.Extensions.DependencyInjection` and options support from the shared framework.

## Integration points

- `OpenStaff.Application\Marketplace\MarketplaceAppService` consumes `IMarketplaceSourceFactory` to:
  - list registered sources
  - route searches to a selected source
  - resolve a source when installing a marketplace item
- `OpenStaff.HttpApi\Controllers\MarketplaceController` exposes that application service through `api/mcp/marketplace`
- concrete source modules register themselves by configuring `MarketplaceOptions`

## Operational notes

- Source instances are created on first use and then cached by `MarketplaceSourceFactory`.
- Source keys must be unique and stable, because callers select a source by `SourceKey`.
- The abstraction allows both cursor-based and page-based sources. Consumers should not assume every provider supports both equally.
- Server IDs are source-defined. For example, one source may use GUIDs while another may use a composite `name:version` identifier.
- Installed-state reconciliation is intentionally outside this project; the application layer augments search results with local installation state.
