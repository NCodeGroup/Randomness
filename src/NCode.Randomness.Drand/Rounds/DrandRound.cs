// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Randomness.Drand.Bls;
using Neo.Cryptography.BLS12_381;

namespace NCode.Randomness.Drand.Rounds;

/// <summary>
/// A round of a drand quicknet chain whose BLS12-381 signature verified against the chain's pinned group public
/// key. Members the chain does not sign are ignored, except <c>randomness</c>, which must be the SHA-256 of the
/// signature when present.
/// </summary>
internal sealed class DrandRound
{
    /// <summary>
    /// The number of bytes, 48, in a signature: a compressed point of G1.
    /// </summary>
    private const int SignatureLength = 48;

    /// <summary>
    /// The number of bytes, 32, in a round's randomness: the SHA-256 hash of its signature.
    /// </summary>
    private const int RandomnessLength = 32;

    /// <summary>
    /// Gets the options that parse a document: nesting up to 16 levels deep and no duplicate property names.
    /// </summary>
    internal static JsonDocumentOptions JsonOptions { get; } =
        new() { MaxDepth = 16, AllowDuplicateProperties = false };

    /// <summary>
    /// Gets the domain separation tag of the quicknet signature scheme: RFC 9380 hash-to-curve for G1 with
    /// SHA-256, as the BLS signature ciphersuite names it.
    /// </summary>
    internal static ReadOnlySpan<byte> Domain => "BLS_SIG_BLS12381G1_XMD:SHA-256_SSWU_RO_NUL_"u8;

    /// <summary>
    /// Writes the message the group signs for a round: the SHA-256 of the round number as 8 big-endian bytes.
    /// </summary>
    /// <param name="round">The round number.</param>
    /// <param name="destination">Receives the 32-byte message.</param>
    internal static void MessageOf(long round, Span<byte> destination)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, round);
        SHA256.HashData(bytes, destination);
    }

    /// <summary>
    /// Parses a round document from its UTF-8 JSON, as the HTTP API serves it, and verifies its signature against the
    /// group public key of <paramref name="chain"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON document the chain's endpoint served for one round.</param>
    /// <param name="chain">The pinned chain.</param>
    /// <returns>The verified round.</returns>
    /// <exception cref="DrandFormatException">
    /// The document is not valid JSON, its text is not well-formed Unicode, it is malformed, or its signature does not
    /// verify.
    /// </exception>
    public static DrandRound Verify(ReadOnlySpan<byte> utf8Json, DrandChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        if (!Utf8JsonText.IsText(utf8Json))
        {
            throw new DrandFormatException(
                "The drand round is not valid JSON: its text is not well-formed Unicode."
            );
        }

        JsonElement json;
        try
        {
            json = JsonElement.Parse(utf8Json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new DrandFormatException(
                $"The drand round is not valid JSON: {exception.Message}",
                exception
            );
        }

        return Verify(json, chain);
    }

    /// <summary>
    /// Parses a round document and verifies its signature against the group public key of <paramref name="chain"/>:
    /// <c>e(signature, g2)</c> must equal <c>e(H(message), key)</c>.
    /// </summary>
    /// <param name="json">The JSON document the chain's endpoint served for one round.</param>
    /// <param name="chain">The pinned chain.</param>
    /// <returns>The verified round.</returns>
    /// <exception cref="DrandFormatException">The document is malformed or its signature does not verify.</exception>
    public static DrandRound Verify(JsonElement json, DrandChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        if (json.ValueKind != JsonValueKind.Object)
        {
            throw new DrandFormatException("A drand round is a JSON object.");
        }

        var round = ReadRound(json, chain);
        var signature = ReadSignature(json);

        G1Affine point;
        try
        {
            point = G1Affine.FromCompressed(signature);
        }
        catch (FormatException exception)
        {
            throw new DrandFormatException(
                $"The signature of round {round} is not a point of G1.",
                exception
            );
        }

        if (point.IsIdentity)
        {
            throw new DrandFormatException($"The signature of round {round} is the identity.");
        }

        var key = chain.PublicKey;
        Span<byte> message = stackalloc byte[SHA256.HashSizeInBytes];
        MessageOf(round, message);
        var hashed = G1HashToCurve.Hash(message, Domain);
        var generator = G2Affine.Generator;
        if (Bls12.Pairing(in point, in generator) != Bls12.Pairing(in hashed, in key))
        {
            throw new DrandFormatException($"The BLS signature of round {round} does not verify.");
        }

        var randomness = SHA256.HashData(signature);
        if (json.TryGetProperty("randomness", out var claimed))
        {
            var claimedText =
                claimed.ValueKind == JsonValueKind.String ? claimed.GetString() : null;
            if (
                claimedText is null
                || !TryReadHex(claimedText, RandomnessLength, out var claimedBytes)
                || !CryptographicOperations.FixedTimeEquals(claimedBytes, randomness)
            )
            {
                throw new DrandFormatException(
                    $"The randomness of round {round} is not the hash of its signature."
                );
            }
        }

        var timestamp = chain.TimeOf(round);
        return new DrandRound(round, signature, randomness, timestamp);
    }

    /// <summary>
    /// Reads the round number of a document and checks that it is a round of the chain.
    /// </summary>
    /// <param name="json">The round document.</param>
    /// <param name="chain">The pinned chain.</param>
    /// <returns>The round number.</returns>
    /// <exception cref="DrandFormatException">The <c>round</c> member is missing or not a round of the chain.
    /// </exception>
    private static long ReadRound(JsonElement json, DrandChain chain)
    {
        if (!json.TryGetProperty("round", out var member))
        {
            throw new DrandFormatException("The drand member 'round' is missing.");
        }

        if (
            member.ValueKind != JsonValueKind.Number
            || !member.TryGetInt64(out var round)
            || round < 1
            || round > chain.MaxRound
        )
        {
            throw new DrandFormatException("The drand member 'round' is not a round of the chain.");
        }

        return round;
    }

    /// <summary>
    /// Reads the signature of a document as 48 bytes of hexadecimal.
    /// </summary>
    /// <param name="json">The round document.</param>
    /// <returns>The 48-byte compressed signature.</returns>
    /// <exception cref="DrandFormatException">The <c>signature</c> member is missing or not 48 bytes of hexadecimal.
    /// </exception>
    private static byte[] ReadSignature(JsonElement json)
    {
        if (!json.TryGetProperty("signature", out var member))
        {
            throw new DrandFormatException("The drand member 'signature' is missing.");
        }

        var text = member.ValueKind == JsonValueKind.String ? member.GetString() : null;
        if (text is null || !TryReadHex(text, SignatureLength, out var signature))
        {
            throw new DrandFormatException(
                "The drand member 'signature' is not 48 bytes of hexadecimal."
            );
        }

        return signature;
    }

    /// <summary>
    /// Decodes hexadecimal text of an exact length.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <param name="length">The expected number of bytes.</param>
    /// <param name="bytes">Receives the decoded bytes when the text is valid, and an empty array otherwise.</param>
    /// <returns>
    /// <see langword="true"/> and sets <paramref name="bytes"/> when <paramref name="text"/> is exactly
    /// <paramref name="length"/> bytes of hexadecimal; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TryReadHex(string? text, int length, out byte[] bytes)
    {
        bytes = [];
        if (text is null || text.Length != length * 2)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromHexString(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Initializes a round from verified parts; only <see cref="Verify(JsonElement, DrandChain)"/> builds a round.
    /// </summary>
    /// <param name="round">The number of the round.</param>
    /// <param name="signature">The verified 48-byte signature.</param>
    /// <param name="randomness">The SHA-256 of the signature.</param>
    /// <param name="timestamp">The time the round is due, in UTC.</param>
    private DrandRound(long round, byte[] signature, byte[] randomness, DateTimeOffset timestamp)
    {
        Round = round;
        Signature = signature;
        Randomness = randomness;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the number of the round.
    /// </summary>
    public long Round { get; }

    /// <summary>
    /// Gets the 48-byte compressed G1 signature of the round.
    /// </summary>
    public byte[] Signature { get; }

    /// <summary>
    /// Gets the 32-byte randomness of the round: the SHA-256 of its signature.
    /// </summary>
    public byte[] Randomness { get; }

    /// <summary>
    /// Gets the time the round is due, in UTC.
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}
