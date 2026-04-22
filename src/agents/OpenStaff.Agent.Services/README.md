# OpenStaff.Agent.Services

In-process execution kernel for a single logical agent message.

## Runtime responsibility

- Accepts `CreateMessageRequest` values and starts asynchronous execution.
- Maintains replayable in-memory event streams plus a terminal execution summary.
- Normalizes provider streaming updates into content, reasoning, tool, usage, retry, completion, error, and cancellation events.

## Place in the layered architecture

- This project is the runtime kernel for the agent family.
- It depends only on `OpenStaff.Agent.Abstractions` and `System.Reactive`.
- It does not know about EF Core, notifications, MCP, or application persistence.
- `OpenStaff.Agent.Services.Adapters` supplies the concrete application-backed implementations around this kernel.

## Key contracts and types

- `IAgentService`: public entry point for creating, cancelling, and tracking message runs.
- `IAgentRunFactory` and `PreparedAgentRun`: prepare the concrete `AIAgent`, restored history, optional session, and per-run `AgentRunOptions`.
- `IAgentMessageObserver`: outbound event sink for projections and streaming.
- `IProjectAgentRuntimeCache`: invalidation hook used by higher layers when project-level agent capability changes.
- `CreateMessageRequest`, `CreateMessageResponse`, `MessageContext`, `MessageScene`: request identity, routing, and scene metadata.
- `AgentMessageEvent`, `AgentMessageEventType`, `MessageExecutionSummary`: stable runtime contracts exposed to callers and adapters.
- `AgentService`, `MessageHandler`, `AgentExecutionState`, `ToolInvocationState`, `AgentServiceOptions`: the execution pipeline, replay buffer, stream aggregator, tool state tracker, and retry settings.

## Dependency direction

- Higher layers call inward through `IAgentService`.
- The kernel depends outward only on `IAgentRunFactory` and `IAgentMessageObserver`.
- Concrete persistence, notification, and tool-capability systems remain outside this project.

## Kernel vs adapters

- Kernel here: request validation, background execution, retries, event replay, completion handling, and cancellation.
- Adapters elsewhere: resolve roles/providers/history, inject per-run tools, persist final messages, publish session events, and project monitoring data.

## Extension points

- Replace `IAgentRunFactory` to run the kernel against a different host environment.
- Add one or more `IAgentMessageObserver` implementations to fan out runtime events.
- Tune `AgentServiceOptions.MaxRetryCount` to enable automatic retries.

## Lifecycle and integration notes

- `CreateMessageAsync` emits `Accepted` before the background task starts so callers can subscribe immediately.
- `MessageHandler` uses a `ReplaySubject<AgentMessageEvent>` plus a completion task, so late subscribers still receive prior streamed events.
- `ExecuteAsync` prepares once, then retries streaming attempts up to `MaxRetryCount`.
- Terminal events are written to the local replay stream before downstream observer failures are ignored, so `Completion` cannot be blocked by persistence or push sinks.
- Each message owns a linked `CancellationTokenSource` that is disposed when the run finishes or the handler is removed.
