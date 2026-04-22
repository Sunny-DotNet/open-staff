# OpenStaff.Agent.Vendor.GitHubCopilot

## 作用

本项目实现 OpenStaff 的 `github-copilot` 厂商智能体提供器。与其他厂商项目不同，它不是依赖已保存的 API Key 来创建智能体，而是启动一个 `CopilotClient`，复用本机已登录的 GitHub Copilot 用户身份，并把该会话暴露为 `AIAgent`。

## 与 OpenStaff 的集成方式

- `GitHubCopilotAgentProvider` 同时实现 `IAgentProvider` 和 `IVendorAgentProvider`。
- `OpenStaffApplicationModule` 将它注册为单例；当 `AgentRole.ProviderType` 为 `github-copilot` 时，`AgentFactory` 会路由到这里。
- `AgentRoleAppService` 仍然会调用 `GetConfigSchema()` 和 `GetModelsAsync()` 来生成 Copilot 厂商角色的界面配置；这里查询模型元数据时使用的 vendor key 是 `github`，不是 `github-copilot`。
- `ApplicationAgentRunFactory` 对 Vendor 角色有一条特殊分支：如果角色自行处理认证，就允许返回空的 `ResolvedProvider`。正是这条逻辑让本 Provider 即使没有 `ModelProviderId` 也可以运行。

## 认证与运行时前提

- 运行主机必须已经存在可被 SDK 复用的 GitHub Copilot 登录状态，因为代码设置了 `CopilotClientOptions.UseLoggedInUser = true`。
- `CreateAgentAsync()` 不会使用 `ResolvedProvider.ApiKey`、`ResolvedProvider.BaseUrl` 或其他 Provider 账号配置。
- 运行时需要允许 Copilot SDK 发起外部网络连接，并且能够访问当前用户配置文件或凭据存储中的登录信息。

## SDK 与协议行为

- 关键依赖：`GitHub.Copilot.SDK`、`Microsoft.Agents.AI.GitHub.Copilot`、`Microsoft.Agents.AI.OpenAI`。
- Provider 会创建 `CopilotClient`，调用 `StartAsync()`，再通过 `AsAIAgent(...)` 转成智能体。
- `SessionConfig` 开启流式输出，并且会自动批准所有权限请求。
- 所有用户输入回调都会被自动回答为自由文本 `继续`，以避免后端执行因为 SDK 交互式提问而阻塞。
- `ownsClient: true` 让返回的智能体接管 `CopilotClient` 的释放，从而随智能体生命周期一起清理后台连接。

## 重要类型

- `GitHubCopilotAgentProvider`：Provider 实现，也是运行时入口。
- `SessionConfig`：定义 Copilot 会话的流式与回调行为。
- `GetGhCliToken()`：已废弃的排障辅助方法，仅保留作后续诊断参考。

## 注意事项

- 当前实现不会把 `role.Config`、所选模型、`role.Name` 或 `role.SystemPrompt` 应用到最终创建的智能体上。
- `GetModelsAsync()` 可以列出多个模型家族，但实际创建出来的智能体仍取决于 Copilot SDK 自身协商得到的会话与模型行为。
- 自动批准权限、自动回答提问适合无人值守执行，但不适合权限严格或需要人工审批的部署环境。
