# OpenStaff.Marketplace.Registry

## Purpose

`OpenStaff.Marketplace.Registry` integrates the official MCP Registry at `https://registry.modelcontextprotocol.io` and exposes it as a marketplace source inside OpenStaff.

## Marketplace role

This project is the external marketplace provider in the marketplace family. It complements the local `internal` source by letting OpenStaff browse and install registry-hosted MCP servers from the public catalog.

## Key components and registry models

- `RegistryApiClient`
  - wraps HTTP calls to `/v0/servers`
  - uses a dedicated `HttpClient`
  - returns deserialized `RegistryResponse` models

- `RegistryMcpSource`
  - implements `IMcpMarketplaceSource`
  - maps registry payloads to `MarketplaceServerInfo`
  - performs client-side keyword filtering until the upstream API supports a search parameter
  - resolves individual items by paginating the registry when `GetByIdAsync` is called

- `RegistryModels`
  - contains DTOs for registry payloads such as:
    - `RegistryResponse`
    - `RegistryServerEntry`
    - `RegistryServer`
    - `RegistryPackage`
    - `RegistryRemote`
    - metadata types for pagination and official-release flags

- `OpenStaffMarketplaceRegistryModule`
  - configures a typed `HttpClient` for `RegistryApiClient`
  - registers `RegistryMcpSource` in `MarketplaceOptions`

## Dependencies

Direct project dependencies:

- `OpenStaff.Marketplace.Abstractions`
- `Microsoft.Extensions.Http`

The project also relies on logging abstractions from the shared framework.

## Integration points

- enabled by `OpenStaffMarketplaceRegistryModule`, which depends on `MarketplaceAbstractionsModule`
- included in the application module graph so `IMarketplaceSourceFactory` can discover it
- used by `OpenStaff.Application\Marketplace\MarketplaceAppService` for:
  - remote search
  - remote lookup during install
  - installed-state reconciliation against the local `McpServers` table
- exposed by `OpenStaff.HttpApi\Controllers\MarketplaceController`

## Operational notes

- Default base URL: `https://registry.modelcontextprotocol.io`
- `HttpClient` timeout: 60 seconds
- User-Agent: `OpenStaff/1.0`
- The registry currently does not accept a keyword query parameter, so name/description filtering happens in `RegistryMcpSource`.
- Search prefers entries whose official metadata does not mark them as non-latest, reducing duplicate versions in UI results.
- Result IDs are generated as `name:version`, not as database GUIDs.
- `GetByIdAsync` paginates up to five batches of 100 items because the registry does not expose a direct lookup-by-id endpoint.
- Transport types are assembled from both remote endpoints and installable package metadata; npm/PyPI packages also imply `stdio`.
- HTTP failures and timeouts are logged and converted into empty responses instead of throwing source-level exceptions.
