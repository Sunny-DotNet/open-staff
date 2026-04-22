# OpenStaff.Provider.Google

`OpenStaff.Provider.Google` registers Google Generative Language support in the provider system. The project contributes one protocol, `GoogleProtocol`, and relies on the shared model catalog instead of calling Google directly during discovery.

## Responsibility
- Registers the `google` provider key through `OpenStaffProviderGoogleModule`.
- Declares support for the `GoogleGenerateContent` protocol family.
- Resolves Google-backed model entries through `VendorProtocolBase<GoogleProtocolEnv>` and the shared `IModelDataSource`.

## Configuration and environment model
`GoogleProtocolEnv` inherits `ProtocolApiKeyEnvironmentBase`, so the env shape includes:
- `BaseUrl` (default `https://generativelanguage.googleapis.com/v1beta2`)
- `ApiKeyFromEnv` (`false` by default)
- `ApiKeyEnvName` (default `GOOGLE_API_KEY`)
- `ApiKey` (secret)

The abstractions layer supplies metadata generation, secret handling, and JSON hydration for this config object.

## Authentication expectations
Runtime calls are expected to use a Google API key. The key can be stored directly in `ApiKey` or resolved from the environment when `ApiKeyFromEnv` is enabled.

This project does not perform live model discovery against the Google API; it only publishes the protocol metadata and config defaults needed by the rest of OpenStaff.

## Key classes
- `OpenStaffProviderGoogleModule`
- `GoogleProtocol`
- `GoogleProtocolEnv`

## Relation to provider abstractions
- Depends on `ProviderAbstractionsModule`.
- Registers `GoogleProtocol` in `ProviderOptions`.
- Inherits `VendorProtocolBase<TProtocolEnv>`, so it gets vendor-catalog discovery behavior and `IsVendor = true` from the abstractions package.

## Notable quirks
- `ProtocolName` is the lowercase string `google`, which aligns with the shared vendor slug rather than the usual product branding.
- The default base URL points to the `v1beta2` Generative Language endpoint.
- The advertised protocol flag is `GoogleGenerateContent`, so consumers must treat this provider as Google-native rather than OpenAI-compatible.
