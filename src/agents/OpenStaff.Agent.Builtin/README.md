# OpenStaff.Agent.Builtin

Builtin provider implementation for OpenStaff's default agent experience.

## Runtime responsibility

- Implements the `builtin` `IAgentProvider`.
- Turns database-defined role records into `ChatClientAgent` instances.
- Creates protocol-specific `IChatClient` implementations for OpenAI-compatible, Google, and GitHub Copilot style endpoints.

## Place in the layered architecture

- Sits on top of `OpenStaff.Agent.Abstractions` and `OpenStaff.Provider.Abstractions`.
- It is neither the runtime execution kernel nor the application projection adapter layer.
- `AgentFactory` and `ApplicationAgentRunFactory` route here when a role uses `ProviderType = "builtin"` or omits the provider type.
- `OpenStaffAgentBuiltinModule` registers the provider and chat-client factory as shared singletons.

## Key contracts and types

- `BuiltinAgentProvider`: main `IAgentProvider` for builtin roles and custom OpenAI-compatible roles stored in the database.
- `ChatClientFactory`: selects the concrete chat client and API surface for the resolved provider account and model.

## Dependency direction

- Depends inward on agent abstractions and provider protocol abstractions.
- Is consumed through `IAgentProvider`; higher layers should not depend on its concrete type unless they need builtin-provider-specific behavior.

## Extension points

- Extend `ChatClientFactory` when a new provider protocol or API-selection rule is needed.
- Use persisted `AgentRole` records to define builtin or custom roles without shipping repository resources.

## Provider, tool, and lifecycle notes

- `BuiltinAgentProvider` reconstructs a lightweight `RoleConfig` from persisted role fields and optional JSON config, then builds final instructions through `IAgentPromptGenerator`.
- Runtime-supplied tools such as MCP tools are merged by name without duplication.
- `ChatClientFactory` caches model-protocol discovery per provider account version and chooses between OpenAI Chat Completions and Responses APIs from discovered capability or model heuristics.
- GitHub Copilot requests receive normalized base URLs plus extra headers; Google requests normalize the beta endpoint shape separately.
