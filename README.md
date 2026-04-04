# OpenStaff — 多智能体软件开发平台 / Multi-Agent Software Development Platform

OpenStaff 是一个基于 [microsoft/agent-framework](https://github.com/microsoft/agents) 的多智能体协作平台，专注于软件开发场景。多个专业化 AI 智能体角色协同工作，在中央调度器的编排下完成从需求收集到代码交付的全流程。

OpenStaff is a multi-agent collaboration platform built on microsoft/agent-framework, focusing on software development. Multiple specialized AI agent roles work together, orchestrated by a central orchestrator, to complete the entire process from requirements gathering to code delivery.

## ✨ 核心特性 / Key Features

- 🤖 **7 种内置智能体角色** — 对话者、决策者、架构者、生产者、调试者、图片/视频创造者
- 🎯 **中央调度器** — 自动路由用户需求到合适的智能体
- 💬 **群聊式协作界面** — 实时观看智能体间的对话和思考过程
- 📋 **任务看板** — 自动分解需求为可执行的子任务
- 🔄 **双重存储点** — Git commit + 数据库记录，随时回溯
- 🔌 **插件系统** — 支持自定义角色和工具扩展
- 🌐 **多语言** — 中文/英文双语界面
- 🐳 **Docker 一键部署** — 开箱即用

## 🏗️ 技术栈 / Tech Stack

| 层级 | 技术 |
|------|------|
| 智能体框架 | microsoft/agent-framework (.NET 1.0.0) |
| 后端 | C# / .NET 8, ASP.NET Core Web API |
| 实时通信 | SignalR (WebSocket) |
| 数据库 | PostgreSQL 16 + EF Core |
| 前端 | Vue 3 + TypeScript + Vite + Pinia |
| 容器化 | Docker + Docker Compose |

## 📁 项目结构 / Project Structure

```
open-staff/
├── src/                            # 后端源码 / Backend source
│   ├── OpenStaff.Core/             # 核心领域模型与接口
│   │   ├── Agents/                 # IAgent 接口、AgentContext、AgentMessage
│   │   ├── Models/                 # 领域模型（Project、AgentRole 等）
│   │   ├── Orchestration/          # 任务图调度（TaskGraph）
│   │   └── Services/               # 服务接口
│   ├── OpenStaff.Agents/           # 智能体实现（AgentFactory + 各角色）
│   ├── OpenStaff.Infrastructure/   # 基础设施（数据库、LLM、Git、加密）
│   ├── OpenStaff.Api/              # Web API 入口 + SignalR Hubs
│   │   ├── Controllers/            # REST API 控制器
│   │   └── Hubs/                   # AgentHub、ProjectHub
│   ├── OpenStaff.Tests/            # 测试项目（xUnit）
│   │   ├── Unit/                   # 单元测试
│   │   ├── Integration/            # 集成测试
│   │   └── E2E/                    # 端到端测试
│   └── OpenStaff.slnx              # 解决方案文件
├── web/                            # Vue 3 前端
│   ├── src/                        # 前端源码
│   ├── vite.config.ts              # Vite 配置
│   └── package.json                # 前端依赖
├── docker-compose.yml              # Docker 部署编排
├── .env.example                    # 环境变量模板
└── docs/                           # 项目文档
```

## 🚀 快速开始 / Quick Start

### 前置条件 / Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- PostgreSQL 16+（或使用 Docker）

### 方式一：Docker Compose（推荐 / Recommended）

```bash
# 1. 克隆仓库 / Clone repo
git clone https://github.com/m67186636/open-staff.git
cd open-staff

# 2. 复制并编辑环境变量 / Copy and edit env
cp .env.example .env
# 编辑 .env 设置数据库密码 / Edit .env to set DB_PASSWORD

# 3. 启动所有服务 / Start all services
docker compose up -d

# 4. 访问 / Visit
# 前端 / Frontend: http://localhost:3000
# API:              http://localhost:5000
```

### 方式二：本地开发 / Local Development

```bash
# 后端 / Backend
cd src
dotnet restore
dotnet build OpenStaff.slnx
dotnet run --project OpenStaff.Api

# 前端 / Frontend（另一个终端 / separate terminal）
cd web
npm install
npm run dev
```

### 运行测试 / Running Tests

```bash
cd src
dotnet test OpenStaff.slnx
```

## 🤖 智能体角色 / Agent Roles

| 角色 | 类型标识 | 职责 |
|------|---------|------|
| 🗣️ **对话者** (Communicator) | `communicator` | 理解用户需求，交互式需求澄清 |
| 🧠 **决策者** (DecisionMaker) | `decision_maker` | 技术方案评估，选择最优路径 |
| 📐 **架构者** (Architect) | `architect` | 任务分解，依赖分析，分配策略 |
| 💻 **生产者** (Producer) | `producer` | 代码生成，文件操作，Git 管理 |
| 🔧 **调试者** (Debugger) | `debugger` | 测试编写/执行，问题诊断 |
| 🎨 **图片创造者** (ImageCreator) | `image_creator` | 图片资源生成（通过外部 API） |
| 🎬 **视频创造者** (VideoCreator) | `video_creator` | 视频资源生成（通过外部 API） |

智能体由 `AgentFactory` 按角色类型动态创建，通过 `IAgent` 接口统一管理。调度器 (`orchestrator`) 负责将用户消息路由到合适的角色。

## ⚙️ 配置 / Configuration

### 环境变量

| 变量 | 说明 | 默认值 |
|------|------|--------|
| `DB_PASSWORD` | PostgreSQL 密码 | *(必填)* |
| `ConnectionStrings__DefaultConnection` | 数据库连接串 | 见 docker-compose.yml |
| `ASPNETCORE_URLS` | API 监听地址 | `http://+:5000` |
| `ASPNETCORE_ENVIRONMENT` | 运行环境 | `Production` |

### 模型供应商配置

通过 Web UI 的设置页面配置 LLM 模型供应商：
- OpenAI / Azure OpenAI
- 通义千问 / 文心一言等 OpenAI 兼容接口

## 📄 API 文档 / API Reference

### REST API

```
POST   /api/projects                                  # 创建工程
GET    /api/projects                                  # 工程列表
POST   /api/projects/:id/initialize                   # 初始化工程
POST   /api/projects/:id/agents/:agentId/message      # 发消息给智能体

GET    /api/projects/:id/tasks                        # 任务列表
GET    /api/projects/:id/files                        # 文件浏览

GET    /api/settings                                  # 全局设置
GET    /api/model-providers                           # 模型供应商
GET    /api/agent-roles                               # 角色定义
GET    /api/monitor                                   # 系统监控
```

### SignalR Hubs

| Hub | 路径 | 用途 |
|-----|------|------|
| AgentHub | `/hubs/agent` | 智能体实时消息、思考过程推送 |
| ProjectHub | `/hubs/project` | 工程进度、任务状态更新 |

## 🧪 测试 / Testing

项目使用 xUnit 测试框架，测试位于 `src/OpenStaff.Tests/`：

- **Unit/** — 单元测试（TaskGraph、AgentFactory、EncryptionService 等）
- **Integration/** — 集成测试
- **E2E/** — 端到端测试

```bash
# 运行所有测试
cd src && dotnet test OpenStaff.slnx

# 仅运行单元测试
cd src && dotnet test OpenStaff.slnx --filter "FullyQualifiedName~OpenStaff.Tests.Unit"
```

## 📝 License

MIT
