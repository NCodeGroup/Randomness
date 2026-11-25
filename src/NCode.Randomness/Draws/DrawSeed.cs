// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Text;

namespace NCode.Randomness.Draws;

/// <summary>
/// Encodes a draw's nonce and personalization string, as docs/draws.md specifies: every field length-prefixed with a
/// 4-byte big-endian count, and the round as an 8-byte big-endian integer.
/// </summary>
internal static class DrawSeed
{
    public const string Algorithm = "ncode-randomness-draw/1";

    private static UTF8Encoding Utf8 { get; } =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] Nonce(BeaconPulse pulse)
    {
        var beacon = Utf8.GetBytes(pulse.Beacon);
        var chain = Utf8.GetBytes(pulse.Chain);
        var id = Utf8.GetBytes(pulse.Id);
        var nonce = new byte[
            (3 * sizeof(int)) + beacon.Length + chain.Length + sizeof(long) + id.Length
        ];
        var rest = Write(nonce, beacon);
        rest = Write(rest, chain);
        BinaryPrimitives.WriteInt64BigEndian(rest, pulse.Round);
        Write(rest[sizeof(long)..], id);
        return nonce;
    }

    public static byte[] Personalization(string context)
    {
        var algorithm = Utf8.GetBytes(Algorithm);
        var label = Utf8.GetBytes(context);
        var personalization = new byte[(2 * sizeof(int)) + algorithm.Length + label.Length];
        var rest = Write(personalization, algorithm);
        Write(rest, label);
        return personalization;
    }

    private static Span<byte> Write(Span<byte> destination, ReadOnlySpan<byte> field)
    {
        BinaryPrimitives.WriteInt32BigEndian(destination, field.Length);
        field.CopyTo(destination[sizeof(int)..]);
        return destination[(sizeof(int) + field.Length)..];
    }
}
