# OpenStaff.Agent.Abstractions

这是智能体家族的共享抽象层，为具体 Provider 实现和上层运行时适配层提供统一基础。

## 运行时职责

- 定义把 `AgentRole`、`AgentContext`、`ResolvedProvider` 组装成 `AIAgent` 所需的 Provider 契约。
- 提供统一的提示词拼装能力。
- 暴露给前端动态表单使用的 Provider 配置元数据。

## 在分层架构中的位置

- 位于 `OpenStaff.Core`、`OpenStaff.Application.Contracts` 之上。
- 位于 `OpenStaff.Agent.Builtin` 和各 Vendor Provider 项目之下。
- 它不是消息执行内核；真正负责单条消息运行的是 `OpenStaff.Agent.Services`。
- `OpenStaffAgentAbstractionsModule` 会把这一层的共享单例服务注册到 DI 容器。

## 关键契约与类型

- `IAgentProvider`：统一的 Provider 入口，供 `AgentFactory` 调用。
- `IVendorAgentProvider`：可选扩展，用于支持模型列表发现。
- `AgentFactory`：按 `AgentRole.ProviderType` 路由到已注册 Provider，并为旧角色保留 `builtin` 回退。
- `IAgentPromptGenerator` / `AgentPromptGenerator`：按“全局 -> 项目 -> 角色 -> 场景”顺序生成完整提示词，并在单例生命周期下安全读取作用域设置。
- `AgentConfigSchema`、`AgentConfigField`、`AgentConfigOption`、`AgentConfig`：描述 Provider 配置表单及其运行时键值。
- `AgentComponents`：把构造好的 Agent、最终指令和挂载的工具打包成可复用结果。

## 依赖方向

- 只引用 `OpenStaff.Core` 和 `OpenStaff.Application.Contracts`。
- 具体 Provider 与更高层的运行时适配实现都向内依赖本项目。

## 扩展点

- 通过实现 `IAgentProvider` 并注册到 DI 来增加新的 Provider。
- 通过同时实现 `IVendorAgentProvider` 增加模型枚举能力。
- 通过返回更丰富的 `AgentConfigSchema` 扩展 Provider 配置界面。

## 生命周期与集成要点

- `AgentFactory` 在单例创建时会枚举当前容器里所有 `IAgentProvider`，因此 Provider 必须在模块初始化完成前注册好。
- `AgentPromptGenerator` 每次构建都会新建 scope，以便读取最新全局设置，同时保持单例安全。
