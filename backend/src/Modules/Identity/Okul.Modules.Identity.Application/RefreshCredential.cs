using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Okul.Modules.Identity.Application;

/// <summary>
/// The opaque refresh credential handed to the client: <c>&lt;tenant&gt;.&lt;session&gt;.&lt;secret&gt;</c>. The tenant and
/// session ids only locate the session (under an <c>ITenantScope</c>); nothing is trusted until the secret's hash
/// matches the session's current one, so a forged tenant id finds nothing. Only the hash is stored.
/// </summary>
public readonly record struct RefreshCredential(Guid TenantId, Guid SessionId, string Secret)
{
    private const int SecretBytes = 32;

    public static RefreshCredential New(Guid tenantId, Guid sessionId) =>
        new(tenantId, sessionId, Base64Url(RandomNumberGenerator.GetBytes(SecretBytes)));

    public string Hash => HashOf(Secret);

    public override string ToString() => $"{TenantId:N}.{SessionId:N}.{Secret}";

    public static bool TryParse(string? value, [NotNullWhen(true)] out RefreshCredential? credential)
    {
        credential = null;
        var parts = value?.Split('.');
        if (parts is not { Length: 3 }
            || !Guid.TryParseExact(parts[0], "N", out var tenantId)
            || !Guid.TryParseExact(parts[1], "N", out var sessionId)
            || parts[2].Length < 40)
        {
            return false;
        }

        credential = new RefreshCredential(tenantId, sessionId, parts[2]);
        return true;
    }

    public static string HashOf(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
