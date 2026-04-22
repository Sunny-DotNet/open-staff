# OpenStaff 智能体创建 / 执行链路

这份文档不是泛泛的架构介绍，而是给**调试**用的总链路地图。它回答三个问题：

1. **角色是怎么被创建和保存的**
2. **测试对话时，运行时 Agent 是怎么临时拉起来的**
3. **正式项目会话里，Session / Frame / AgentService / AgentFactory 是怎么串起来的**

---

## 先分清两种“创建智能体”

这套系统里，“创建智能体”至少有两种完全不同的语义：

| 语义 | 本质 | 是否持久化 |
| --- | --- | --- |
| **创建角色记录** | 创建 / 更新 `AgentRole` 这条数据库记录 | **是** |
| **创建运行时 Agent 实例** | 为某次消息执行临时准备 `IStaffAgent` | **否**，通常只活在一次运行里 |

如果这两个概念不分开，后面所有调试都会混乱。

---

## 核心对象速查

| 对象 | 作用 | 典型出处 |
| --- | --- | --- |
| `AgentRole` | 角色定义，保存名字、模型、provider、config、soul 等 | `OpenStaff.Domain\Entities\AgentRole.cs` |
| `ProjectAgent` | 某个项目里实际分配出去的成员实例 | 项目执行场景 |
| `CreateMessageRequest` | 一次逻辑消息执行请求 | `IAgentService.CreateMessageAsync` 的输入 |
| `MessageContext` | 把这次消息重新挂回 session / frame / task / project-agent | 运行时上下文主键包 |
| `AgentContext` | 最终传给 Agent Provider 的执行上下文 | `ApplicationAgentRunFactory.BuildAgentContext` |
| `PreparedAgentRun` | 已准备好的运行时 Agent + 历史消息 + RunOptions | `IAgentRunFactory.PrepareAsync` 的输出 |
| `MessageHandler` | 单条消息的内存事件流 + Completion | `AgentService` 内部 |
| `SessionStreamManager` | 面向前端的 session 事件流 | 测试对话和正式会话都会用 |

---

## 总览图

```mermaid
flowchart TD
    A[角色保存入口<br/>AgentRolesController POST/PUT] --> B[AgentRoleApiService Create/Update]
    B --> C[Repository + SaveChanges]
    C --> D[AgentRole 持久化完成]

    E[测试对话入口<br/>POST /api/agent-roles/{id}/test-chat] --> F[AgentRoleApiService.TestChatAsync]
    F --> G[加载 AgentRole + 应用 live override]
    G --> H[创建临时 SessionStream]
    H --> I[IAgentService.CreateMessageAsync]

    J[正式会话入口<br/>SessionRunner.StartSessionAsync / SendMessageAsync] --> K[创建 ChatSession / ChatFrame]
    K --> L[ExecuteUserInputAsync / ExecuteFrameAsync]
    L --> M[ExecuteRuntimeFrameAsync]
    M --> I

    I --> N[AgentService.ExecuteAsync]
    N --> O[IAgentRunFactory.PrepareAsync]
    O --> P[ApplicationAgentRunFactory]
    P --> Q[ResolveExecutionAsync]
    P --> R[ResolveProviderAsync]
    P --> S[BuildAgentContext + Load MCP/Skills]
    S --> T[AgentFactory.CreateAgentAsync]

    T --> U{providerType / 平台能力}
    U -->|平台注册了 TaskAgentFactory| V[Platform TaskAgentFactory]
    U -->|普通 provider| W[IAgentProvider.CreateAgentAsync]
    W --> X[BuiltinAgentProvider / 其他 Provider]
    X --> Y[构建 Prompt + ChatClient + Tools]

    V --> Z[PreparedAgentRun]
    Y --> Z
    Z --> AA[Agent.CreateTaskAsync]
    AA --> AB[Reasoning / Content / Tool / Usage 流]
    AB --> AC[MessageHandler + Observers]

    AC --> AD[测试链: CompleteTestChatStreamAsync]
    AD --> AE[SessionStreamManager 推回前端]

    AC --> AF[正式链: handler.Completion]
    AF --> AG[ExecuteFrameAsync 处理路由 / 子 Frame / 完成态]
    AG --> AH[SessionStreamManager + ChatMessage 投影]
```

---

## 一、角色创建 / 保存链

这条链**不会创建运行时 Agent**。它只负责把角色定义存进数据库。

### 入口

- `src\application\OpenStaff.HttpApi\Controllers\AgentRolesController.cs`
  - `POST /api/agent-roles`
  - `PUT /api/agent-roles/{id}`

### 调用步骤

1. `AgentRolesController.Create/Update`
2. 调用 `IAgentRoleApiService`
3. 落到 `AgentRoleApiService.CreateAsync/UpdateAsync`
4. 通过 `MapToEntity(...)` 把输入 DTO 映射到 `AgentRole`
5. 通过 repository + `SaveChangesAsync` 持久化
6. 再构造 `AgentRoleDto` 返回前端

### 持久化边界

- **进入数据库的只有角色定义**
- 当前活跃字段集中在：
  - `Name`
  - `Description`
  - `JobTitle`
  - `Avatar`
  - `ModelProviderId`
  - `ModelName`
  - `Config`
  - `Soul`
  - `ProviderType`
  - `Source`

### 调试要点

如果你看到“角色保存成功，但对话里没生效”，先判断问题属于哪一层：

- **保存链问题**：数据库字段没更新
- **运行时链问题**：保存了，但测试或正式运行拿到的不是你预期的角色快照

---

## 二、测试对话链

这条链从 `agent-roles` 页面发起，目标是：**基于一个角色定义，临时拉起一次可调试的运行时 Agent**。

### 入口

- `src\application\OpenStaff.HttpApi\Controllers\AgentRolesController.cs`
  - `POST /api/agent-roles/{id}/test-chat`

### 入口到运行时的完整步骤

1. `AgentRolesController.TestChat(...)`
2. 调用 `AgentRoleApiService.TestChatAsync(...)`
3. `AgentRoleApiService`：
   - 用 `AsNoTracking()` 读取源 `AgentRole`
   - 调用 `CreateEffectiveTestRole(...)`
   - 本质是 `AgentRoleExecutionProfileFactory.CreateEffectiveRole(...)`
4. 这一步会把测试界面里的临时 override 叠到**克隆后的角色副本**上
   - 不污染数据库实体
   - 不污染共享缓存
5. `AgentRoleApiService` 创建一个临时 `sessionId`
6. `SessionStreamManager.Create(sessionId)` 创建测试流
7. 构造 `CreateMessageRequest`
   - `Scene = MessageScene.Test`
   - `AgentRoleId = 当前角色 id`
   - `OverrideJson = 测试面板 override`
   - `ModelContext = 当前这次测试显式选中的模型/provider`
8. 调用 `IAgentService.CreateMessageAsync(...)`
9. `AgentService.CreateMessageAsync(...)`
   - 创建 `MessageHandler`
   - 先发 `Accepted`
   - 后台 `Task.Run(ExecuteAsync(...))`
10. `AgentRoleApiService` 通过 `TryGetMessageHandler(...)` 拿到 handler
11. 再启动 `CompleteTestChatStreamAsync(...)`
12. 该方法等待 `handler.Completion`
13. 把最终成功 / 失败摘要推回 `SessionStreamManager`
14. 前端通过测试会话流拿到最终结果

### 运行时准备阶段

`AgentService.ExecuteAsync(...)` 是测试链和正式链的共同汇合点。

它的主要动作是：

1. 如果 `ModelContext.ProviderAccountId` 有值，用 `ICurrentProviderDetail.Use(...)` 把 provider account 放进当前异步上下文
2. 调用 `_runFactory.PrepareAsync(...)`
3. 当前默认实现是 `ApplicationAgentRunFactory.PrepareAsync(...)`

### `ApplicationAgentRunFactory` 里做了什么

#### 1. 解析“这次到底谁执行”

`ResolveExecutionAsync(...)` 的优先级是：

1. `ProjectAgentId`
2. `AgentRoleId`
3. `TargetRole`

测试对话通常直接走 `AgentRoleId` 这一支。

#### 2. 生成有效角色快照

- 先反序列化 `OverrideJson`
- 再调用 `AgentRoleExecutionProfileFactory.CreateEffectiveRole(...)`
- 得到这次运行真正用的 `effectiveRole`

#### 3. 解析 Provider

- `ResolveProviderAsync(...)`
- 优先使用：
  - `role.ModelProviderId`
  - 否则 `project.DefaultProviderId`
- 如果是 vendor 角色且认证由 SDK 自管，允许返回空的 `ResolvedProvider`

#### 4. 构建 AgentContext

- `BuildAgentContext(...)`
- 这里会把：
  - `ProjectId`
  - `SessionId`
  - `Project`
  - `Language`
  - `Role`
  - `Scene`
  - `AgentInstanceId`
  - `Account`
  - `ApiKey`
  统一塞到 `AgentContext`

#### 5. 加载本次运行能力

- `IAgentSkillRuntimeService.LoadRuntimePayloadAsync(...)`
- `IAgentMcpToolService.LoadEnabledToolsAsync(...)`
- 测试场景下 role 级 skill / MCP 绑定会在这里汇入运行时

#### 6. 创建运行时 Agent

调用：

- `AgentFactory.CreateAgentAsync(effectiveRole, agentContext, provider)`

这里才真正进入“运行时 Agent 实例化”。

### `AgentFactory` 如何分流

`AgentFactory.CreateAgentAsync(...)` 的核心逻辑：

1. 先确定 `providerType`
   - 缺失时默认 `"builtin"`
2. 先看 `IPlatformRegistry` 里是否存在同名平台
3. 如果该平台实现了 `IHasTaskAgentFactory`
   - 走平台型 `TaskAgentFactory`
4. 否则看 `_providers` 字典里是否有同名 `IAgentProvider`
5. 再调用对应 `CreateAgentAsync(...)`

也就是说，它优先走：

1. **平台 TaskAgentFactory**
2. **普通 IAgentProvider**

### `BuiltinAgentProvider` 如何组装 Agent

对于 builtin / 默认 provider，关键逻辑在：

- `BuiltinAgentProvider.PrepareAgentAsync(...)`

主要步骤：

1. `BuildRoleConfigFromDb(role)`
   - 从数据库角色字段重建轻量 `RoleConfig`
   - 解析 `Config` 里的 `modelParameters`、`tools`
2. `IAgentPromptGenerator.PromptBuildAsync(...)`
   - 当前活跃 system prompt 主要由这里现建
3. `ChatClientFactory.CreateAsync(provider, modelName, ...)`
4. 从 `IAgentToolRegistry` 取工具，并桥接为 `AITool`
5. 合并本次运行追加的 MCP tools
6. 构造 `ChatClientAgentOptions`
7. `chatClient.AsAIAgent(...)`
8. 返回 `PreparedAgentRun`

> 当前有效链路里，**系统提示词的主来源是 `IAgentPromptGenerator` 组装结果**，而不是单独从某个嵌入 prompt 文件直接透传到运行时。

### 执行与结果回流

准备完成后，`AgentService.ExecuteAsync(...)` 会：

1. `preparedRun.Agent.CreateTaskAsync(new CreateTaskRequest(request.Input), ...)`
2. 订阅这些流式事件：
   - reasoning
   - content
   - tool call request
   - tool call response
   - usage
3. 聚合成 `AgentMessageEvent`
4. 写入 `MessageHandler`
5. 再扇出给 `IAgentMessageObserver`

终态时：

- 成功：`Completed`
- 失败：`Error`
- 取消：`Cancelled`

最后 `handler.Complete(summary)`。

然后测试链自己的 `CompleteTestChatStreamAsync(...)` 会：

1. 等待 `handler.Completion`
2. 把结果包装成 `streaming_done` / `error`
3. 推进 `SessionStreamManager`
4. 移除消息处理器

### 这条链的边界

| 边界 | 位置 |
| --- | --- |
| **持久化边界** | `AgentRole` 是持久化的，但测试执行本身不是 |
| **内存态边界** | `MessageHandler`、`PreparedAgentRun`、`SessionStream` |
| **异步边界** | `AgentService.CreateMessageAsync -> Task.Run(ExecuteAsync)` |
| **实时边界** | `SessionStreamManager`、`IAgentMessageObserver` |

---

## 三、正式项目会话链

这条链不是直接从 `agent-roles` 页面进，而是走 `SessionRunner`，带有 **session / frame / routing / group dispatch** 这些更重的编排语义。

### 入口

主要入口：

- `SessionRunner.StartSessionAsync(...)`
- `SessionRunner.SendMessageAsync(...)`

这两条最后都会进入：

- `ExecuteUserInputAsync(...)`

### 正式链的完整步骤

1. `StartSessionAsync(...)`
2. 持久化 `ChatSession`
3. `SessionStreamManager.Create(session.Id)`
4. 后台 `Task.Run(() => ExecuteUserInputAsync(...))`
5. `ExecuteUserInputAsync(...)`
   - 先调用 `_orchestration.InitializeProjectAgentsAsync(...)`
   - 这里更多是**项目级预热和缓存**，不是单条消息执行核心
6. 根据场景决定 root frame
   - 默认会创建一个以 `secretary` 为目标角色的 root frame
7. 调用 `CreateFrameAsync(...)`
8. 调用 `ExecuteFrameAsync(...)`

### `ExecuteFrameAsync(...)` 做了什么

它是正式链最关键的外层编排器。

主要步骤：

1. 建立 frame 级取消令牌
2. 推送：
   - `FramePushed`
   - `Thought`
3. 调用 `ExecuteRuntimeFrameAsync(...)`
4. 等待运行时返回 `OrchestrationResponse`
5. 根据场景做额外解析
   - ProjectBrainstorm：处理 requirements / R.MD 状态
   - ProjectGroup：处理 dispatch plan / capability request
6. 发布本轮 agent message
7. 判断是否：
   - 要暂停等待用户输入
   - 要继续路由到 child frame
   - 还是直接完成当前 frame

### `ExecuteRuntimeFrameAsync(...)` 如何接入运行时内核

`ExecuteRuntimeFrameAsync(...)` 本质是把一个 frame 再翻译成 `CreateMessageRequest`：

1. 读取 frame 的 entry message
2. 解析 projectAgentId
3. 构造 `CreateMessageRequest`
   - `Scene = 根据 session scene 转成 MessageScene`
   - `MessageContext` 里带上：
     - `ProjectId`
     - `SessionId`
     - `ParentMessageId`
     - `FrameId`
     - `ParentFrameId`
     - `TaskId`
     - `ProjectAgentId`
     - `InitiatorRole`
     - `Extra["skip_final_projection"] = "true"`
4. 调用 `_agentService.CreateMessageAsync(...)`
5. 取回 `MessageHandler`
6. 直接等待 `handler.Completion`
7. 把 `MessageExecutionSummary` 映射为 `OrchestrationResponse`
8. `finally` 里移除 handler

这里很关键：

> 正式会话链不会直接自己创建 Agent。  
> 它仍然复用 **同一条运行时核心链**：`IAgentService -> ApplicationAgentRunFactory -> AgentFactory`。

### 正式链如何把结果接回 Session / Frame

`ExecuteFrameAsync(...)` 在拿到 `OrchestrationResponse` 后：

1. 写 assistant 消息
2. 如有 `RequiresUserInput`
   - 暂停当前 session
3. 如有 `TargetRole` 且不是当前角色
   - 创建 child frame
   - 递归 `ExecuteFrameAsync(...)`
4. 否则：
   - `CompleteFrameAsync(...)`
   - 推送 `FrameCompleted`

也就是说，正式链相较测试链多出来的是：

- `ChatSession`
- `ChatFrame`
- 路由 / 子 frame
- group dispatch / capability plan
- 会话状态机

但底层真正执行模型调用的核心仍然没变。

---

## 共同汇合点：运行时内核

无论是测试对话还是正式会话，最终都会汇合到这条主干：

```text
IAgentService.CreateMessageAsync
  -> AgentService.ExecuteAsync
    -> IAgentRunFactory.PrepareAsync
      -> ApplicationAgentRunFactory.PrepareAsync
        -> AgentFactory.CreateAgentAsync
          -> BuiltinAgentProvider / 平台型 TaskAgentFactory / 其他 IAgentProvider
    -> preparedRun.Agent.CreateTaskAsync
    -> MessageHandler / Observers / Completion
```

---

## 当前有效的 Prompt / 能力注入位置

### Prompt

当前运行时里，system prompt 的实际组装主链是：

1. `ApplicationAgentRunFactory` 解析出 `effectiveRole`
2. `BuiltinAgentProvider.PrepareAgentAsync(...)`
3. `IAgentPromptGenerator.PromptBuildAsync(role, context, ct)`
4. `AgentPromptGenerator`
   - 全局设置
   - 项目说明
   - 角色说明
   - 场景说明

### MCP / Skills

能力注入不是在 controller 层完成，而是在运行时准备阶段完成：

- `IAgentSkillRuntimeService.LoadRuntimePayloadAsync(...)`
- `IAgentMcpToolService.LoadEnabledToolsAsync(...)`

因此这两类问题都应该往 `ApplicationAgentRunFactory` 里断，而不是只看 `AgentRoleApiService`。

---

## 调试断点地图

### 1. 角色保存了，但数据库没变

先断：

- `AgentRolesController.Create/Update`
- `AgentRoleApiService.CreateAsync/UpdateAsync`

重点看：

- 输入 DTO 是否正确
- `MapToEntity(...)` 后字段是否已经偏了
- `SaveChangesAsync` 前实体状态是否已正确更新

### 2. 测试对话时拿到的不是最新角色配置

先断：

- `AgentRoleApiService.TestChatAsync`
- `AgentRoleExecutionProfileFactory.CreateEffectiveRole`

重点看：

- `sourceRole` 是不是刚从 DB 读出来的
- `liveOverride` 是否覆盖了你以为来自数据库的字段
- `effectiveRole.ModelProviderId / ModelName / Soul` 最终值是否正确

### 3. 运行时提示词不对

先断：

- `BuiltinAgentProvider.PrepareAgentAsync`
- `IAgentPromptGenerator.PromptBuildAsync`
- `AgentPromptGenerator.PromptBuildAsync`

重点看：

- `effectiveRole`
- `AgentContext.Scene`
- `AgentContext.Project`
- 最终 `config.SystemPrompt`

### 4. provider / api key 解析失败

先断：

- `ApplicationAgentRunFactory.ResolveProviderAsync`
- `ProviderResolver.ResolveAsync`

重点看：

- `role.ModelProviderId`
- `project.DefaultProviderId`
- `ResolvedProvider.ApiKey`
- 是否属于 vendor SDK 自管认证场景

### 5. 明明调用了 runtime，但前端一直没结果

先断：

- `IAgentService.CreateMessageAsync`
- `AgentService.ExecuteAsync`
- `TryGetMessageHandler(...)`
- `handler.Completion`

重点看：

- handler 是否创建成功
- `ExecuteAsync` 有没有在 prepare 阶段就异常
- 有没有终态事件进入 `handler.Complete(...)`

### 6. 正式会话里执行了，但 frame 没结束 / 没路由

先断：

- `SessionRunner.ExecuteFrameAsync`
- `SessionRunner.ExecuteRuntimeFrameAsync`

重点看：

- `OrchestrationResponse.Success`
- `OrchestrationResponse.TargetRole`
- `OrchestrationResponse.RequiresUserInput`
- `responseContent`

### 7. 测试对话流有 Accepted，但没有最终结果

先断：

- `AgentRoleApiService.CompleteTestChatStreamAsync`
- `handler.Completion`

重点看：

- `MessageExecutionSummary.Success/Error`
- handler 是否被提前移除
- `SessionStreamManager` 是否拿到了最终 push

---

## 常见误判

### 误判 1：以为保存角色就等于创建了运行时 Agent

不是。

- 保存角色只改数据库
- 运行时 Agent 只会在某次消息执行前由 `ApplicationAgentRunFactory + AgentFactory` 临时准备

### 误判 2：以为测试链和正式链是两套完全不同的执行系统

也不是。

它们的**外层编排不同**，但**底层执行核心相同**：

- 都会进 `IAgentService`
- 都会进 `ApplicationAgentRunFactory`
- 都会进 `AgentFactory`

### 误判 3：以为问题一定在 controller

通常不是。

长链路里最常出问题的反而是：

- `effectiveRole` 叠加结果
- `ResolveProviderAsync`
- prompt 组装
- handler 生命周期
- frame / session 回流

---

## 建议的实际排查顺序

如果你现在要追一个“智能体没按预期创建 / 执行”的问题，建议固定按下面顺序断：

1. **入口层**
   - 到底是角色保存、测试对话，还是正式会话
2. **角色快照层**
   - `sourceRole` / `effectiveRole`
3. **Provider 层**
   - `ResolveProviderAsync`
4. **Prompt / 能力层**
   - `PromptBuildAsync`
   - MCP / Skills runtime payload
5. **AgentFactory 层**
   - 到底走了 builtin、platform task factory，还是别的 provider
6. **Handler / Completion 层**
   - 看终态有没有真正完成
7. **回流层**
   - 测试链看 `CompleteTestChatStreamAsync`
   - 正式链看 `ExecuteFrameAsync`

---

## 补充：另一个常被忽略的入口

除了本文主讲的三条主线，还有一个较短的入口：

- `ProjectAgentService.SendMessageAsync(...)`

它用于给项目成员发私聊消息。它同样会：

1. 加载 `ProjectAgent`
2. 构造 `CreateMessageRequest`
3. 调用 `_agentService.CreateMessageAsync(...)`
4. 等待 `handler.Completion`

所以它也是同一运行时核心的一个“薄入口”。

---

## 一句话总结

**OpenStaff 不是“保存角色后就拥有一个常驻智能体实例”。**  
它是“先保存 `AgentRole`，等有消息进来时，再由 `ApplicationAgentRunFactory` 和 `AgentFactory` 按场景临时组装出一次运行时 Agent，并把结果通过 handler / frame / session 流回推上层”。
