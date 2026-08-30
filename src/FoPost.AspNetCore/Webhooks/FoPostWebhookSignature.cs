using System.Security.Cryptography;
using System.Text;

namespace FoPost.AspNetCore;

/// <summary>
/// Verifies the <c>X-FoPost-Signature</c> header FoPost sends with every webhook:
/// <c>sha256=&lt;hex&gt;</c>, where the hex is an HMAC-SHA256 of the raw request body keyed
/// with the webhook's shared secret.
/// </summary>
public static class FoPostWebhookSignature
{
    private const string Prefix = "sha256=";
    private const int DigestBytes = 32;

    /// <summary>Computes the header value FoPost would send for this body.</summary>
    public static string Compute(ReadOnlySpan<byte> body, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        Span<byte> digest = stackalloc byte[DigestBytes];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body, digest);

        return Prefix + Convert.ToHexString(digest).ToLowerInvariant();
    }

    /// <summary>
    /// True when <paramref name="header"/> matches <paramref name="body"/> under
    /// <paramref name="secret"/>. The comparison is constant time; everything before it
    /// depends only on attacker-supplied data.
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> body, string? header, string? secret)
    {
        if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        var hex = header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            ? header.AsSpan(Prefix.Length)
            : header.AsSpan();

        Span<byte> provided = stackalloc byte[DigestBytes];
        if (!TryParseHex(hex, provided))
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[DigestBytes];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body, expected);

        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    // Convert.TryFromHexString does not exist on net8.0/net9.0, and Convert.FromHexString
    // throws on malformed input that an attacker controls. Parse it ourselves instead.
    private static bool TryParseHex(ReadOnlySpan<char> hex, Span<byte> destination)
    {
        if (hex.Length != destination.Length * 2)
        {
            return false;
        }

        for (var i = 0; i < destination.Length; i++)
        {
            if (!TryParseNibble(hex[i * 2], out var high) || !TryParseNibble(hex[(i * 2) + 1], out var low))
            {
                return false;
            }

            destination[i] = (byte)((high << 4) | low);
        }

        return true;
    }

    private static bool TryParseNibble(char c, out int value)
    {
        value = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };

        return value >= 0;
    }
}
