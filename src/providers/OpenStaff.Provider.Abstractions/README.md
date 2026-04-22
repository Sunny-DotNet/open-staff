# OpenStaff.Provider.Abstractions

`OpenStaff.Provider.Abstractions` is the shared contract layer for provider discovery and provider-account configuration in OpenStaff. It does not implement any vendor protocol by itself. Instead, it defines the protocol model, environment schema conventions, metadata shape, and factory services that concrete provider packages plug into.

## Responsibility
- Registers `IProtocolFactory` through `ProviderAbstractionsModule`.
- Stores discoverable protocol types in `ProviderOptions`.
- Defines `IProtocol`, `ProtocolBase<TProtocolEnv>`, and `VendorProtocolBase<TProtocolEnv>`.
- Builds protocol metadata for the API and UI through `ProtocolMetadata` and `ProtocolEnvField`.
- Serializes and deserializes provider environment JSON with selective encryption through `ProtocolEnvSerializer`.
- Defines normalized model discovery output through `ModelInfo` and `ModelProtocolType`.
- Bridges provider modules to the shared `OpenStaff.Plugins.ModelDataSource` catalog.

## Configuration and environment model
All provider environment types inherit one of the base env classes:
- `ProtocolEnvBase`: must expose `BaseUrl`.
- `ProtocolApiKeyEnvironmentBase`: adds `ApiKeyFromEnv`, `ApiKeyEnvName`, and encrypted `ApiKey`.

`ProtocolFactory.GetProtocolMetadata()` reflects public properties from each env type and emits field descriptors with UI-friendly types:
- `string`
- `secret` for properties marked with `[Encrypted]`
- `bool`
- `number`

`ProtocolEnvSerializer` only encrypts `[Encrypted]` string properties when an encryption delegate is supplied. On read, it only attempts decryption when a value looks like ciphertext, so plaintext legacy configs keep working.

## Authentication expectations
This package does not authenticate against any remote service. It only defines how providers describe credentials and endpoints. Concrete providers decide whether they use API keys, OAuth tokens, or no secret at all.

## Key classes
- `ProviderAbstractionsModule`
- `IProtocol`
- `ProtocolBase<TProtocolEnv>`
- `VendorProtocolBase<TProtocolEnv>`
- `IProtocolFactory` / `ProtocolFactory`
- `ProtocolEnvSerializer`
- `ProviderOptions`
- `ModelInfo`, `ModelProtocolType`
- `ProtocolMetadata`, `ProtocolEnvField`

## Relation to concrete providers
Concrete provider modules depend on `ProviderAbstractionsModule` and register themselves through `Configure<ProviderOptions>(options => options.AddProtocol<TProtocol>())`.

Downstream consumers use this layer directly:
- `ProtocolsController` returns `IProtocolFactory.GetProtocolMetadata()` for the API.
- provider-account services serialize and decrypt env JSON with `ProtocolEnvSerializer`.
- `ChatClientFactory` creates configured protocols with `CreateProtocolWithEnv(...)` so runtime code can inspect model and protocol compatibility.

## Notable quirks
- `VendorProtocolBase<TProtocolEnv>` does not call provider APIs. It reads from the shared `IModelDataSource` catalog and only returns models that support text input, text output, and function calling.
- `ProtocolFactory.CreateProtocolWithEnv(...)` uses a lenient Boolean converter, so JSON values such as `"true"` and `1` can hydrate `bool` fields.
- `ModelInfo` uses `VendorSlug` for upstream model ownership metadata.
