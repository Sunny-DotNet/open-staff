# OpenStaff.Provider.GitHubCopilot

`OpenStaff.Provider.GitHubCopilot` integrates GitHub Copilot as a dynamic, multi-vendor provider source. Instead of assuming a single vendor catalog, it exchanges a GitHub OAuth token for a short-lived Copilot API token, downloads the Copilot model catalog, and maps each exposed endpoint back to OpenStaff protocol flags.

## Responsibility
- Registers the `github-copilot` provider key through `OpenStaffProviderGitHubCopilotModule`.
- Registers `CopilotTokenService` and `IHttpClientFactory`, which the protocol needs to talk to GitHub and Copilot endpoints.
- Exchanges an OAuth device-flow token for a Copilot API token.
- Fetches and caches the Copilot `/models` catalog, then maps supported endpoints such as `/chat/completions`, `/responses`, and `/v1/messages` to internal protocol flags.

## Configuration and environment model
`GitHubCopilotProtocolEnv` inherits `ProtocolEnvBase`, so its public config surface is small:
- `BaseUrl` (default `https://api.individual.githubcopilot.com`)
- `OAuthToken` (encrypted)

Because `OAuthToken` is marked with `[Encrypted]`, the abstractions package will expose it as a secret field and persist it through the normal encrypted env-config flow.

This project also depends on `OpenStaffOptions.WrokingDirectory`, because the protocol caches the downloaded model catalog to `{WrokingDirectory}\providers\github_copilot_models.json`.

## Authentication expectations
GitHub Copilot model discovery requires a GitHub OAuth token obtained from the GitHub device flow. `CopilotTokenService` sends that token to `https://api.github.com/copilot_internal/v2/token`, receives a short-lived Copilot API token, and caches the result in memory until five minutes before expiry.

Model-catalog requests use that Copilot token as a Bearer token and send Copilot-specific headers that mimic the VS Code Copilot Chat client.

## Key classes
- `OpenStaffProviderGitHubCopilotModule`
- `GitHubCopilotProtocol`
- `GitHubCopilotProtocolEnv`
- `CopilotTokenService`
- `CopilotToken`
- model DTOs in `Provider\Protocols\Models.cs`

## Relation to provider abstractions
- Depends on `ProviderAbstractionsModule`.
- Registers `GitHubCopilotProtocol` in `ProviderOptions`.
- Inherits `ProtocolBase<GitHubCopilotProtocolEnv>` instead of `VendorProtocolBase<TProtocolEnv>` because Copilot can expose models from multiple vendors and protocol families.
- This is one of the providers that `ChatClientFactory` resolves dynamically through `CreateProtocolWithEnv(...)` so it can inspect the exact protocol flags exposed by the current account.

## Notable quirks
- `IsVendor` is `false`, because Copilot can expose OpenAI, Anthropic, and other vendors behind a single account.
- Model metadata is cached on disk for one day, and a stale cache is reused if refreshing the catalog fails.
- If the token response contains a `chat_completions` endpoint, its host overrides the default model-catalog host.
- `BaseUrl` exists in the env object, but `ModelsAsync()` currently does not use it for discovery.
- `CopilotTokenService` hashes the OAuth token before using it as an in-memory cache key so plaintext credentials are not retained as dictionary keys.
- `ProtocolName` is the current code string `GitHub Copilot`.
