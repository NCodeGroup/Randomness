// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// A verified pulse of a Twine RNG chain: its document hashes to its CID, it names the chain, and the chain's key
/// signed it. Its randomness is the SHA3-512 digest in its CID.
/// </summary>
internal sealed class TwinePulse
{
    private const int SaltLength = 64;

    private TwinePulse(
        Cid cid,
        long index,
        Cid? previous,
        byte[] salt,
        byte[] precommitment,
        DateTimeOffset timestamp
    )
    {
        Cid = cid;
        Index = index;
        Previous = previous;
        SaltBytes = salt;
        PrecommitmentBytes = precommitment;
        Timestamp = timestamp;
    }

    public Cid Cid { get; }

    public long Index { get; }

    /// <summary>
    /// Gets the CID of the pulse before this one, its first link, or <see langword="null"/> for the first pulse.
    /// </summary>
    public Cid? Previous { get; }

    public ReadOnlySpan<byte> Salt => SaltBytes;

    /// <summary>
    /// Gets the SHA3-512 digest this pulse commits the next pulse's salt to.
    /// </summary>
    public ReadOnlySpan<byte> Precommitment => PrecommitmentBytes;

    public DateTimeOffset Timestamp { get; }

    private byte[] SaltBytes { get; }

    private byte[] PrecommitmentBytes { get; }

    public static TwinePulse Verify(TwineDocument document, TwineChain chain)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(chain);
        var content = document.Content;
        var chainCid = content.Get<IpldLink>("chain").Cid;
        if (!chainCid.Equals(chain.Cid))
        {
            throw new TwineFormatException(
                $"The pulse belongs to chain {chainCid}, not {chain.Cid}."
            );
        }

        JwsRs256.Verify(document.Signature, chain.Key, document.ContentMultihash());

        var index = content.Get<IpldInteger>("index").Value;
        if (index < 0)
        {
            throw new TwineFormatException($"The pulse index {index} is negative.");
        }

        var links = content.Get<IpldList>("links").Items;
        var previous = links.IsEmpty ? null : links[0].As<IpldLink>("links").Cid;
        var payload = content.Get<IpldMap>("payload");
        var salt = payload.Get<IpldBytes>("salt").Value.ToArray();
        if (salt.Length != SaltLength)
        {
            throw new TwineFormatException(
                $"The pulse salt has {salt.Length} bytes, not {SaltLength}."
            );
        }

        var pre = payload.Get<IpldBytes>("pre").Value;
        if (
            pre.Length != 2 + Cid.DigestLength
            || pre[0] != Cid.Sha3_512Code
            || pre[1] != Cid.DigestLength
        )
        {
            throw new TwineFormatException("The pulse precommitment is not a SHA3-512 multihash.");
        }

        var timestamp = ParseTimestamp(payload.Get<IpldString>("timestamp").Value);
        return new TwinePulse(document.Cid, index, previous, salt, pre[2..].ToArray(), timestamp);
    }

    /// <summary>
    /// Checks that this pulse directly follows <paramref name="previous"/>, and that its salt is the one
    /// <paramref name="previous"/> committed to: SHA3-512(salt XOR the previous CID's digest) equals the previous
    /// precommitment. A beacon therefore fixed this pulse's salt a round before it published the pulse.
    /// </summary>
    public void VerifyFollows(TwinePulse previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (Previous is null || !Previous.Equals(previous.Cid) || previous.Index != Index - 1)
        {
            throw new TwineFormatException(
                $"Pulse {previous.Index} ({previous.Cid}) does not precede pulse {Index}."
            );
        }

        Span<byte> mixed = stackalloc byte[SaltLength];
        var digest = previous.Cid.Digest;
        for (var i = 0; i < SaltLength; i++)
        {
            mixed[i] = (byte)(Salt[i] ^ digest[i]);
        }

        Span<byte> commitment = stackalloc byte[Sha3.Sha3_512Length];
        Sha3.HashData512(mixed, commitment);
        if (!CryptographicOperations.FixedTimeEquals(commitment, previous.Precommitment))
        {
            throw new TwineFormatException(
                $"The salt of pulse {Index} does not match the precommitment of pulse {previous.Index}."
            );
        }
    }

    private static DateTimeOffset ParseTimestamp(string text)
    {
        if (
            !DateTimeOffset.TryParseExact(
                text,
                "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp
            )
        )
        {
            throw new TwineFormatException(
                $"The pulse timestamp '{text}' is not an ISO 8601 UTC time."
            );
        }

        return timestamp;
    }
}
