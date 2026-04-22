# OpenStaff.Agent.Vendor.OpenAI

## Purpose

This project implements the `openai` vendor provider for OpenStaff. It creates `AIAgent` instances backed by OpenAI GPT-family chat models through the official `OpenAI` SDK and the `Microsoft.Extensions.AI` bridge. Use this provider when a role should call OpenAI directly instead of going through the generic builtin multi-protocol stack.

## How it plugs into OpenStaff

- `OpenAIAgentProvider` implements both `IAgentProvider` and `IVendorAgentProvider`.
- `OpenStaffApplicationModule` registers it as a singleton; `AgentFactory` selects it whenever `AgentRole.ProviderType` is `openai`.
- `AgentRoleAppService` uses `GetConfigSchema()` and `GetModelsAsync()` to build the vendor-role form and model picker shown in the application.
- `ProviderResolver` supplies `ResolvedProvider.ApiKey` and `ResolvedProvider.BaseUrl` from the selected provider account before `CreateAgentAsync()` runs.

## Auth and runtime assumptions

- A resolved provider account must contain an API key.
- The effective model is read from `AgentRole.Config` (`model`), not from `role.ModelName`; the default is `gpt-4o`.
- The runtime must be able to reach the configured OpenAI endpoint or an OpenAI-compatible gateway.

## SDK and protocol behavior

- Key packages: `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `OpenAI`.
- `CreateAgentAsync()` builds an `OpenAIClient` with `ApiKeyCredential`.
- If `ResolvedProvider.BaseUrl` is present, it is assigned directly to `OpenAIClientOptions.Endpoint`. This is intentional: the provider supports reverse proxies and OpenAI-compatible gateways.
- The SDK chat client is converted with `GetChatClient(model).AsIChatClient()` and wrapped in a `ChatClientAgent`.
- Model discovery prefers `IModelDataSource` (models.dev-backed metadata) and falls back to the local `FallbackModels` list when the plugin is unavailable.

## Important classes

- `OpenAIAgentProvider` - provider implementation and agent factory entry point.
- `VendorModel` / `FallbackModels` - static model metadata used when dynamic discovery is unavailable.
- `AgentConfigSchema` / `AgentConfigField` - the dynamic config contract exposed to the frontend.

## Caveats

- `AgentContext` is currently not used during agent creation.
- `role.ModelName` is not consulted; keep `role.Config` in sync with the model you expect to run.
- Because `BaseUrl` is passed through verbatim, misconfigured endpoints fail at runtime rather than being normalized here.
