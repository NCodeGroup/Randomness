// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness.Drand.Bls;

/// <summary>
/// The <c>expand_message_xmd</c> function of RFC 9380 section 5.3.1, instantiated with SHA-256, which expands a
/// message into uniformly distributed bytes under a domain separation tag.
/// </summary>
internal static class ExpandMessageXmd
{
    private const int BlockBytes = 64;
    private const int OutputBytes = 32;
    private const int MaxDomainBytes = 255;
    private const int MaxLength = 255 * OutputBytes;

    /// <summary>
    /// Expands a message into <paramref name="destination"/>.
    /// </summary>
    /// <param name="message">The message to expand.</param>
    /// <param name="domain">The domain separation tag, from 1 to 255 bytes.</param>
    /// <param name="destination">
    /// Receives the expanded bytes; its length, from 1 to 8160, is the number of bytes to produce.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The domain is empty or longer than 255 bytes, or the destination is outside 1 to 8160 bytes.
    /// </exception>
    public static void Expand(
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> domain,
        Span<byte> destination
    )
    {
        if (domain.Length is 0 or > MaxDomainBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(domain),
                domain.Length,
                "The domain must be from 1 to 255 bytes."
            );
        }

        if (destination.Length is < 1 or > MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destination),
                destination.Length,
                "The destination must be from 1 to 8160 bytes."
            );
        }

        var length = destination.Length;
        var blocks = (length + OutputBytes - 1) / OutputBytes;
        var domainSuffix = (byte)domain.Length;
        Span<byte> lengthBytes = [(byte)(length >> 8), (byte)length];

        Span<byte> zeroBlock = stackalloc byte[BlockBytes];
        zeroBlock.Clear();
        Span<byte> b0 = stackalloc byte[OutputBytes];
        Span<byte> previous = stackalloc byte[OutputBytes];
        Span<byte> input = stackalloc byte[OutputBytes];

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(zeroBlock);
        hash.AppendData(message);
        hash.AppendData(lengthBytes);
        hash.AppendData([0]);
        hash.AppendData(domain);
        hash.AppendData([domainSuffix]);
        hash.GetHashAndReset(b0);

        for (var index = 1; index <= blocks; index++)
        {
            if (index == 1)
            {
                b0.CopyTo(input);
            }
            else
            {
                for (var i = 0; i < OutputBytes; i++)
                {
                    input[i] = (byte)(b0[i] ^ previous[i]);
                }
            }

            hash.AppendData(input);
            hash.AppendData([(byte)index]);
            hash.AppendData(domain);
            hash.AppendData([domainSuffix]);
            hash.GetHashAndReset(previous);

            var offset = (index - 1) * OutputBytes;
            previous[..Math.Min(OutputBytes, length - offset)].CopyTo(destination[offset..]);
        }
    }
}
