namespace FoPost.AspNetCore;

/// <summary>Settings for receiving published blog articles, bound from <c>FoPost:Blog</c>.</summary>
public sealed class FoPostBlogOptions
{
    /// <summary>
    /// The website secret shown once on the blog settings page. Signs every delivery
    /// <c>MapFoPostArticles</c> receives.
    /// </summary>
    public string? WebhookSecret { get; set; }
}
