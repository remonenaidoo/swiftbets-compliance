using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SwiftBets.Compliance.Application.Ports;

namespace SwiftBets.Compliance.Infrastructure.Documents;

/// <summary>A token is the file id and expiry, then an HMAC-SHA256 over both, base64url encoded.</summary>
public sealed class HmacDocumentLinks(IOptions<DocumentOptions> options) : IDocumentLinks
{
    private const int PayloadLength = 24;

    public string Sign(Guid fileId, DateTimeOffset expiresAt)
    {
        var payload = new byte[PayloadLength];
        fileId.TryWriteBytes(payload);
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(16), expiresAt.ToUnixTimeSeconds());
        return Base64Url.EncodeToString([.. payload, .. Mac(payload)]);
    }

    public Guid? Verify(string token, DateTimeOffset now)
    {
        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(token);
        }
        catch (FormatException)
        {
            return null;
        }

        if (bytes.Length != PayloadLength + 32 || !CryptographicOperations.FixedTimeEquals(Mac(bytes.AsSpan(0, PayloadLength)), bytes.AsSpan(PayloadLength)))
        {
            return null;
        }

        return BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(16, 8)) >= now.ToUnixTimeSeconds() ? new Guid(bytes.AsSpan(0, 16)) : null;
    }

    private byte[] Mac(ReadOnlySpan<byte> payload) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.SigningKey), payload);
}
