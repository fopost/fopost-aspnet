using System.ComponentModel.DataAnnotations;

namespace FoPost.AspNetCore;

/// <summary>
/// Settings for the FoPost client, bound from the <c>FoPost</c> configuration section.
/// </summary>
/// <example>
/// <code language="json">
/// {
///   "FoPost": {
///     "ApiKey": "fp_...",
///     "BaseUrl": "https://api.fopost.com",
///     "Timeout": "00:00:30",
///     "MaxRetries": 3,
///     "WebhookSecret": "whsec_..."
///   }
/// }
/// </code>
/// </example>
public sealed class FoPostOptions : IValidatableObject
{
    private static readonly string[] ApiKeyMember = [nameof(ApiKey)];
    private static readonly string[] BaseUrlMember = [nameof(BaseUrl)];
    private static readonly string[] TimeoutMember = [nameof(Timeout)];

    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "FoPost";

    /// <summary>Environment variable consulted when no key is configured.</summary>
    public const string ApiKeyEnvironmentVariable = "FOPOST_API_KEY";

    /// <summary>
    /// API key from the dashboard, under Settings → API Keys. Sent as <c>X-API-Key</c>.
    /// Falls back to the <c>FOPOST_API_KEY</c> environment variable.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// A dashboard session token, sent as <c>Authorization: Bearer</c>. Only needed for the
    /// handful of endpoints that do not accept an API key. Set it and it wins over
    /// <see cref="ApiKey"/>.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>API root. Override it for staging or a self-hosted deployment.</summary>
    [Required]
    public string BaseUrl { get; set; } = FoPostClientOptions.DefaultBaseUrl;

    /// <summary>How long a single request may take before it is abandoned.</summary>
    public TimeSpan Timeout { get; set; } = FoPostClientOptions.DefaultTimeout;

    /// <summary>
    /// Total attempts for a request the API answers with 429, including the first. The wait
    /// comes from the <c>Retry-After</c> header.
    /// </summary>
    [Range(1, 10)]
    public int MaxRetries { get; set; } = FoPostClientOptions.DefaultMaxRetries;

    /// <summary>
    /// Shared secret for the webhook registered against this app, used to verify the
    /// <c>X-FoPost-Signature</c> header. Required only when <c>MapFoPostWebhook</c> is used.
    /// </summary>
    public string? WebhookSecret { get; set; }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ApiKey) && string.IsNullOrWhiteSpace(BearerToken))
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(ApiKey)} is required — set it in configuration or in the " +
                $"{ApiKeyEnvironmentVariable} environment variable.",
                ApiKeyMember);
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(BaseUrl)} must be an absolute URL.",
                BaseUrlMember);
        }

        if (Timeout <= TimeSpan.Zero)
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(Timeout)} must be greater than zero.",
                TimeoutMember);
        }
    }

    internal FoPostClientOptions ToClientOptions(HttpClient httpClient) => new()
    {
        ApiKey = ApiKey,
        BearerToken = BearerToken,
        BaseUrl = BaseUrl,
        MaxRetries = MaxRetries,
        // Ignored by the SDK when a client is supplied — the timeout is set on the
        // IHttpClientFactory client instead.
        Timeout = Timeout,
        HttpClient = httpClient,
    };
}
