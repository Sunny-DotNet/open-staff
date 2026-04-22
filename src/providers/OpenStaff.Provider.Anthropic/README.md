# OpenStaff.Provider.Anthropic

`OpenStaff.Provider.Anthropic` registers Anthropic support in the provider system. Like the OpenAI package, it is a vendor registration project: the module adds `AnthropicProtocol`, and the protocol publishes Anthropic-specific metadata and env settings while reusing the shared model catalog for discovery.

## Responsibility
- Registers the `anthropic` provider key through `OpenStaffProviderAnthropicModule`.
- Declares Anthropic support for the `AnthropicMessages` protocol.
- Resolves available Anthropic models through `VendorProtocolBase<AnthropicProtocolEnv>` and the shared `IModelDataSource`.

## Configuration and environment model
`AnthropicProtocolEnv` inherits `ProtocolApiKeyEnvironmentBase`, which exposes:
- `BaseUrl` (default `https://api.anthropic.com`)
- `ApiKeyFromEnv` (`false` by default)
- `ApiKeyEnvName` (default `ANTHROPIC_AUTH_TOKEN`)
- `ApiKey` (stored as a secret field)

The abstractions package serializes this env object, marks the secret field for UI consumption, and supports both plaintext and encrypted persisted values.

## Authentication expectations
An Anthropic API credential is expected at runtime. The account can carry the token in `ApiKey` or instruct OpenStaff to load it from the environment through `ApiKeyEnvName`.

This package does not call the Anthropic API during model discovery; it only describes how Anthropic should be represented in OpenStaff.

## Key classes
- `OpenStaffProviderAnthropicModule`
- `AnthropicProtocol`
- `AnthropicProtocolEnv`

## Relation to provider abstractions
- Depends on `ProviderAbstractionsModule`.
- Registers `AnthropicProtocol` in `ProviderOptions`.
- Inherits `VendorProtocolBase<TProtocolEnv>`, so it gets shared vendor-catalog lookup behavior and `IsVendor = true` automatically.

## Notable quirks
- The protocol only advertises `AnthropicMessages`; it does not expose an OpenAI-compatible fallback flag.
- Model discovery is still catalog-based, so the default `BaseUrl` is configuration metadata rather than something used for live discovery calls here.
- `Logo` is `Claude.Color`, while `ProtocolName` remains `Anthropic`.
