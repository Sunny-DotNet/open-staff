# OpenStaff

OpenStaff is a multi-agent software delivery platform for turning a project idea into coordinated execution.

OpenStaff 是一个面向软件研发协作的多智能体平台，重点不是“单助手聊天”，而是把**项目、角色、能力、工作区和执行过程**组织成一个可持续推进的系统。

## What OpenStaff does

OpenStaff is built around a few core ideas:

- **Project-first workflow**: requirements, members, files, tasks, and conversations belong to a project
- **Role-based collaboration**: agents are defined as reusable roles, then assigned into projects
- **Controllable capabilities**: models, MCP servers, skills, and permission requests are explicit and governable
- **Observable runtime**: sessions, frames, messages, and project-group execution are streamed and traceable
- **Extensible platform**: provider accounts, MCP, skills, plugins, and remote role sources are modular

## What is implemented today

The current repository is already centered on a usable product surface, not just a prototype runtime.

### 1. Projects

- Create, edit, delete, import, and export projects
- Initialize project workspaces
- Start a project into execution mode
- Persist project-level settings such as provider/model defaults
- Surface projects directly on the home page and workspace entry

### 2. Project Brainstorm

- Run a project-scoped brainstorm conversation
- Keep the evolving requirements document in `.staff/project-brainstorm.md`
- Let the secretary role guide the user from rough idea to start-ready requirements

### 3. Project Group execution

- Keep a long-lived project group chat for execution
- Route direct `@agent` mentions to the targeted member
- Fall back to the secretary when the user does not explicitly target a member
- Support structured dispatch and capability-request flows during execution

### 4. Agent Roles and Role Workspace

- Manage local roles from the **Agent Roles** page
- Edit name, avatar, job title, description, model settings, soul config, MCP bindings, and skill bindings
- Run **Test Chat** against a role with temporary overrides
- Use normalized job-title keys with localized display
- Configure job titles from a shared dropdown source in the role workspace

### 5. Talent Market

- Browse remote role templates from **Sunny-DotNet/agents**
- Search remote roles in the **Talent Market**
- Preview a hire before importing
- Import remote role JSON into local `AgentRole` records
- Show remote avatar, model, role summary, and local overwrite state

### 6. Provider Accounts

- Manage model-provider accounts from the UI
- Load provider configuration and model lists
- Support provider-driven role runtimes and vendor-backed role flows

### 7. MCP and Skills

- Manage MCP catalog sources, installed MCP definitions, configs, and role bindings
- Search and install MCP entries from the marketplace
- Manage installed skills and bind them to roles
- Keep MCP and skills as explicit runtime capabilities rather than hidden implicit behavior

### 8. Real-time runtime and notifications

- Stream runtime events through a single SignalR notification hub
- Track session, message, frame, and task execution state
- Keep project-scoped execution visible to the frontend in real time

## Current web console modules

These modules are currently the main usable surfaces in `web/apps/web-antd`:

| Module | Status | Purpose |
| --- | --- | --- |
| Projects | Live | Project lifecycle, initialization, import/export, execution entry |
| Agent Roles | Live | Role list, role editing, role workspace, test chat |
| Talent Market | Live | Remote role search, preview hire, import into local roles |
| Provider Accounts | Live | Provider credentials, config, and model list management |
| Skills | Live | Catalog, installation, and role skill binding |
| MCP | Live | Catalog, installation, server definitions, configs, and bindings |
| Settings | Live | System and app settings |

Some modules still exist only as scaffolds or partial surfaces, such as sessions, tasks, monitor, permission requests, and marketplace.

## Architecture overview

OpenStaff is a modular monorepo.

| Path | Responsibility |
| --- | --- |
| `src/foundation` | Core domain types, orchestration abstractions, notifications, modularity |
| `src/application/OpenStaff.Application.Contracts` | DTOs and app-service contracts |
| `src/application/OpenStaff.Application` | Application orchestration, sessions, projects, roles, settings, MCP/skills wiring |
| `src/application/OpenStaff.HttpApi` | REST controllers |
| `src/infrastructure` | EF Core persistence, git/file services, workspace/import-export, security |
| `src/agents` | Agent abstractions, builtin role runtime, adapters, prompt generation |
| `src/platform/OpenStaff.AgentSouls` | Soul catalogs and alias resolution |
| `src/platform/OpenStaff.Mcp*` | MCP runtime, builtin shell integration, related plumbing |
| `src/platform/OpenStaff.Skills` | Skill catalog and installation support |
| `src/platform/OpenStaff.TalentMarket` | Remote talent-market source integration and caching |
| `src/hosts` | API host, Aspire app host, shared service defaults |
| `src/tests` | xUnit tests |
| `web` | pnpm workspace frontend monorepo |

### Runtime model

The most important runtime concepts are:

- **AgentRole**: reusable role definition
- **ProjectAgentRole**: a role assigned into a project
- **ChatSession**: a long-lived conversation container
- **ChatFrame**: stack-based execution frame for nested routing/execution
- **ChatMessage**: persisted conversation or system message
- **TaskItem**: tracked work unit during project execution

## Tech stack

- **Backend**: .NET 10, ASP.NET Core, SignalR, EF Core
- **Frontend**: Vue 3, TypeScript, Ant Design Vue, Vite, vue-vben-admin
- **Storage**: SQLite by default for local development
- **Orchestration**: modular application/runtime layering with Aspire support
- **Package managers**: `dotnet` + `pnpm`

## Quick start

### Prerequisites

- .NET SDK 10+
- Node.js 20.19+, 22.18+, or 24+
- pnpm 10+
- Git

### Install dependencies

```powershell
dotnet restore src\OpenStaff.slnx
dotnet build src\OpenStaff.slnx

Set-Location web
pnpm install
Set-Location ..
```

### Run with AppHost

This is the best local full-stack entry when you want backend + frontend together.

```powershell
dotnet run --project src\hosts\OpenStaff.AppHost
```

### Run API and frontend separately

Backend:

```powershell
dotnet run --project src\hosts\OpenStaff.Api
```

Frontend:

```powershell
Set-Location web
pnpm dev:antd
```

## Common development commands

### Backend

```powershell
dotnet build src\OpenStaff.slnx
dotnet test src\OpenStaff.slnx
dotnet run --project src\hosts\OpenStaff.Api
```

### Frontend

```powershell
Set-Location web
pnpm install
pnpm dev:antd
pnpm build:antd
```

## Notes about the current product shape

- The repository is **project-first**, not chat-first.
- The secretary role is still the main coordination entry in execution flows.
- Role data is persisted in the database; remote talent-market roles are imported into local role records.
- Job titles are stored as normalized keys and localized in the frontend.
- The current product surface is strongest around:
  - projects
  - role management
  - talent market
  - provider accounts
  - MCP / skills
  - project brainstorm + project group execution

## Documentation

- `docs/README.md`
- `docs/PRD.md`
- `docs/agent-runtime-lifecycle.md`

## Repository

GitHub: `https://github.com/Sunny-DotNet/open-staff`
