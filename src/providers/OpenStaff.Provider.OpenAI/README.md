# OpenStaff.Provider.OpenAI

`OpenStaff.Provider.OpenAI` adds the OpenAI protocol definition to the provider system. It is a focused registration package: the module registers `OpenAIProtocol`, and the protocol describes how OpenStaff should treat OpenAI-backed accounts and models. Model discovery in this project is catalog-based, not a live OpenAI API call.

## Responsibility
- Registers the `openai` provider key through `OpenStaffProviderOpenAIModule`.
- Declares OpenAI support for both `OpenAIChatCompletions` and `OpenAIResponse` runtimes.
- Maps OpenAI models from the shared `IModelDataSource` through `VendorProtocolBase<OpenAIProtocolEnv>`.

## Configuration and environment model
`OpenAIProtocolEnv` inherits `ProtocolApiKeyEnvironmentBase`, so the exposed environment schema contains:
- `BaseUrl` (default `https://api.openai.com/v1`)
- `ApiKeyFromEnv` (`false` by default)
- `ApiKeyEnvName` (default `OPENAI_API_KEY`)
- `ApiKey` (treated as a secret field by the abstractions layer)

The abstractions package handles JSON schema generation, encryption, and deserialization for this env type.

## Authentication expectations
The runtime is expected to use an OpenAI API key. OpenStaff can either store the key in `ApiKey` or resolve it from the process environment when `ApiKeyFromEnv` is enabled.

This project itself does not exchange tokens or query the OpenAI REST API during model discovery.

## Key classes
- `OpenStaffProviderOpenAIModule`
- `OpenAIProtocol`
- `OpenAIProtocolEnv`

## Relation to provider abstractions
- Depends on `ProviderAbstractionsModule`.
- Registers `OpenAIProtocol` in `ProviderOptions`.
- Reuses `VendorProtocolBase<TProtocolEnv>`, which means `IsVendor` is `true` and model enumeration comes from the shared vendor catalog.

## Notable quirks
- The protocol advertises both Chat Completions and Responses support, so one OpenAI account can back either runtime style.
- Only models that the shared catalog marks as text-input, text-output, and function-call capable are surfaced.
- `ProtocolKey` is `openai`, `ProtocolName` is `OpenAI`, and `Logo` is `OpenAI`.
