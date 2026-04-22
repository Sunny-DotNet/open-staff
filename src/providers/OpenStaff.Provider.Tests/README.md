# OpenStaff.Provider.Tests

## Test scope

This project verifies protocol discovery and provider metadata for the provider family. Instead of mocking providers, it boots the real module graph, resolves `IProtocolFactory`, and checks that registered protocols can be created and expose the expected model and configuration metadata.

## Major suite and fixtures

- **`ProtocolModelsTests`**
  - Implements `IAsyncLifetime`
  - Builds a fresh DI container with `AddOpenStaffModules<ProviderTestModule>()`
  - Calls `UseOpenStaffModules()`
  - Initializes the shared `IModelDataSource`
  - Reuses `IProtocolFactory` for the actual assertions
- **`ProviderTestModule`**
  - Aggregates the production modules for:
    - OpenAI
    - Anthropic
    - Google
    - NewApi
    - GitHub Copilot

The current assertions cover:

- every registered protocol can be instantiated
- vendor protocols (`openai`, `anthropic`, `google`) return at least one model from the shared model data source
- `github-copilot` is present without requiring a live authentication flow
- `newapi` stays discoverable and returns an empty model list when `BaseUrl` is not configured
- protocol metadata and `EnvSchema` are available for all registered protocols

## Dependencies on production projects

`OpenStaff.Provider.Tests.csproj` directly references:

- `..\OpenStaff.Provider.Abstractions\OpenStaff.Provider.Abstractions.csproj`
- `..\OpenStaff.Provider.OpenAI\OpenStaff.Provider.OpenAI.csproj`
- `..\OpenStaff.Provider.Anthropic\OpenStaff.Provider.Anthropic.csproj`
- `..\OpenStaff.Provider.Google\OpenStaff.Provider.Google.csproj`
- `..\OpenStaff.Provider.NewApi\OpenStaff.Provider.NewApi.csproj`
- `..\OpenStaff.Provider.GitHubCopilot\OpenStaff.Provider.GitHubCopilot.csproj`
- `..\..\OpenStaff.Core\OpenStaff.Core.csproj`

In practice, the suite also exercises the module system and model catalog plumbing brought in through those provider modules.

## How to run

Run from the repository root:

```powershell
dotnet test src\providers\OpenStaff.Provider.Tests\OpenStaff.Provider.Tests.csproj
```

Run only the protocol suite:

```powershell
dotnet test src\providers\OpenStaff.Provider.Tests\OpenStaff.Provider.Tests.csproj --filter "FullyQualifiedName~ProtocolModelsTests"
```

## What these tests are intended to protect

These tests are meant to catch regressions in:

- provider module registration and protocol factory discovery
- vendor model catalog loading through the shared data source
- protocol metadata needed by configuration and management flows
- safe behavior for protocols that should exist even without live credentials or full endpoint configuration
