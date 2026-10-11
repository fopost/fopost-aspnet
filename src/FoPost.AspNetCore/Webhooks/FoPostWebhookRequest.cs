using Microsoft.AspNetCore.Http;

namespace FoPost.AspNetCore;

internal static class FoPostWebhookRequest
{
    // The signature covers the bytes on the wire, so nothing may re-encode them first.
    public static async Task<ReadOnlyMemory<byte>> ReadBodyAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        using var buffer = new MemoryStream();

        await context.Request.Body.CopyToAsync(buffer, context.RequestAborted).ConfigureAwait(false);
        context.Request.Body.Position = 0;

        return buffer.ToArray();
    }

    public static IEnumerable<KeyValuePair<string, string?>> Headers(HttpContext context) =>
        context.Request.Headers.Select(header => new KeyValuePair<string, string?>(header.Key, header.Value.ToString()));
}
