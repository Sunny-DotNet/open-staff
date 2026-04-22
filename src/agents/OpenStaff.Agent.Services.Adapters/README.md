# OpenStaff.Agent.Services.Adapters

Application-facing adapters that connect the agent runtime kernel to database, notification, and MCP-backed behavior.

## Runtime responsibility

- Supplies the default `IAgentRunFactory` used by the application runtime.
- Resolves persisted roles, project agents, providers, message history, scene-specific defaults, and per-run MCP tools.
- Projects kernel events into session streaming, final chat messages, monitoring tables, and task metadata.

## Place in the layered architecture

- This project is the adapter half of the kernel/adapters split.
- It sits above `OpenStaff.Agent.Services` and `OpenStaff.Agent.Abstractions`.
- It reaches outward to `OpenStaff.Infrastructure`, `OpenStaff.Application.Contracts`, `OpenStaff.Core`, and the builtin provider implementation.
- It has no module class of its own today; `OpenStaffApplicationModule` registers these concrete types.

## Key contracts and types

- `ApplicationAgentRunFactory`: application-backed `IAgentRunFactory` that resolves the effective role, provider context, history, and `AgentRunOptions`.
- `AgentRoleExecutionProfileFactory`: clones persisted `AgentRole` entities and applies live overrides without mutating stored data.
- `IAgentMcpToolService` and `AgentMcpCapabilityGrantResult`: boundary for loading enabled MCP tools and granting missing tool capabilities.
- `SessionStreamingAgentMessageObserver`: maps kernel events to `SessionEvent` notifications for live UI streaming.
- `ChatMessageProjectionObserver`: materializes terminal assistant messages in `ChatMessages` and publishes the final session message event.
- `RuntimeMonitoringProjectionObserver`: persists `AgentEvent` trees and updates task runtime metadata.
- `RuntimeProjectionMetadataMapper`: normalizes scene values and parses persisted runtime metadata payloads.

## Dependency direction

- Depends inward on the runtime kernel (`OpenStaff.Agent.Services`) and common provider abstractions.
- Depends outward on `AppDbContext`, `IProviderResolver`, `INotificationService`, and MCP capability services supplied by the application/infrastructure layers.
- The kernel knows only the interfaces; this project owns the application-specific implementations.

## Kernel vs adapters

- Kernel (`OpenStaff.Agent.Services`) defines event contracts, retries, replay, cancellation, and message lifecycle.
- Adapters (this project) decide where roles come from, how providers/prompts/history are resolved, how tools are injected, and where runtime events are persisted or streamed.

## Extension points

- Replace `ApplicationAgentRunFactory` when hosting the kernel in a different application shell.
- Add more `IAgentMessageObserver` implementations for extra projections or telemetry.
- Implement `IAgentMcpToolService` against a different tool entitlement system.

## Lifecycle and tool/provider integration notes

- `ApplicationAgentRunFactory` resolves the executor in this order: `ProjectAgentId`, explicit `AgentRoleId`, then `TargetRole` with project-assigned roles preferred over the global catalog.
- Role overrides are applied to a cloned `AgentRole`; non-builtin roles rebuild their system prompt when identity fields change.
- Project brainstorm defaults are intentionally scoped to the builtin `secretary` role in `ProjectBrainstorm`, so unrelated scenes do not inherit project defaults.
- Non-builtin providers receive a freshly built prompt through `IAgentPromptGenerator`; builtin roles use the builtin provider's database-backed role profile flow.
- Enabled MCP tools are injected through `RunOptions`, allowing capability changes per run without rebuilding the stored role.
- History restoration follows only the ancestor chain from `ParentMessageId`, avoiding full-session prompt inflation.
- `ChatMessageProjectionObserver` honors `skip_final_projection=true` in `MessageContext.Extra` for runs that need streaming or monitoring only.
- `RuntimeMonitoringProjectionObserver` keeps parent-child `AgentEvent` trees stable by attaching tool results and tool errors to the originating tool-call event.
