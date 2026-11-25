// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Numerics;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// SHA3-512 (rate 72, padding 0x06) and SHAKE256 (rate 136, padding 0x1F) as FIPS 202 specifies them: the Keccak-f[1600]
/// permutation in a sponge with the padding 0x80 in the last byte of the rate. It hashes only public data here, so it
/// makes no constant-time claim.
/// </summary>
internal static class Keccak
{
    private const int Sha3_512Rate = 72;
    private const int Shake256Rate = 136;
    private const int Rounds = 24;

    private static ReadOnlySpan<ulong> RoundConstants =>
        [
            0x0000000000000001UL,
            0x0000000000008082UL,
            0x800000000000808aUL,
            0x8000000080008000UL,
            0x000000000000808bUL,
            0x0000000080000001UL,
            0x8000000080008081UL,
            0x8000000000008009UL,
            0x000000000000008aUL,
            0x0000000000000088UL,
            0x0000000080008009UL,
            0x000000008000000aUL,
            0x000000008000808bUL,
            0x800000000000008bUL,
            0x8000000000008089UL,
            0x8000000000008003UL,
            0x8000000000008002UL,
            0x8000000000000080UL,
            0x000000000000800aUL,
            0x800000008000000aUL,
            0x8000000080008081UL,
            0x8000000000008080UL,
            0x0000000080000001UL,
            0x8000000080008008UL,
        ];

    private static ReadOnlySpan<int> Rotations =>
        [
            0,
            1,
            62,
            28,
            27,
            36,
            44,
            6,
            55,
            20,
            3,
            10,
            43,
            25,
            39,
            41,
            45,
            15,
            21,
            8,
            18,
            2,
            61,
            56,
            14,
        ];

    public static void Sha3_512(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length < Sha3.Sha3_512Length)
        {
            throw new ArgumentException("The destination is too small.", nameof(destination));
        }

        Span<ulong> state = stackalloc ulong[25];
        state.Clear();
        while (source.Length >= Sha3_512Rate)
        {
            Absorb(state, source[..Sha3_512Rate]);
            source = source[Sha3_512Rate..];
        }

        Span<byte> last = stackalloc byte[Sha3_512Rate];
        last.Clear();
        source.CopyTo(last);
        last[source.Length] ^= 0x06;
        last[Sha3_512Rate - 1] ^= 0x80;
        Absorb(state, last);
        for (var i = 0; i < Sha3.Sha3_512Length / sizeof(ulong); i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination[(i * sizeof(ulong))..], state[i]);
        }
    }

    public static void Shake256(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Span<ulong> state = stackalloc ulong[25];
        state.Clear();
        while (source.Length >= Shake256Rate)
        {
            Absorb(state, source[..Shake256Rate]);
            source = source[Shake256Rate..];
        }

        Span<byte> block = stackalloc byte[Shake256Rate];
        block.Clear();
        source.CopyTo(block);
        block[source.Length] ^= 0x1F;
        block[Shake256Rate - 1] ^= 0x80;
        Absorb(state, block);

        while (true)
        {
            for (var i = 0; i < Shake256Rate / sizeof(ulong); i++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(block[(i * sizeof(ulong))..], state[i]);
            }

            var count = Math.Min(Shake256Rate, destination.Length);
            block[..count].CopyTo(destination);
            destination = destination[count..];
            if (destination.IsEmpty)
            {
                return;
            }

            Permute(state);
        }
    }

    private static void Absorb(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        for (var i = 0; i < block.Length / sizeof(ulong); i++)
        {
            state[i] ^= BinaryPrimitives.ReadUInt64LittleEndian(block[(i * sizeof(ulong))..]);
        }

        Permute(state);
    }

    private static void Permute(Span<ulong> a)
    {
        Span<ulong> b = stackalloc ulong[25];
        Span<ulong> c = stackalloc ulong[5];
        for (var round = 0; round < Rounds; round++)
        {
            // Theta.
            for (var x = 0; x < 5; x++)
            {
                c[x] = a[x] ^ a[x + 5] ^ a[x + 10] ^ a[x + 15] ^ a[x + 20];
            }

            for (var x = 0; x < 5; x++)
            {
                var d = c[(x + 4) % 5] ^ BitOperations.RotateLeft(c[(x + 1) % 5], 1);
                for (var y = 0; y < 25; y += 5)
                {
                    a[y + x] ^= d;
                }
            }

            // Rho and pi: lane (x, y) moves to (y, 2x + 3y).
            for (var x = 0; x < 5; x++)
            {
                for (var y = 0; y < 5; y++)
                {
                    var index = x + (5 * y);
                    b[y + (5 * (((2 * x) + (3 * y)) % 5))] = BitOperations.RotateLeft(
                        a[index],
                        Rotations[index]
                    );
                }
            }

            // Chi.
            for (var y = 0; y < 25; y += 5)
            {
                for (var x = 0; x < 5; x++)
                {
                    a[y + x] = b[y + x] ^ (~b[y + ((x + 1) % 5)] & b[y + ((x + 2) % 5)]);
                }
            }

            // Iota.
            a[0] ^= RoundConstants[round];
        }
    }
}
