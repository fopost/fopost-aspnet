# FoPost for ASP.NET Core

[![NuGet](https://img.shields.io/nuget/v/FoPost.AspNetCore.svg)](https://www.nuget.org/packages/FoPost.AspNetCore)
[![Downloads](https://img.shields.io/nuget/dt/FoPost.AspNetCore.svg)](https://www.nuget.org/packages/FoPost.AspNetCore)
[![CI](https://img.shields.io/github/actions/workflow/status/fopost/fopost-aspnet/ci.yml?branch=main&label=ci)](https://github.com/fopost/fopost-aspnet/actions)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/fopost/fopost-aspnet/blob/main/LICENSE)

Official ASP.NET Core integration for the [FoPost](https://fopost.com) API. Schedule and publish to +30 social
platforms from your code.

This package is a thin wrapper around [`FoPost.Sdk`](https://github.com/fopost/fopost-dotnet).
It adds nothing to the API surface — no models, no HTTP, no retries, no error types. What it
adds is the wiring an ASP.NET Core app expects:

- `services.AddFoPost(…)` — the options pattern, validated at startup, client registered in DI
- an `IHttpClientFactory` client under the hood, so handlers and DNS behave
- `app.MapFoPostWebhook("/fopost/webhook")` — raw-body signature verification and typed handlers
- `services.AddFoPostHealthCheck()` — a health check that never mentions your key

## Requirements

- .NET 8 or .NET 9
- A FoPost API key from [fopost.com/dashboard/api-keys](https://fopost.com/dashboard/api-keys)

## Installation

```bash
dotnet add package FoPost.AspNetCore
```

`FoPost.Sdk` comes with it.

## Quick start

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFoPost();                       // binds the "FoPost" configuration section
builder.Services.AddFoPostHealthCheck(tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapFoPostWebhook("/fopost/webhook");

app.MapGet("/workspaces", async (FoPostClient fopost, CancellationToken cancellationToken) =>
    await fopost.Workspaces.ListAsync(cancellationToken));

app.Run();
```

## Configuration

```json
{
  "FoPost": {
    "ApiKey": "fp_your_key_here",
    "BaseUrl": "https://api.fopost.com",
    "Timeout": "00:00:30",
    "MaxRetries": 3,
    "WebhookSecret": "whsec_your_secret_here"
  }
}
```

| Setting         | Default                    | What it does                                                        |
| --------------- | -------------------------- | ------------------------------------------------------------------- |
| `ApiKey`        | `FOPOST_API_KEY` env var   | Sent as `X-API-Key`. Required unless `BearerToken` is set            |
| `BearerToken`   | none                       | Dashboard session token for the few endpoints that need one         |
| `BaseUrl`       | `https://api.fopost.com`   | API root — override for staging or a self-hosted deployment         |
| `Timeout`       | `00:00:30`                 | Per-request timeout                                                 |
| `MaxRetries`    | `3`                        | Attempts for a rate-limited request, honouring `Retry-After`        |
| `WebhookSecret` | none                       | Verifies `X-FoPost-Signature`. Required only for `MapFoPostWebhook` |

Never commit the key. Use user-secrets in development and an environment variable in
production — `FoPost__ApiKey`, or the SDK's own `FOPOST_API_KEY`, which is consulted when
nothing is configured.

The options are validated with `ValidateDataAnnotations().ValidateOnStart()`, so a missing
key or a malformed base URL fails the host at startup rather than on the first request.

### Registering without configuration binding

```csharp
builder.Services.AddFoPost(options =>
{
    options.ApiKey = keyFromYourVault;
    options.MaxRetries = 5;
});

// or bind an explicit section, then adjust
builder.Services.AddFoPost(
    builder.Configuration.GetSection("Integrations:FoPost"),
    options => options.Timeout = TimeSpan.FromSeconds(10));
```

## Injecting the client

`FoPostClient` is registered as a singleton and behaves like any other service.

```csharp
[ApiController]
[Route("posts")]
public sealed class PostsController : ControllerBase
{
    private readonly FoPostClient _fopost;

    public PostsController(FoPostClient fopost) => _fopost = fopost;

    [HttpPost]
    public async Task<IActionResult> Create(DraftRequest request, CancellationToken cancellationToken)
    {
        var post = await _fopost.Posts.CreateAsync(
            new CreatePostOptions
            {
                WorkspaceId = request.WorkspaceId,
                Content = [new PostContent(request.Text)],
                Accounts = request.Accounts,
            },
            cancellationToken);

        return Created($"/posts/{post.Id}", post);
    }
}
```

### HTTP client

The client is built on a named `IHttpClientFactory` client, `FoPostDefaults.HttpClientName`,
so you can decorate it:

```csharp
builder.Services
    .AddHttpClient(FoPostDefaults.HttpClientName)
    .AddHttpMessageHandler<MyTracingHandler>();
```

Because `FoPostClient` is a singleton it holds its `HttpClient` for the life of the process,
which would defeat the factory's handler rotation. So the registration turns rotation off and
sets `SocketsHttpHandler.PooledConnectionLifetime` to two minutes instead — connections are
recycled and DNS changes are picked up, which is what the rotation was for.

## Receiving webhooks

FoPost signs every delivery with HMAC-SHA256 over the raw request body and sends it as
`X-FoPost-Signature: sha256=<hex>`, alongside `X-FoPost-Event` and `X-FoPost-Delivery`.

`MapFoPostWebhook` reads the raw bytes before anything can re-encode them, compares the
digest in constant time, and answers `401` when it does not match — no handler runs. The
endpoint is anonymous by design: the signature is its authentication.

```csharp
builder.Services.AddFoPostWebhookHandler<PublishedPostHandler>();

app.MapFoPostWebhook("/fopost/webhook");

internal sealed class PublishedPostHandler : IFoPostWebhookHandler
{
    // Leave Events empty to receive every event.
    public IReadOnlyCollection<string> Events =>
        [FoPostWebhookEvents.PostPublished, FoPostWebhookEvents.PostFailed];

    public Task HandleAsync(FoPostWebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        var postId = webhookEvent.Data?["post_id"]?.GetValue<string>();
        // …
        return Task.CompletedTask;
    }
}
```

Handlers are scoped, so they may take a `DbContext` or anything else scoped. They run in
registration order. Throwing surfaces as a `500`, which FoPost retries with backoff — so
throw when the work should be retried and swallow when it should not. `DeliveryId` is unique
per send and is what to key on for idempotency.

Events: `post.published`, `post.failed`, `post.partially_failed`, `delivery.published`,
`delivery.failed`, `delivery.delayed`, `account.health_changed` — all on
`FoPostWebhookEvents`.

## Health checks

```csharp
builder.Services.AddFoPostHealthCheck(tags: ["ready"]);

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
```

The check lists the workspaces the credential can see — the cheapest authenticated call the
API offers, so it proves reachability and the credential in one request. A 429 reports
`Degraded`; anything else reports the registered failure status. Descriptions never include
the key, the base URL, or a raw provider message.

## Errors, retries, and the rest of the API

All of that lives in `FoPost.Sdk` and is unchanged here: the `FoPostException` hierarchy
(`FoPostValidationException`, `FoPostAuthenticationException`, `FoPostPaymentRequiredException`,
`FoPostPermissionDeniedException`, `FoPostNotFoundException`, `FoPostRateLimitException`),
automatic retries on 429, the `Posts` / `Accounts` / `Workspaces` / `Labels` / `Ai` resources,
and `RequestAsync` for endpoints the SDK does not wrap yet.

See the [`FoPost.Sdk` README](https://github.com/fopost/fopost-dotnet) for the full API
surface, and [fopost.com/docs](https://fopost.com/docs) for the product documentation.

## Examples

[`examples/MinimalApi`](examples/MinimalApi) is a runnable app that registers the client,
exposes a health endpoint, receives webhooks, and creates a draft post.

## Support

Open an issue at [fopost/fopost-aspnet](https://github.com/fopost/fopost-aspnet/issues), or
use the contact form at [fopost.com/contact](https://fopost.com/contact).

## Contributing

```bash
dotnet build
dotnet test
dotnet pack src/FoPost.AspNetCore/FoPost.AspNetCore.csproj -c Release
```

`FoPost.Sdk` is not on NuGet yet, so a local build needs the parent packed into the
`local-packages/` feed first — see CLAUDE.md, "Parent dependency".

## License

MIT
