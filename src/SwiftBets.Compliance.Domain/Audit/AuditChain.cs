using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SwiftBets.Compliance.Domain.Audit;

/// <summary>
/// The audit trail is a hash chain: each entry's hash is SHA-256 over the previous hash and the entry's fields, so
/// changing, removing or reordering any stored entry breaks every hash after it.
/// </summary>
public static class AuditChain
{
    public static readonly byte[] Genesis = new byte[32];

    public static byte[] HashOf(byte[] previousHash, AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(previousHash);
        ArgumentNullException.ThrowIfNull(record);
        var r = record.Normalised();
        // Unit separators keep field boundaries unambiguous; a null and an empty string hash differently.
        var canonical = string.Join('\u001f',
            r.AuditId.ToString("D"), r.Service, r.Actor, r.Action, r.SubjectType, r.SubjectId,
            r.Before is null ? "\u0000" : r.Before, r.After is null ? "\u0000" : r.After, r.CorrelationId,
            r.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
        return SHA256.HashData([.. previousHash, .. Encoding.UTF8.GetBytes(canonical)]);
    }

    /// <summary>Checks one stretch of the chain, given the hash before its first entry; returns the first bad sequence.</summary>
    public static long? FirstBreak(byte[] previousHash, IEnumerable<ChainedAuditRecord> entries, out byte[] lastHash)
    {
        ArgumentNullException.ThrowIfNull(entries);
        lastHash = previousHash;
        foreach (var entry in entries)
        {
            if (!entry.PreviousHash.AsSpan().SequenceEqual(lastHash) || !entry.Hash.AsSpan().SequenceEqual(HashOf(lastHash, entry.Record)))
            {
                return entry.Sequence;
            }

            lastHash = entry.Hash;
        }

        return null;
    }
}
