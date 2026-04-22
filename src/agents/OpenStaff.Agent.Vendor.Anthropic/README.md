# OpenStaff.Agent.Vendor.Anthropic

## Purpose

This project implements the `anthropic` vendor provider for OpenStaff. It creates Claude-family `AIAgent` instances through the Anthropic SDK integration instead of the builtin provider pipeline. The project exists to give Anthropic roles a dedicated provider type, vendor-specific model discovery, and direct SDK-backed runtime behavior.

## How it plugs into OpenStaff

- `AnthropicAgentProvider` implements both `IAgentProvider` and `IVendorAgentProvider`.
- `OpenStaffApplicationModule` registers it as a singleton; `AgentFactory` routes roles here when `AgentRole.ProviderType` is `anthropic`.
- `AgentRoleAppService` calls `GetConfigSchema()` and `GetModelsAsync()` so the UI can render Anthropic-specific role configuration and model choices.
- `ProviderResolver` is expected to populate `ResolvedProvider.ApiKey` from the selected provider account before `CreateAgentAsync()` is invoked.

## Auth and runtime assumptions

- A resolved Anthropic API key is required.
- The active model comes from `AgentRole.Config` (`model`), not from `role.ModelName`; the default is `claude-sonnet-4-20250514`.
- The current implementation assumes direct access to Anthropic-managed endpoints. No custom base URL is read from `ResolvedProvider`.

## SDK and protocol behavior

- Key package: `Microsoft.Agents.AI.Anthropic` (which exposes the `AnthropicClient` integration used here).
- `CreateAgentAsync()` constructs `AnthropicClient { ApiKey = apiKey }`.
- Agent creation uses `client.AsAIAgent(model: ..., name: ..., instructions: ...)` directly, so Anthropic-specific SDK behavior is owned by that adapter rather than by `ChatClientAgent`.
- Model discovery prefers `IModelDataSource` metadata and falls back to the local `FallbackModels` list when the data source is not ready.

## Important classes

- `AnthropicAgentProvider` - the only runtime class in this project and the vendor entry point for Claude agents.
- `VendorModel` / `FallbackModels` - local fallback model catalog for Claude families.
- `AgentConfigSchema` / `AgentConfigField` - the provider-owned dynamic schema for frontend configuration.

## Caveats

- `ResolvedProvider.BaseUrl` is ignored, so this project does not currently support Anthropic-compatible proxies or alternate hosted endpoints.
- `role.ModelName` is ignored in favor of `role.Config`.
- The constructor stores `ILoggerFactory`, but the created Anthropic agent is not passed a logger factory today.
