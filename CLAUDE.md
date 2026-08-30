# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

## ⚠️ This repository has never been compiled

The `dotnet` SDK was not installed on the machine that authored the initial commit, so
**nothing here has been built, run, or tested locally**. Every type, member, and overload was
matched by reading the parent SDK's source rather than by compiling against it. CI is the
first real verification. Do not assume `dotnet build` or `dotnet test` has ever passed — if
you have a `dotnet` SDK, run both before trusting anything, and fix what falls out.

## What This Is

`FoPost.AspNetCore` on NuGet — the official ASP.NET Core integration for the FoPost API. It is
a **thin wrapper** around `FoPost.Sdk` (the sibling repo `fopost-dotnet`). It contains no API
logic: no HTTP, no models, no retry policy, no error types. Those belong to the parent and must
never be reimplemented here. What lives here is Microsoft.Extensions wiring — dependency
injection, the options pattern, `IHttpClientFactory`, minimal-API endpoints, health checks.

## Brand Rules

- The product is **FoPost** (`fopost.com`). Never write "OwlStack" — retired Aug 2026.
- Never write an email address. Support is https://fopost.com/contact and GitHub issues.
- Never name AI providers/models, infrastructure vendors, or any person. The author is
  Porter Bridge, LLC.

## Architecture

```
src/FoPost.AspNetCore/
  FoPostOptions.cs                     bound from the "FoPost" configuration section
  FoPostDefaults.cs                    client name, webhook path, header names
  FoPostServiceCollectionExtensions.cs AddFoPost — three overloads
  Webhooks/
    FoPostWebhookSignature.cs          HMAC-SHA256 verify, constant time
    FoPostWebhookEndpoint.cs           the handler behind MapFoPostWebhook
    FoPostWebhookEndpointRouteBuilderExtensions.cs
    FoPostWebhookServiceCollectionExtensions.cs   AddFoPostWebhookHandler<T>
    IFoPostWebhookHandler.cs, FoPostWebhookEvent.cs, FoPostWebhookEvents.cs
  Diagnostics/
    FoPostHealthCheck.cs, FoPostHealthCheckExtensions.cs
tests/FoPost.AspNetCore.Tests/         xUnit, fully offline
examples/MinimalApi/                   runnable minimal API
```

Extension methods sit in the namespace their target lives in (`Microsoft.Extensions.
DependencyInjection`, `Microsoft.AspNetCore.Builder`) so they surface without an extra
`using` in an ASP.NET Core app. Everything else is in `FoPost.AspNetCore`.

**Registration flow.** `AddFoPost` builds an `OptionsBuilder<FoPostOptions>`, binds it (from
the ambient configuration, an explicit section, or an inline action), post-configures the
`FOPOST_API_KEY` fallback the SDK contract requires, then
`.ValidateDataAnnotations().ValidateOnStart()`. It registers a named `IHttpClientFactory`
client (`FoPostDefaults.HttpClientName`) and a singleton `FoPostClient` built on it via
`FoPostClientOptions.HttpClient`.

Because the client is a singleton it would pin one handler forever, so handler rotation is
disabled and `SocketsHttpHandler.PooledConnectionLifetime` is set to two minutes instead.
That is the documented way to keep DNS fresh behind a long-lived `HttpClient`. Do not
"fix" this back to a rotating handler without also making `FoPostClient` non-singleton.

**Parent types this wrapper depends on** (all verified by reading `fopost-dotnet/src/FoPost/`):

| Type | Where |
| --- | --- |
| `FoPost.FoPostClient` | `FoPostClient.cs` — ctor `FoPostClient(FoPostClientOptions?)`, `Workspaces`, `Posts`, `BaseUrl`, `IDisposable` |
| `FoPost.FoPostClientOptions` | `FoPostClientOptions.cs` — `ApiKey`, `BearerToken`, `BaseUrl`, `Timeout`, `MaxRetries`, `HttpClient`, and the `Default*` constants |
| `FoPost.FoPostException` and subclasses | `Errors/FoPostException.cs` — `Status`, `Code`, `Body` |
| `FoPost.Resources.WorkspacesResource.ListAsync` | `Resources/WorkspacesResource.cs` — the health check's probe |

`FoPostClientOptions.HttpClient` is the reason `IHttpClientFactory` integration is possible at
all: when it is set, the SDK neither configures nor disposes the client. That is documented on
the property itself.

## API Contract

Owned by `FoPost.Sdk`, repeated here only because the webhook endpoint has to know it:

- Base URL `https://api.fopost.com`, auth header `X-API-Key` (not Bearer), 30s timeout,
  retries on 429 honouring `Retry-After`.
- Error envelope `{ "error": "<code>", "message": "<text>" }`; 402 may carry `upgrade_url`.

**Webhook signature scheme**, read out of the API source
(`fopost/apps/api/src/workers/webhook.worker.ts` and
`services/webhook-dispatcher.ts`), not guessed:

- The API `JSON.stringify`s `{ event, data, timestamp }` and HMAC-SHA256s those UTF-8 bytes
  with the webhook's stored secret, hex-encoded.
- Headers sent: `X-FoPost-Signature: sha256=<hex>`, `X-FoPost-Event: <event>`,
  `X-FoPost-Delivery: <delivery id>`.
- There is **no timestamp in the signature** and no replay window, so nothing here can check
  one. If the API adds `t=…,v1=…` later, this is where it changes.
- Delivery is retried 5 times with exponential backoff on any non-2xx; a webhook is
  auto-disabled after 10 consecutive failures.

## Parent dependency

`FoPost.Sdk` is **not published to NuGet yet** (checked 2026-08-30). The manifest still
declares the real coordinate — `<PackageReference Include="FoPost.Sdk" Version="0.1.0" />` —
because that is what ships. To make the build green today, the parent is resolved from source:

- `NuGet.config` adds a `fopost-local` package source pointing at `local-packages/`.
- Both workflows check out `fopost/fopost-dotnet` into `.parent/` and run
  `dotnet pack .parent/src/FoPost/FoPost.csproj -c Release -o local-packages` before restore.
- Locally, do the same:
  ```bash
  dotnet pack ../fopost-dotnet/src/FoPost/FoPost.csproj -c Release -o local-packages
  ```

**Delete the whole shim once `FoPost.Sdk` 0.1.0 is on NuGet**: the two workflow steps, the
`fopost-local` source in `NuGet.config`, the `local-packages/` folder, and its `.gitignore`
entries. Nothing else references it.

If `fopost/fopost-dotnet` is private, the cross-repo checkout needs a PAT in the repo secret
`PARENT_REPO_TOKEN`; the workflows fall back to `github.token` when it is absent.

## Commands

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet pack src/FoPost.AspNetCore/FoPost.AspNetCore.csproj -c Release -o artifacts
dotnet run --project examples/MinimalApi
```

Run the parent-pack step above first, or restore will fail on `FoPost.Sdk`.

## Conventions

- Multi-targets `net8.0;net9.0`. `Nullable` and `ImplicitUsings` on, `LangVersion` 12,
  `TreatWarningsAsErrors` on (`CS1591` suppressed — doc comments cover the API surface, not
  every member), deterministic build, `GenerateDocumentationFile` on.
- `.editorconfig` is copied from the parent: file-scoped namespaces, 4-space C#, LF, System
  usings first.
- Public types carry XML doc comments. Everything else follows the house rule: short comments,
  only for a non-obvious "why".
- The tests must stay **fully offline**. No test may reach the network. The webhook tests run
  a `TestServer` through `HostBuilder().ConfigureWebHost(…UseTestServer())` rather than
  `WebApplicationFactory<T>`, because that needs a real entry-point assembly and this
  repository has no host project to point it at. `Microsoft.AspNetCore.Mvc.Testing` is still
  referenced, since it is what pins the test host to the target framework.
- Test parallelisation is disabled assembly-wide: two tests read and restore
  `FOPOST_API_KEY`, which is process-wide state.

## Not Allowed

- Do not reimplement anything that belongs to `FoPost.Sdk` — HTTP, retries, models, error
  mapping, the envelope unwrap, or resource methods. If something is missing, fix it in
  `fopost-dotnet`.
- Do not put an API key, base URL, or raw provider exception message into a health-check
  description, a log message, or an HTTP response. Health endpoints are routinely exposed.
- Do not weaken the webhook check: raw bytes only, `CryptographicOperations.FixedTimeEquals`
  only, `401` before any handler runs.

## Releasing

Tag `v<version>` matching `<Version>` in `src/FoPost.AspNetCore/FoPost.AspNetCore.csproj`;
`.github/workflows/release.yml` verifies the two agree, then publishes to NuGet.
Requires repo secret **`NUGET_API_KEY`** on the `nuget` environment (and optionally
`PARENT_REPO_TOKEN`, see "Parent dependency").

## Git

Conventional Commits, atomic. Branch `feature/<description>`, merge to `main` via PR.
Never `gh pr create` — push the branch and hand over the compare link.
