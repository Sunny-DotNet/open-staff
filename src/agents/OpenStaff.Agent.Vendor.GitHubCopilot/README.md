# OpenStaff.Agent.Vendor.GitHubCopilot

## Purpose

This project implements the `github-copilot` vendor provider for OpenStaff. Unlike the other vendor projects, it does not create agents from a stored API key. Instead, it spins up a `CopilotClient` that reuses the locally signed-in GitHub Copilot user and exposes that session as an `AIAgent`.

## How it plugs into OpenStaff

- `GitHubCopilotAgentProvider` implements both `IAgentProvider` and `IVendorAgentProvider`.
- `OpenStaffApplicationModule` registers it as a singleton, and `AgentFactory` selects it when `AgentRole.ProviderType` is `github-copilot`.
- `AgentRoleAppService` still uses `GetConfigSchema()` and `GetModelsAsync()` so the UI can expose a Copilot vendor role. Model metadata is queried with vendor key `github`, not `github-copilot`.
- `ApplicationAgentRunFactory` contains a vendor-role fast path that allows an empty `ResolvedProvider` when a vendor role manages authentication internally. That behavior is what makes this provider usable even when no `ModelProviderId` is supplied.

## Auth and runtime assumptions

- The host machine must already have a GitHub Copilot login that the SDK can reuse, because `CopilotClientOptions.UseLoggedInUser = true`.
- `CreateAgentAsync()` does not consume `ResolvedProvider.ApiKey`, `ResolvedProvider.BaseUrl`, or other provider-account settings.
- The runtime must allow outbound access for the Copilot SDK and must have access to the local user profile or credential store that backs the Copilot login.

## SDK and protocol behavior

- Key packages: `GitHub.Copilot.SDK`, `Microsoft.Agents.AI.GitHub.Copilot`, `Microsoft.Agents.AI.OpenAI`.
- The provider creates a `CopilotClient`, calls `StartAsync()`, and then converts it to an agent with `AsAIAgent(...)`.
- `SessionConfig` enables streaming and auto-approves all permission requests.
- Interactive user-input callbacks are auto-answered with the freeform text `继续` so backend execution does not block waiting for SDK prompts.
- `ownsClient: true` hands Copilot client disposal to the returned agent so background connections are cleaned up with the agent lifecycle.

## Important classes

- `GitHubCopilotAgentProvider` - the provider implementation and runtime entry point.
- `SessionConfig` - defines streaming and callback behavior for the Copilot session.
- `GetGhCliToken()` - an obsolete troubleshooting helper kept only for future diagnostics.

## Caveats

- The current implementation does not apply `role.Config`, the selected model, `role.Name`, or `role.SystemPrompt` to the created agent.
- `GetModelsAsync()` can expose several model families, but agent creation currently depends on whatever model/session behavior the Copilot SDK negotiates.
- Auto-approving permissions and auto-answering prompts is convenient for unattended execution, but it is a poor fit for locked-down or approval-sensitive deployments.
