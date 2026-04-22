# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OpenStaff is a multi-agent software development platform built on [microsoft/agent-framework](https://github.com/microsoft/agents). Multiple specialized AI agents collaborate under a central orchestrator to complete the full software development lifecycle from requirements to code delivery.

**Tech Stack**: .NET 8/10, C#, ASP.NET Core Web API, SignalR, PostgreSQL 16 + EF Core, Vue 3 frontend

## Common Development Commands

### Backend (C#/.NET)

```bash
# Build the solution (solution file is in src/)
dotnet build src/OpenStaff.slnx

# Run all tests
dotnet test src/OpenStaff.slnx

# Run only unit tests
dotnet test src/OpenStaff.slnx --filter "FullyQualifiedName~OpenStaff.Tests.Unit"

# Run the API server (auto-migrates database on startup)
dotnet run --project src/hosts/OpenStaff.Api

# Run specific test
dotnet test src/tests/OpenStaff.Tests --filter "FullyQualifiedName~ClassName.TestName"
```

### Frontend (Vue 3)

```bash
cd web
pnpm install  # Uses pnpm (not npm/yarn)
pnpm dev      # Development server
pnpm build    # Production build
```

### Docker Deployment

```bash
# Set environment variables first
cp .env.example .env  # Edit .env to set DB_PASSWORD

# Start all services (PostgreSQL, API, Frontend)
docker compose up -d
```

## Architecture Overview

### Project Structure

```
src/
├── OpenStaff.Core/          # Domain models and interfaces
│   ├── Agents/              # IAgent interface, AgentContext, AgentMessage
│   ├── Models/              # Domain entities (Project, AgentRole, etc.)
│   ├── Orchestration/       # TaskGraph (dependency-based scheduling)
│   ├── Plugins/             # Plugin system interfaces
│   └── Notifications/       # Notification abstractions
├── OpenStaff.Agents/        # Agent implementations
│   ├── Roles/               # JSON role configs (communicator.json, etc.)
│   ├── Prompts/             # EmbeddedPromptLoader
│   ├── Tools/               # AgentToolRegistry, AgentToolBridge
│   ├── Orchestrator/        # Orchestrator agent implementation
│   └── StandardAgent.cs     # Single agent implementation for all roles
├── OpenStaff.Infrastructure/ # Infrastructure concerns
│   ├── Persistence/         # EF Core AppDbContext, migrations
│   ├── Git/                 # GitService for version control
│   └── Security/            # EncryptionService
├── OpenStaff.Api/           # Web API + SignalR
│   ├── Controllers/         # REST endpoints
│   ├── Hubs/                # NotificationHub (single SignalR hub)
│   ├── Services/            # SessionRunner, OrchestrationService, ProjectService
│   └── Middleware/          # Error handling, locale
├── OpenStaff.ServiceDefaults/ # .NET Aspire service defaults
├── OpenStaff.AppHost/       # .NET Aspire orchestration
└── OpenStaff.Tests/         # xUnit tests
    ├── Unit/                # Unit tests (TaskGraph, AgentFactory, etc.)
    ├── Integration/         # Integration tests
    └── E2E/                 # End-to-end tests
```

### Agent System Architecture

**Core Pattern**: All agents use the same `StandardAgent` implementation, differentiated only by role configuration (`RoleConfig`). This is a **configuration-driven architecture**.

- **AgentFactory**: Creates `IAgent` instances by `roleType` (e.g., "communicator", "architect"). Loads role configs from embedded JSON resources in `OpenStaff.Agents/Roles/*.json`.
- **AIAgentFactory**: Wraps `microsoft/agent-framework`'s `AIAgent` to provide LLM provider abstraction (OpenAI, Azure, etc.)
- **AgentToolRegistry**: Manages tool discovery and bridges between `IAgentTool` and `AITool` (for tool-calling)
- **Routing**: Agents can route to other agents via routing markers in JSON config (e.g., `REQUIREMENTS_COMPLETE` → `decision_maker`)

**Role Configuration** (`Roles/*.json`):
- `roleType`: Unique identifier (e.g., "communicator")
- `systemPrompt`: Embedded resource name (e.g., "communicator.system")
- `modelName`: Default LLM model (can be overridden per-project)
- `tools`: List of tool names to enable
- `routing.markers`: Keyword → target role mappings
- `routing.defaultNext`: Default next agent if no markers match

**Available Agent Roles**:
- `communicator` - Understands user requirements and interactive clarification
- `decision_maker` - Technical solution evaluation and optimal path selection
- `architect` - Task decomposition, dependency analysis, allocation strategy
- `producer` - Code generation, file operations, Git management
- `debugger` - Test writing/execution, problem diagnosis
- `image_creator` - Image resource generation (via external API)
- `video_creator` - Video resource generation (via external API)
- `orchestrator` - Central coordination and routing agent

### Chat/Session System

**Stack-Based Frame Execution**:
- `ChatSession` → Multiple `ChatFrame` (stack-ordered) → Multiple `ChatMessage`
- `SessionRunner` executes frames recursively: when an agent routes to another, a child frame is pushed
- **Frame Popping**: Clients can request `PopCurrentFrame` to cancel the current agent and return to parent
- **CancellationToken Hierarchy**: Session-level and Frame-level cancellation tokens for granular control

**Real-Time Streaming**:
- Single `NotificationHub` at `/hubs/notification` (NOT multiple hubs)
- Channels: `global`, `project:{id}`, `session:{id}`
- Streaming method: `StreamSession(sessionId)` returns `IAsyncEnumerable<SessionEvent>`
- **SessionStreamManager**: Manages per-session event streams using replay pattern

### Orchestration System

- **TaskGraph**: Dependency-aware task scheduling with cycle detection and priority-based execution
- **OrchestrationService**: Routes user messages to appropriate agents, manages project-level agent context
- **Context Resolution**: `ProviderResolver` and `ApiKeyResolver` resolve LLM provider/credentials from project settings or global defaults

### Database

- **ORM**: Entity Framework Core with PostgreSQL
- **Migrations**: Located in `OpenStaff.Infrastructure/Migrations/`
- **Auto-Migration**: Runs on API startup in `Program.cs`
- **Default Path**: `~/.staff/openstaff.db` (SQLite for local dev) or PostgreSQL via connection string
- **.NET Aspire**: Uses `OpenStaff.ServiceDefaults` for shared configuration and health checks

### External Services Integration

- **ModelsDevService**: Syncs model data from models.dev API
- **GitHubDeviceAuthService**: GitHub OAuth device flow authentication
- **CopilotTokenService**: GitHub Copilot token management
- **FileProviderService**: File system-based model provider configuration

### SignalR Hub Patterns

- **DO**: Use the single `NotificationHub` for all real-time notifications
- **DO**: Use `INotificationService` to publish events (abstracts SignalR details)
- **DO**: Channel naming pattern: `Channels.Project(projectId)`, `Channels.Session(sessionId)`
- **DON'T**: Create additional hubs—add channels/events to `NotificationHub` instead

## Testing Patterns

- **Unit Tests**: Test business logic in isolation (TaskGraph, AgentFactory, EncryptionService)
- **No External Dependencies**: Unit tests should not hit database, network, or file system
- **Test Location**: `src/tests/OpenStaff.Tests/Unit/` matches source structure
- **Example**: `TaskGraphTests.cs` demonstrates cycle detection, priority ordering, dependency blocking
- **Available test files**:
  - `TaskGraphTests.cs` - Dependency graph scheduling
- `AgentFactoryTests.cs` - Agent creation and role loading
- `StandardAgentTests.cs` - Single agent behavior
- `OrchestrationServiceTests.cs` - Message routing and orchestration
- `EmbeddedPromptLoaderTests.cs` - Prompt resource loading
- `AgentToolRegistryTests.cs` - Tool discovery and registration
- `EncryptionServiceTests.cs` - Security/encryption utilities

## .NET Aspire Integration

This project uses .NET Aspire for service orchestration and health monitoring:

- **OpenStaff.ServiceDefaults**: Shared service configuration (health checks, service discovery, telemetry)
- **OpenStaff.AppHost**: Development orchestration for local microservices management
- **Health Checks**: Available at `/health` endpoints (mapped via `MapDefaultEndpoints()`)
- **Service Discovery**: Aspire handles service-to-service communication in distributed scenarios

**Note**: For local development, you can typically run `dotnet run --project src/hosts/OpenStaff.Api` directly. The AppHost is primarily for orchestrated microservices scenarios.

## Important Implementation Notes

1. **Agent Context**: When working with agents, always ensure `AgentContext` has valid `Provider` and `ApiKey` before processing. See `StandardAgent.ProcessAsync` lines 54-62.

2. **Tool Bridging**: `AgentToolBridge.ToAITools()` converts domain `IAgentTool` to framework `AITool`. Tools are resolved from registry by name, then passed to `AIAgentFactory.CreateAgent()`.

3. **Bilingual System**: The system supports Chinese/English. Prompt loading is language-aware: `_promptLoader.Load(_config.SystemPrompt, language)`. Default is "zh-CN".

4. **Git Integration**: `GitService` handles version control operations. Commits are created automatically when agents generate code.

5. **Plugin System**: Plugins implement `IAgentPlugin` and are loaded via `PluginLoader`. Can add custom roles, tools, and model providers.

6. **Provider Resolution**: Model provider and API key are resolved from:
   - Project-level settings (per-project provider/keys)
   - Global settings (fallback defaults)
   - See `ProviderResolver.cs` and `ApiKeyResolver.cs`

7. **Orchestrator Integration**: The orchestrator agent (`OrchestratorAgent`) implements `IOrchestrator` and routes messages to appropriate agents based on context and routing markers

8. **Chat Client Factory**: `ChatClientFactory` creates `IChatClient` instances for different LLM providers (OpenAI, Azure, etc.) via microsoft/agent-framework

## Common Development Workflows

### Adding a New Agent Role

1. Create or import the persisted `AgentRole` record used by the application runtime
2. Bind the role to the desired provider account and model
3. Register any additional tools or capabilities needed by the role
4. Verify the role can be resolved through the database-backed runtime path

### Modifying Agent Routing

Agent routing is controlled by JSON config `routing.markers`:
- Edit role config to add/modify marker → target role mappings
- Agents output special markers (e.g., "REQUIREMENTS_COMPLETE") to trigger routing
- Default routing is controlled by `routing.defaultNext`

### Database Schema Changes

1. Modify entities in `OpenStaff.Core/Models/`
2. Run `dotnet ef migrations add MigrationName --project src/infrastructure/OpenStaff.Infrastructure`
3. Migration runs automatically on API startup
4. For local development with SQLite: DB is at `~/.staff/openstaff.db`

### Working with SignalR Streams

- Use `INotificationService` to publish events (don't use SignalR directly)
- Channel patterns: `Channels.Project(projectId)`, `Channels.Session(sessionId)`
- Session streaming: `StreamSession(sessionId)` returns `IAsyncEnumerable<SessionEvent>`
- See `SessionStreamManager` for replay pattern implementation

## Frontend Notes

The `web/` directory is a **monorepo** using pnpm workspaces (based on vben-admin):
- Uses `pnpm` package manager (not npm/yarn) - requires pnpm >= 10.0.0
- Multiple UI variants: `web-antd`, `web-antdv-next`, `web-ele`, `web-naive`, `web-tdesign`
- Entry points in `apps/` subdirectory within `web/`
- For development, typically use `pnpm dev` or `pnpm dev:antd`
- Node.js version: ^20.19.0 || ^22.18.0 || ^24.0.0

**Available frontend apps:**
- `apps/web-antd` - Ant Design Vue UI (recommended for development)
- `apps/web-antdv-next` - Ant Design Vue Next (preview)
- `apps/web-ele` - Element Plus UI
- `apps/web-naive` - Naive UI
- `apps/web-tdesign` - TDesign UI
- `apps/backend-mock` - Mock backend for testing

## Configuration Files

- **`.env.example`**: Template for environment variables (primarily `DB_PASSWORD` for Docker)
- **`docker-compose.yml`**: Full-stack deployment (PostgreSQL + API + Vue frontend)
- **`src/OpenStaff.slnx`**: .NET solution file (new XML format) - note the `src/` path
- **Role Configs**: `src/platform/OpenStaff.Agents/Roles/*.json` — defines agent behavior, routing, and tools

## Dependency Injection Patterns

The application uses a layered DI container setup:
- **Singleton services**: AgentFactory, AIAgentFactory, AgentToolRegistry, OrchestrationService, SessionRunner, SessionStreamManager
- **Scoped services**: ProjectService, AgentService, SettingsService (per-request scope)
- **Infrastructure**: Added via `AddInfrastructure()` extension method
- **Aspire integration**: `builder.AddServiceDefaults()` for health checks and service discovery

## Key Architectural Patterns

1. **Configuration-Driven Agents**: All agent behavior is defined by JSON configs in `OpenStaff.Agents/Roles/*.json`
2. **Embedded Resources**: Prompts are stored as embedded resources (e.g., "communicator.system") and loaded by `EmbeddedPromptLoader`
3. **Service Resolution**: `ProviderResolver` and `ApiKeyResolver` handle multi-tenant credential resolution (project-level → global defaults)
4. **Unified Notification Hub**: Single `NotificationHub` for all real-time events (not multiple hubs)
5. **Bilingual Support**: System prompt loading is language-aware (default: "zh-CN")
6. **Auto-Migration**: Database migrations run automatically on API startup
