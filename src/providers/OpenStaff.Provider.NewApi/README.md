# OpenStaff.Provider.NewApi

`OpenStaff.Provider.NewApi` integrates NewApi or OneAPI-style gateways into the provider system. Unlike the vendor-specific packages, this project models a multi-vendor gateway and discovers models dynamically from gateway metadata rather than from a fixed vendor catalog.

## Responsibility
- Registers the `newapi` provider key through `OpenStaffProviderNewApiModule`.
- Calls the configured gateway's `/api/pricing` endpoint to discover available models.
- Maps gateway endpoint names to OpenStaff protocol flags such as Chat Completions, Responses, Anthropic Messages, and Google Generate Content.
- Normalizes model and vendor names against the shared `IModelDataSource` when possible.

## Configuration and environment model
`NewApiProtocolEnv` inherits `ProtocolApiKeyEnvironmentBase`, which exposes:
- `BaseUrl` (default empty string; discovery does nothing until it is configured)
- `ApiKeyFromEnv` (`false` by default)
- `ApiKeyEnvName` (default `NEW_API_AUTH_TOKEN`)
- `ApiKey` (secret)

The env object is still processed by the abstractions layer, so its secret handling and metadata schema are consistent with the other provider packages.

## Authentication expectations
The environment model includes API key settings because actual gateway calls may require authentication elsewhere in the system. However, `ModelsAsync()` currently performs an unauthenticated `GET` to `{BaseUrl}/api/pricing`.

That means:
- model discovery works out of the box for gateways that expose pricing publicly;
- discovery will fail and return an empty list if the pricing endpoint requires auth;
- the configured `ApiKey` is currently metadata for downstream runtime usage, not something this discovery path sends on the wire.

## Key classes
- `OpenStaffProviderNewApiModule`
- `NewApiProtocol`
- `NewApiProtocolEnv`

## Relation to provider abstractions
- Depends on `ProviderAbstractionsModule`.
- Registers `NewApiProtocol` in `ProviderOptions`.
- Inherits `ProtocolBase<NewApiProtocolEnv>` instead of `VendorProtocolBase<TProtocolEnv>` because it must parse gateway JSON instead of reading a single-vendor catalog.
- Reuses the shared `IModelDataSource` only for normalization and canonical vendor lookup.

## Notable quirks
- Unknown or missing endpoint metadata falls back to `OpenAIChatCompletions`.
- Vendor normalization uses both exact and containment-based matching against the shared catalog, plus a hard-coded replacement from `zhipuai-coding-plan` to `zai`.
- When the gateway model cannot be matched to the canonical catalog, the protocol keeps the raw model name and falls back to a best-effort vendor slug instead of dropping the entry.
- `Logo` is currently an empty string.
