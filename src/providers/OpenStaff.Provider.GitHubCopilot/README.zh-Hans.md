# OpenStaff.Provider.GitHubCopilot

`OpenStaff.Provider.GitHubCopilot` 把 GitHub Copilot 作为一个动态的、多厂商 Provider 来源接入系统。它不会假设只有单一厂商目录，而是先用 GitHub OAuth token 换取短期 Copilot API token，再下载 Copilot 模型目录，并把目录中暴露的端点映射回 OpenStaff 内部协议标记。

## 职责
- 通过 `OpenStaffProviderGitHubCopilotModule` 注册 `github-copilot` 协议键。
- 注册协议依赖的 `CopilotTokenService` 与 `IHttpClientFactory`，用于访问 GitHub 与 Copilot 端点。
- 使用设备流获得的 OAuth token 换取 Copilot API token。
- 拉取并缓存 Copilot `/models` 目录，然后将 `/chat/completions`、`/responses`、`/v1/messages` 等端点映射为内部协议标记。

## 配置与环境模型
`GitHubCopilotProtocolEnv` 继承自 `ProtocolEnvBase`，因此公开配置面较小：
- `BaseUrl`（默认值 `https://api.individual.githubcopilot.com`）
- `OAuthToken`（加密字段）

由于 `OAuthToken` 带有 `[Encrypted]` 标记，抽象层会把它视为敏感字段，并沿用统一的加密环境配置持久化流程。

此外，该项目还依赖 `OpenStaffOptions.WrokingDirectory`，因为协议会把下载得到的模型目录缓存到 `{WrokingDirectory}\providers\github_copilot_models.json`。

## 认证预期
GitHub Copilot 的模型发现依赖通过 GitHub 设备流获得的 OAuth token。`CopilotTokenService` 会把该 token 发送到 `https://api.github.com/copilot_internal/v2/token`，换取短期 Copilot API token，并在距离过期还有五分钟时之前复用内存缓存。

模型目录请求会把 Copilot token 作为 Bearer token 发送，并附带模拟 VS Code Copilot Chat 客户端的专用请求头。

## 关键类型
- `OpenStaffProviderGitHubCopilotModule`
- `GitHubCopilotProtocol`
- `GitHubCopilotProtocolEnv`
- `CopilotTokenService`
- `CopilotToken`
- `Provider\Protocols\Models.cs` 中的模型 DTO

## 与 Provider 抽象层的关系
- 依赖 `ProviderAbstractionsModule`。
- 通过 `ProviderOptions` 注册 `GitHubCopilotProtocol`。
- 继承 `ProtocolBase<GitHubCopilotProtocolEnv>`，而不是 `VendorProtocolBase<TProtocolEnv>`，因为 Copilot 账户下可能同时暴露多个厂商与多种协议族。
- 这是 `ChatClientFactory` 会通过 `CreateProtocolWithEnv(...)` 动态解析的 Provider 之一，以便根据当前账户实际暴露的协议标记做精确判断。

## 值得注意的细节
- `IsVendor` 为 `false`，因为单个 Copilot 账户后面可能同时出现 OpenAI、Anthropic 等多个厂商模型。
- 模型元数据会在磁盘上缓存一天；如果刷新失败，会回退使用过期缓存。
- 如果 token 响应里带有 `chat_completions` 端点，则其主机会覆盖默认的模型目录主机。
- `BaseUrl` 虽然存在于环境对象中，但 `ModelsAsync()` 当前并不会在发现流程里使用它。
- `CopilotTokenService` 会先对 OAuth token 做哈希，再用作内存缓存键，避免把明文凭据长期作为字典键保存。
- `ProtocolName` 当前与代码保持一致，为 `GitHub Copilot`。
