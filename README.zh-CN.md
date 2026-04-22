# OpenStaff

**语言版本：** [English](README.md) | 简体中文

OpenStaff 是一个面向软件研发协作的多智能体平台，重点不是“单助手聊天”，而是把**项目、角色、能力、工作区和执行过程**组织成一个可持续推进的系统。

## OpenStaff 是什么

OpenStaff 围绕几条核心原则构建：

- **项目优先**：需求、成员、文件、任务和会话都归属于项目
- **角色化协作**：智能体先定义为可复用角色，再分配到项目里协同工作
- **能力可控**：模型、MCP、Skills 和权限申请都是显式配置、可治理的
- **运行时可观测**：会话、帧、消息和项目执行过程可以被持续追踪
- **平台可扩展**：Provider 账户、MCP、Skills、插件和远程角色源都是模块化能力

## 当前已经实现的能力

当前仓库已经不是一个只验证底层运行时的原型，而是围绕可用产品面的实现。

### 1. 项目管理

- 创建、编辑、删除、导入和导出项目
- 初始化项目工作区
- 启动项目进入执行阶段
- 保存项目级默认模型和 Provider 等配置
- 首页直接展示项目列表，作为主要入口

### 2. 项目脑暴

- 提供项目级脑暴会话
- 使用 `.staff/project-brainstorm.md` 持续整理需求
- 由秘书角色引导用户把模糊想法逐步整理成可启动项目的需求

### 3. 项目群执行

- 提供长期存在的项目群聊执行会话
- 用户显式 `@agent` 时优先路由到目标成员
- 用户未指定成员时由秘书先接收并判断是否分发
- 支持结构化任务分发和能力申请流程

### 4. 角色管理与角色工作台

- 在 **Agent Roles** 页面管理本地角色
- 编辑角色名称、头像、职位、描述、模型设置、Soul、MCP 绑定和 Skill 绑定
- 对任意角色发起 **Test Chat**
- 职位使用规范化 key 持久化，并在前端本地化显示
- 角色工作台中的职位配置已改为共享下拉数据源

### 5. 人才市场

- 从 **Sunny-DotNet/agents** 浏览远程角色模板
- 在 **Talent Market** 页面搜索远程角色
- 聘用前先做预览
- 将远程角色 JSON 导入为本地 `AgentRole`
- 展示远程头像、模型、角色摘要以及本地覆盖状态

### 6. Provider Accounts

- 在 UI 中管理模型 Provider 账户
- 加载 Provider 配置和模型列表
- 支持 provider 驱动的角色运行时和 vendor-backed 角色流程

### 7. MCP 与 Skills

- 管理 MCP 目录源、已安装定义、配置实例和角色绑定
- 从市场搜索和安装 MCP
- 管理已安装 Skills，并绑定到角色
- 把 MCP 和 Skills 作为显式运行时能力，而不是隐藏行为

### 8. 实时运行时与通知

- 通过单一 SignalR Notification Hub 推送实时事件
- 跟踪 session、message、frame 和 task 的执行状态
- 把项目执行过程实时反馈给前端

## 当前 Web 控制台模块

`web/apps/web-antd` 当前主要可用模块如下：

| 模块 | 状态 | 用途 |
| --- | --- | --- |
| Projects | Live | 项目生命周期、初始化、导入导出、执行入口 |
| Agent Roles | Live | 角色列表、编辑、角色工作台、测试对话 |
| Talent Market | Live | 远程角色搜索、预览聘用、导入本地 |
| Provider Accounts | Live | Provider 凭据、配置和模型列表管理 |
| Skills | Live | 技能目录、安装和角色绑定 |
| MCP | Live | MCP 目录、安装、服务定义、配置和绑定 |
| Settings | Live | 系统与应用设置 |

还有一些模块仍是脚手架或部分完成状态，例如 sessions、tasks、monitor、permission requests 和 marketplace。

## 架构概览

OpenStaff 是一个模块化 monorepo。

| 路径 | 职责 |
| --- | --- |
| `src/foundation` | 核心领域类型、编排抽象、通知、模块化基础设施 |
| `src/application/OpenStaff.Application.Contracts` | DTO 与应用服务契约 |
| `src/application/OpenStaff.Application` | 应用编排、会话、项目、角色、设置、MCP/Skills 接线 |
| `src/application/OpenStaff.HttpApi` | REST 控制器 |
| `src/infrastructure` | EF Core 持久化、git/file 服务、工作区、导入导出、安全 |
| `src/agents` | 智能体抽象、内置角色运行时、适配器、提示词生成 |
| `src/platform/OpenStaff.AgentSouls` | Soul 目录和别名解析 |
| `src/platform/OpenStaff.Mcp*` | MCP 运行时、内置 shell 集成等能力 |
| `src/platform/OpenStaff.Skills` | Skills 目录和安装支持 |
| `src/platform/OpenStaff.TalentMarket` | 远程人才市场源和缓存 |
| `src/hosts` | API Host、Aspire AppHost、共享服务默认配置 |
| `src/tests` | xUnit 测试 |
| `web` | pnpm workspace 前端 monorepo |

### 关键运行时模型

最重要的运行时概念包括：

- **AgentRole**：可复用角色定义
- **ProjectAgentRole**：分配到项目中的角色实例
- **ChatSession**：长期存在的会话容器
- **ChatFrame**：栈式执行帧，用于追踪嵌套调用和上下文
- **ChatMessage**：持久化消息或系统消息
- **TaskItem**：项目执行中的任务项

## 技术栈

- **后端**：.NET 10、ASP.NET Core、SignalR、EF Core
- **前端**：Vue 3、TypeScript、Ant Design Vue、Vite、vue-vben-admin
- **存储**：本地开发默认使用 SQLite
- **编排**：模块化应用/运行时分层，支持 Aspire
- **包管理**：`dotnet` + `pnpm`

## 快速开始

### 前置依赖

- .NET SDK 10+
- Node.js 20.19+、22.18+ 或 24+
- pnpm 10+
- Git

### 安装依赖

```powershell
dotnet restore src\OpenStaff.slnx
dotnet build src\OpenStaff.slnx

Set-Location web
pnpm install
Set-Location ..
```

### 使用 AppHost 启动

如果你希望一次启动后端和前端，这是当前最推荐的本地联调入口。

```powershell
dotnet run --project src\hosts\OpenStaff.AppHost
```

### API 与前端分别启动

后端：

```powershell
dotnet run --project src\hosts\OpenStaff.Api
```

前端：

```powershell
Set-Location web
pnpm dev:antd
```

## 常用开发命令

### 后端

```powershell
dotnet build src\OpenStaff.slnx
dotnet test src\OpenStaff.slnx
dotnet run --project src\hosts\OpenStaff.Api
```

### 前端

```powershell
Set-Location web
pnpm install
pnpm dev:antd
pnpm build:antd
```

## 当前产品形态说明

- 仓库当前是**项目优先**，不是聊天优先
- 秘书角色仍然是项目执行流程里的主要协调入口
- 角色数据持久化在数据库中，远程人才市场角色会被导入为本地角色记录
- 职位使用规范化 key 存储，在前端本地化显示
- 当前最成熟的产品面主要集中在：
  - 项目
  - 角色管理
  - 人才市场
  - Provider Accounts
  - MCP / Skills
  - 项目脑暴与项目群执行

## 文档

- `docs/README.md`
- `docs/PRD.md`
- `docs/agent-runtime-lifecycle.md`

## 仓库地址

GitHub: `https://github.com/Sunny-DotNet/open-staff`
