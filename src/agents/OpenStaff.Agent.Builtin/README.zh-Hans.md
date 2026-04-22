# OpenStaff.Agent.Builtin

这是 OpenStaff 默认智能体体验使用的内置 Provider 实现。

## 运行时职责

- 实现 `builtin` 这一 `IAgentProvider`。
- 把数据库中的角色记录转换成 `ChatClientAgent`。
- 为 OpenAI 兼容、Google、GitHub Copilot 等协议族创建具体 `IChatClient`。

## 在分层架构中的位置

- 位于 `OpenStaff.Agent.Abstractions` 和 `OpenStaff.Provider.Abstractions` 之上。
- 它既不是消息执行内核，也不是应用投影适配层。
- 当角色的 `ProviderType = "builtin"`，或旧数据未填写 ProviderType 时，`AgentFactory` 与 `ApplicationAgentRunFactory` 都会走到这里。
- `OpenStaffAgentBuiltinModule` 会把 Provider 和聊天客户端工厂注册为共享单例。

## 关键契约与类型

- `BuiltinAgentProvider`：内置角色和数据库自定义 OpenAI 兼容角色的主 `IAgentProvider` 实现。
- `ChatClientFactory`：根据解析后的 Provider 账号和模型，选择具体聊天客户端与 API 形态。

## 依赖方向

- 向内依赖智能体抽象层和 Provider 协议抽象层。
- 上层应优先通过 `IAgentProvider` 使用它；只有确实需要 builtin provider 专属行为时才依赖具体类型。

## 扩展点

- 如需支持新的 Provider 协议或新的 API 选择规则，可扩展 `ChatClientFactory`。
- 可以直接用数据库中的 `AgentRole` 记录定义内置或自定义角色，而不再依赖仓库内资源。

## Provider、工具与生命周期要点

- `BuiltinAgentProvider` 会根据持久化字段和可选 JSON 配置临时拼装一份轻量 `RoleConfig`，再通过 `IAgentPromptGenerator` 生成最终 system prompt。
- 运行时额外传入的 MCP 工具会按名称去重合并。
- `ChatClientFactory` 会按 Provider 账号版本缓存模型协议探测结果，并根据探测能力或模型启发式在 OpenAI Chat Completions 与 Responses API 之间切换。
- GitHub Copilot 路径会做基地址规范化并补充请求头；Google 路径则单独规范 beta endpoint 形态。
