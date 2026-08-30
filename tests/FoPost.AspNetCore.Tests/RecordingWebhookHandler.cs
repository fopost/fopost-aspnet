namespace FoPost.AspNetCore.Tests;

internal sealed class RecordingWebhookHandler : IFoPostWebhookHandler
{
    private readonly List<FoPostWebhookEvent> _received = [];

    public RecordingWebhookHandler(params string[] events) => Events = events;

    public IReadOnlyCollection<string> Events { get; }

    public IReadOnlyList<FoPostWebhookEvent> Received => _received;

    public Task HandleAsync(FoPostWebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        _received.Add(webhookEvent);

        return Task.CompletedTask;
    }
}
