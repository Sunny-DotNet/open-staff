# OpenStaff.Agent.Services.Adapters

这是把智能体运行时内核接到数据库、通知通道和 MCP 能力体系上的应用适配层。

## 运行时职责

- 提供应用默认使用的 `IAgentRunFactory` 实现。
- 解析持久化角色、项目成员、Provider、消息历史、场景默认值，以及每次运行需要启用的 MCP 工具。
- 把内核事件投影成会话流、最终聊天消息、监控事件和任务运行时元数据。

## 在分层架构中的位置

- 本项目是“内核 / 适配层”拆分中的适配层一半。
- 它位于 `OpenStaff.Agent.Services` 和 `OpenStaff.Agent.Abstractions` 之上。
- 它向外连接 `OpenStaff.Infrastructure`、`OpenStaff.Application.Contracts`、`OpenStaff.Core`，以及内置 Provider 实现。
- 当前这个程序集本身没有独立模块类，相关具体类型由 `OpenStaffApplicationModule` 统一注册。

## 关键契约与类型

- `ApplicationAgentRunFactory`：面向应用环境的 `IAgentRunFactory`，负责解析有效角色、Provider 上下文、历史消息和 `AgentRunOptions`。
- `AgentRoleExecutionProfileFactory`：克隆持久化 `AgentRole`，并安全叠加临时覆盖，不污染数据库实体。
- `IAgentMcpToolService` 与 `AgentMcpCapabilityGrantResult`：负责加载已启用 MCP 工具，以及补齐缺失工具能力的边界接口。
- `SessionStreamingAgentMessageObserver`：把内核事件映射成前端实时消费的 `SessionEvent`。
- `ChatMessageProjectionObserver`：把终态 assistant 回复落到 `ChatMessages`，并发布最终会话消息事件。
- `RuntimeMonitoringProjectionObserver`：持久化 `AgentEvent` 树，并同步任务运行时元数据。
- `RuntimeProjectionMetadataMapper`：统一规范场景值，解析已持久化的运行时元数据载荷。

## 依赖方向

- 向内依赖运行时内核 `OpenStaff.Agent.Services` 和共享 Provider 抽象。
- 向外依赖应用/基础设施层提供的 `AppDbContext`、`IProviderResolver`、`INotificationService`、MCP 能力服务。
- 运行时内核只认识接口，本项目承载这些接口的应用特定实现。

## 内核与适配层的边界

- 内核 `OpenStaff.Agent.Services` 负责事件契约、重试、回放、取消和消息生命周期。
- 本项目作为适配层，决定角色从哪里来、Provider/提示词/历史如何解析、工具如何注入，以及运行时事件落到哪里。

## 扩展点

- 如果宿主环境变化，可以替换 `ApplicationAgentRunFactory`。
- 可以继续增加新的 `IAgentMessageObserver`，扩展更多投影或遥测。
- 可以基于别的工具授权体系实现 `IAgentMcpToolService`。

## 生命周期与工具 / Provider 集成要点

- `ApplicationAgentRunFactory` 按 `ProjectAgentId` -> 显式 `AgentRoleId` -> `TargetRole` 的顺序解析执行者，并优先命中项目内已分配角色。
- 角色覆盖总是应用在克隆后的 `AgentRole` 上；非内置角色在身份字段变化时会重建 system prompt。
- 项目头脑风暴默认值只会作用于 `ProjectBrainstorm` 场景下的内置 `secretary`，避免污染其他场景。
- 非 builtin Provider 会通过 `IAgentPromptGenerator` 现建提示词；builtin 角色走内置 Provider 的数据库角色画像流程。
- 已启用的 MCP 工具通过 `RunOptions` 注入，因此每次运行的能力集可以独立变化，而不必重建持久化角色。
- 历史消息只恢复从 `ParentMessageId` 向上的祖先链，避免把整段会话全部塞进模型上下文。
- `ChatMessageProjectionObserver` 会识别 `MessageContext.Extra` 中的 `skip_final_projection=true`，用于只需要流式或监控、不需要生成最终聊天消息的场景。
- `RuntimeMonitoringProjectionObserver` 会把工具结果和工具错误挂到对应的工具调用事件下，保持持久化 `AgentEvent` 树稳定。
