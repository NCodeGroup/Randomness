// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// SHA3-512: the platform's implementation where the operating system provides one, and the managed
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

    public static byte[] HashData512(ReadOnlySpan<byte> source)
    {
        var digest = new byte[Sha3_512Length];
        HashData512(source, digest);
        return digest;
    }
}
