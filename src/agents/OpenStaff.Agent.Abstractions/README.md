# OpenStaff.Agent.Abstractions

Shared agent foundation for provider implementations and higher-level runtime adapters.

## Runtime responsibility

- Defines the provider-facing contracts that turn an `AgentRole`, `AgentContext`, and `ResolvedProvider` into an `AIAgent`.
- Owns shared prompt composition helpers.
- Exposes provider configuration metadata used by the UI.

## Place in the layered architecture

- Sits above `OpenStaff.Core` and `OpenStaff.Application.Contracts`.
- Sits below concrete providers such as `OpenStaff.Agent.Builtin` and the vendor provider projects.
- It is not the message execution kernel; `OpenStaff.Agent.Services` owns per-message runtime execution.
- `OpenStaffAgentAbstractionsModule` registers the shared singleton services used across the family.

## Key contracts and types

- `IAgentProvider`: common provider contract consumed by `AgentFactory`.
- `IVendorAgentProvider`: optional extension for providers that can enumerate models.
- `AgentFactory`: routes `AgentRole.ProviderType` to a registered provider and falls back to `builtin` for legacy roles.
- `IAgentPromptGenerator` / `AgentPromptGenerator`: builds prompts in `global -> project -> role -> scene` order while safely reading scoped settings from a singleton.
- `AgentConfigSchema`, `AgentConfigField`, `AgentConfigOption`, `AgentConfig`: provider configuration metadata plus parsed runtime values.
- `AgentComponents`: reusable bundle of the constructed agent, final instructions, and attached tools.

## Dependency direction

- References only `OpenStaff.Core` and `OpenStaff.Application.Contracts`.
- Concrete providers and higher-level runtime adapters depend inward on this project.

## Extension points

- Add a new provider by implementing `IAgentProvider` and registering it in DI.
- Add model discovery support by also implementing `IVendorAgentProvider`.
- Return richer `AgentConfigSchema` data to extend provider-specific configuration UIs.

## Lifecycle and integration notes

- `AgentFactory` snapshots all visible `IAgentProvider` registrations when the singleton is created, so providers must be registered before the module finishes bootstrapping.
- `AgentPromptGenerator` creates a scope per build so global settings stay current without breaking singleton lifetime rules.
