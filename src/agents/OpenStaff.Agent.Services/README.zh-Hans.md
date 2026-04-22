# OpenStaff.Agent.Services

这是智能体家族的进程内运行时内核，负责执行一条逻辑消息。

## 运行时职责

- 接收 `CreateMessageRequest` 并启动异步执行。
- 维护可回放的内存事件流，以及最终的执行摘要。
- 把 Provider 的流式更新规范化为正文、思考、工具、用量、重试、完成、错误、取消等事件。

## 在分层架构中的位置

- 本项目就是这一族智能体运行时的内核层。
- 它只依赖 `OpenStaff.Agent.Abstractions` 和 `System.Reactive`。
- 它不知道 EF Core、通知通道、MCP 或任何应用持久化细节。
- `OpenStaff.Agent.Services.Adapters` 为这个内核提供面向应用的具体适配实现。

## 关键契约与类型

- `IAgentService`：创建、取消、跟踪消息运行的公共入口。
- `IAgentRunFactory` 与 `PreparedAgentRun`：准备具体 `AIAgent`、恢复后的消息历史、可选会话对象以及本次运行的 `AgentRunOptions`。
- `IAgentMessageObserver`：对外事件扇出接口，供投影与流式推送实现使用。
- `IProjectAgentRuntimeCache`：项目级智能体能力变化时的缓存失效钩子。
- `CreateMessageRequest`、`CreateMessageResponse`、`MessageContext`、`MessageScene`：请求标识、路由与场景元数据。
- `AgentMessageEvent`、`AgentMessageEventType`、`MessageExecutionSummary`：对调用方和适配层稳定暴露的运行时契约。
- `AgentService`、`MessageHandler`、`AgentExecutionState`、`ToolInvocationState`、`AgentServiceOptions`：执行流水线、回放缓冲、流聚合器、工具状态追踪与重试配置。

## 依赖方向

- 更高层通过 `IAgentService` 向内调用本项目。
- 内核对外只依赖 `IAgentRunFactory` 和 `IAgentMessageObserver` 这两个抽象。
- 具体的持久化、通知、工具授权系统都放在本项目之外。

## 内核与适配层的边界

- 本项目负责的内核能力：请求校验、后台执行、重试、事件回放、完成态处理、取消控制。
- 其他项目负责的适配能力：解析角色/Provider/历史消息、注入每次运行的工具、持久化最终消息、发布会话事件、投影监控数据。

## 扩展点

- 通过替换 `IAgentRunFactory`，把内核接到不同宿主环境。
- 通过增加一个或多个 `IAgentMessageObserver`，把运行时事件扇出到更多下游。
- 通过调整 `AgentServiceOptions.MaxRetryCount`，启用自动重试。

## 生命周期与集成要点

- `CreateMessageAsync` 会先发出 `Accepted`，再启动后台任务，确保调用方能够立即订阅。
- `MessageHandler` 使用 `ReplaySubject<AgentMessageEvent>` 和完成任务，因此晚到的订阅者也能看到之前的流式事件。
- `ExecuteAsync` 只准备一次上下文，然后按 `MaxRetryCount` 进行流式执行重试。
- 终态事件会先写入本地回放流，再忽略下游观察者失败，因此不会因为持久化或推送异常阻塞 `Completion`。
- 每条消息都有独立的链式 `CancellationTokenSource`，运行结束或处理器被移除时会被释放。
