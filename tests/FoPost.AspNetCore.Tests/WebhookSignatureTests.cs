using System.Text;
using Xunit;

namespace FoPost.AspNetCore.Tests;

/// <summary>
/// Covers the hex decoding in <see cref="FoPostWebhookSignature"/> directly. The header is
/// attacker-supplied, so malformed input must return false rather than throw.
/// </summary>
public class WebhookSignatureTests
{
    private const string Secret = "whsec_test";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"event\":\"post.published\"}");

    [Fact]
    public void Compute_round_trips_through_Verify()
    {
        var header = FoPostWebhookSignature.Compute(Body, Secret);

        Assert.StartsWith("sha256=", header, StringComparison.Ordinal);
        Assert.True(FoPostWebhookSignature.Verify(Body, header, Secret));
    }

    [Fact]
    public void An_uppercase_digest_is_accepted()
    {
        var header = FoPostWebhookSignature.Compute(Body, Secret).ToUpperInvariant();

        Assert.True(FoPostWebhookSignature.Verify(Body, header, Secret));
    }

    [Fact]
    public void A_bare_digest_without_the_prefix_is_accepted()
    {
        var bare = FoPostWebhookSignature.Compute(Body, Secret)["sha256=".Length..];

        Assert.True(FoPostWebhookSignature.Verify(Body, bare, Secret));
    }

    [Theory]
    [InlineData("sha256=")]                          // empty digest
    [InlineData("sha256=abcd")]                      // too short
    [InlineData("sha256=" + "ab")]                   // far too short
    [InlineData("not-hex")]                          // not hex at all
    public void A_malformed_digest_is_rejected_without_throwing(string header)
    {
        Assert.False(FoPostWebhookSignature.Verify(Body, header, Secret));
    }

    [Fact]
    public void A_digest_of_the_right_length_with_non_hex_characters_is_rejected()
    {
        // 64 characters, correct length, but 'z' is not a hex digit.
        var header = "sha256=" + new string('z', 64);

        Assert.False(FoPostWebhookSignature.Verify(Body, header, Secret));
    }

    [Fact]
    public void A_digest_one_character_too_long_is_rejected()
    {
        var header = FoPostWebhookSignature.Compute(Body, Secret) + "0";

        Assert.False(FoPostWebhookSignature.Verify(Body, header, Secret));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_header_or_secret_is_rejected(string? value)
    {
        Assert.False(FoPostWebhookSignature.Verify(Body, value, Secret));
        Assert.False(FoPostWebhookSignature.Verify(Body, FoPostWebhookSignature.Compute(Body, Secret), value));
    }

    [Fact]
    public void A_body_that_differs_by_one_byte_is_rejected()
    {
        var header = FoPostWebhookSignature.Compute(Body, Secret);
        var tampered = Encoding.UTF8.GetBytes("{\"event\":\"post.published\"} ");

        Assert.False(FoPostWebhookSignature.Verify(tampered, header, Secret));
    }
}
