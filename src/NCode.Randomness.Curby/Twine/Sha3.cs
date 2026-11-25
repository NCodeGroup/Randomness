// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// SHA3-512 and SHAKE256: the platform's implementation where the operating system provides one, and the managed
/// <see cref="Keccak"/> sponge elsewhere, such as on Windows before Windows 11 24H2 and Windows Server 2025.
/// </summary>
internal static class Sha3
{
    public const int Sha3_512Length = 64;

    public static void HashData512(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (SHA3_512.IsSupported)
        {
            SHA3_512.HashData(source, destination);
        }
        else
        {
            Keccak.Sha3_512(source, destination);
        }
    }

    /// <summary>
    /// Computes the SHAKE256 extendable-output function of <paramref name="source"/>, filling
    /// <paramref name="destination"/> with output, using the platform's implementation where it has one.
    /// </summary>
    /// <param name="source">The data to hash.</param>
    /// <param name="destination">The buffer to fill; its length sets the output length.</param>
    public static void Shake256(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (System.Security.Cryptography.Shake256.IsSupported)
        {
            System.Security.Cryptography.Shake256.HashData(source, destination);
        }
        else
        {
            Keccak.Shake256(source, destination);
        }
    }

    public static byte[] HashData512(ReadOnlySpan<byte> source)
    {
        var digest = new byte[Sha3_512Length];
        HashData512(source, digest);
        return digest;
    }
}
