// A minimal API wired to FoPost end to end.
//
//   export FoPost__ApiKey=fp_...          # or FOPOST_API_KEY
//   export FoPost__WebhookSecret=whsec_...
//   dotnet run --project examples/MinimalApi
//
// The key needs the `posts` and `accounts` scopes.

using FoPost;
using FoPost.AspNetCore;
using FoPost.Resources;

var builder = WebApplication.CreateBuilder(args);

// Binds the `FoPost` section of configuration, validates it at startup, and registers
// FoPostClient as a singleton over an IHttpClientFactory client.
builder.Services.AddFoPost();
builder.Services.AddFoPostHealthCheck(tags: ["ready"]);
builder.Services.AddFoPostWebhookHandler<PublishedPostHandler>();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapFoPostWebhook("/fopost/webhook");

// The client is injected like any other service.
app.MapGet("/workspaces", async (FoPostClient fopost, CancellationToken cancellationToken) =>
{
    var workspaces = await fopost.Workspaces.ListAsync(cancellationToken);

    return Results.Ok(workspaces.Select(workspace => new { workspace.Id, workspace.Name }));
});

app.MapPost("/drafts", async (
    DraftRequest request,
    FoPostClient fopost,
    CancellationToken cancellationToken) =>
{
    var post = await fopost.Posts.CreateAsync(
        new CreatePostOptions
        {
            WorkspaceId = request.WorkspaceId,
            Content = [new PostContent(request.Text)],
            Accounts = request.Accounts,
        },
        cancellationToken);

    return Results.Created($"/drafts/{post.Id}", new { post.Id, post.Status });
});

// Publishing is create, then publish. It returns once delivery is queued, not once the
// post is live — the webhook above is how you learn it went out.
app.MapPost("/drafts/{id}/publish", async (
    string id,
    FoPostClient fopost,
    CancellationToken cancellationToken) =>
{
    await fopost.Posts.PublishAsync(id, cancellationToken);

    return Results.Accepted($"/drafts/{id}");
});

app.Run();

public sealed record DraftRequest(string WorkspaceId, string Text, List<string> Accounts);

internal sealed class PublishedPostHandler : IFoPostWebhookHandler
{
    private readonly ILogger<PublishedPostHandler> _logger;

    public PublishedPostHandler(ILogger<PublishedPostHandler> logger) => _logger = logger;

    public IReadOnlyCollection<string> Events =>
        [FoPostWebhookEvents.PostPublished, FoPostWebhookEvents.PostFailed];

    public Task HandleAsync(FoPostWebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "FoPost {Event} (delivery {DeliveryId}): {Data}",
            webhookEvent.Event,
            webhookEvent.DeliveryId,
            webhookEvent.Data?.ToJsonString());

        return Task.CompletedTask;
    }
}
