// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// A version 1 content identifier of a DAG-CBOR block hashed with SHA3-512, the only kind Twine uses: varint version
/// 1, varint codec 0x71, then the multihash 0x14 0x40 and its 64-byte digest. It keeps its text as written, which
/// strict decoding makes canonical, and compares by its bytes.
/// </summary>
internal sealed class Cid : IEquatable<Cid>
{
    public const int DigestLength = 64;
    public const byte Version = 0x01;
    public const byte DagCborCodec = 0x71;
    public const byte Sha3_512Code = 0x14;
    private const int Length = 4 + DigestLength;

    private Cid(string text, byte[] bytes)
    {
        Text = text;
        BytesValue = bytes;
    }

    public ReadOnlySpan<byte> Bytes => BytesValue;

    public ReadOnlySpan<byte> Digest => BytesValue.AsSpan(4);

    /// <summary>
    /// Gets the multihash: the hash code, the digest length and the digest.
    /// </summary>
    public ReadOnlySpan<byte> Multihash => BytesValue.AsSpan(2);

    private string Text { get; }

    private byte[] BytesValue { get; }

    public static Cid Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length < 2 || text[0] != 'b')
        {
            throw new TwineFormatException(
                "A content identifier must be multibase base32, starting with 'b'."
            );
        }

        var bytes = Base32.Decode(text.AsSpan(1));
        ThrowIfNotTwine(bytes);

        return new Cid(text, bytes);
    }

    public static Cid FromBytes(ReadOnlySpan<byte> bytes)
    {
        ThrowIfNotTwine(bytes);

        return new Cid("b" + Base32.Encode(bytes), bytes.ToArray());
    }

    public static Cid ForBlock(ReadOnlySpan<byte> block)
    {
        Span<byte> bytes = stackalloc byte[Length];
        bytes[0] = Version;
        bytes[1] = DagCborCodec;
        bytes[2] = Sha3_512Code;
        bytes[3] = DigestLength;
        Sha3.HashData512(block, bytes[4..]);
        return FromBytes(bytes);
    }

    public bool Equals(Cid? other) => other is not null && Bytes.SequenceEqual(other.Bytes);

    public override bool Equals(object? obj) => Equals(obj as Cid);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(Bytes);
        return hash.ToHashCode();
    }

    public override string ToString() => Text;

    private static void ThrowIfNotTwine(ReadOnlySpan<byte> bytes)
    {
        if (
            bytes.Length != Length
            || bytes[0] != Version
            || bytes[1] != DagCborCodec
            || bytes[2] != Sha3_512Code
            || bytes[3] != DigestLength
        )
        {
            throw new TwineFormatException(
                "A content identifier must be CIDv1, DAG-CBOR, SHA3-512."
            );
        }
    }
}
