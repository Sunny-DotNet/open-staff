# OpenStaff.Agent.Vendor.Google

## Purpose

This project implements the `google` vendor provider for OpenStaff. It creates Gemini-backed `AIAgent` instances by combining the `Google.GenAI` SDK with the `Microsoft.Extensions.AI` chat-client bridge. The provider is intended for direct Gemini Developer API usage rather than the generic builtin provider path.

## How it plugs into OpenStaff

- `GoogleAgentProvider` implements both `IAgentProvider` and `IVendorAgentProvider`.
- `OpenStaffApplicationModule` registers it as a singleton, and `AgentFactory` selects it when `AgentRole.ProviderType` is `google`.
- `AgentRoleAppService` uses `GetConfigSchema()` and `GetModelsAsync()` to populate Google-specific role settings and model lists in the UI.
- `ProviderResolver` provides the decrypted API key through `ResolvedProvider.ApiKey` before the provider is asked to create an agent.

## Auth and runtime assumptions

- A Google API key must be available in the resolved provider account.
- The active model comes from `AgentRole.Config` (`model`), not from `role.ModelName`; the default is `gemini-2.5-flash`.
- The runtime is hard-wired for the Gemini Developer API path because `Client(vertexAI: false, apiKey: apiKey)` is used.

## SDK and protocol behavior

- Key packages: `Google.GenAI` and `Microsoft.Agents.AI`.
- `CreateAgentAsync()` constructs `Google.GenAI.Client` with `vertexAI: false`, then converts it to an `IChatClient` via `AsIChatClient(model)`.
- The resulting chat client is wrapped in `ChatClientAgent`, so OpenStaff gets a normal `AIAgent` abstraction while still using the Gemini SDK underneath.
- Model discovery prefers `IModelDataSource` metadata and falls back to the local `FallbackModels` list if the plugin is unavailable.

## Important classes

- `GoogleAgentProvider` - provider implementation and Gemini agent factory entry point.
- `VendorModel` / `FallbackModels` - fallback Gemini model metadata.
- `AgentConfigSchema` / `AgentConfigField` - frontend-facing provider configuration contract.

## Caveats

- `ResolvedProvider.BaseUrl` is ignored.
- This provider does not support Vertex AI project/location/service-account flows; it assumes a plain Gemini API key.
- `role.ModelName` is ignored in favor of the `model` value inside `role.Config`.
