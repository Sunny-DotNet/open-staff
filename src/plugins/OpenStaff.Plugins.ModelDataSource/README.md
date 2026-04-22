# OpenStaff.Plugins.ModelDataSource

## Purpose

`OpenStaff.Plugins.ModelDataSource` provides the shared model catalog for OpenStaff. It defines the contract used by providers and application services to discover model vendors and model capabilities, and it ships the default `models.dev` implementation.

## Plugin role

This project is a plugin-style module in the provider ecosystem, not part of the marketplace family. Its job is to make normalized model metadata available to:

- provider protocol implementations
- application services that expose model data
- API startup logic that initializes the catalog
- vendor-specific agent providers that can optionally consume catalog data

## Key abstractions and data sources

- `IModelDataSource`
  - lifecycle: `InitializeAsync`, `RefreshAsync`
  - status: `SourceId`, `DisplayName`, `IsReady`, `LastUpdatedUtc`
  - queries:
    - `GetVendorsAsync`
    - `GetModelsAsync`
    - `GetModelsByVendorAsync`
    - `GetModelAsync`

- model records and enums
  - `ModelVendor`
  - `ModelData`
  - `ModelLimits`
  - `ModelPricing`
  - `ModelModality`
  - `ModelCapability`

- `ModelsDevModelDataSource`
  - downloads `https://models.dev/api.json`
  - parses vendor-grouped JSON
  - normalizes modalities, capabilities, limits, pricing, and release dates
  - caches the raw payload locally at `%USERPROFILE%\.staff\models-dev.json`

- `ModelDataSourceModule`
  - depends on `OpenStaffCoreModule`
  - registers `ModelsDevModelDataSource` as a singleton
  - resolves `IModelDataSource` to that singleton instance

## Dependencies

Direct project dependency:

- `OpenStaff.Core`

At runtime the default implementation also uses:

- `HttpClient` for remote fetches
- local file system access for cache persistence
- `System.Text.Json` for parsing

## Integration points

- `OpenStaff.Application\OpenStaffApplicationModule` includes `ModelDataSourceModule`
- `OpenStaff.Provider.Abstractions\ProviderAbstractionsModule` depends on this module so provider protocols can always resolve `IModelDataSource`
- `OpenStaff.HttpApi.Host\Program.cs` initializes the shared source on startup with `InitializeAsync()`
- `OpenStaff.Application\ModelData\ModelDataAppService` exposes source status, refresh, vendor, and model queries
- `OpenStaff.HttpApi\Controllers\ModelDataController` exposes those operations through `api/models-dev`
- provider protocol base classes use the data source to list vendor models and filter for text/function-call capable models

## Operational notes

- The default source ID is `models.dev`.
- If a cache file already exists, initialization loads the cache first and then starts a background refresh.
- If no cache exists, initialization performs a foreground refresh so the source becomes usable.
- Refresh failures fall back to the local cache when possible.
- Corrupt local cache files are ignored so a later remote refresh can recover the catalog.
- Parsed data is stored in concurrent dictionaries and replaced atomically after a successful parse.
- `EnsureReady()` can lazily load the cache on first query if startup initialization did not complete yet.
- The singleton creates its own `HttpClient` unless composition or tests inject one explicitly.
- The interface is broader than the current default implementation, so the project can later swap in other model catalog sources without changing consumers.
