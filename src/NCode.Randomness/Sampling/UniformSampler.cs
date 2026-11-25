// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Numerics;

namespace NCode.Randomness.Sampling;

/// <summary>
/// The sampling algorithms that turn random bytes into typed values. They are part of the reproducible draw contract
/// in docs/draws.md: every algorithm reads whole bytes in order, interprets them as little-endian, and is changed only
/// together with that document's version.
/// </summary>
internal static class UniformSampler
{
    private const int MaxIntegerBytes = 16;

    public static T NextInteger<T>(IRandomSource source)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
    {
        var byteCount = T.Zero.GetByteCount();
        var value = ReadUInt128(source, byteCount);
        return T.CreateTruncating(value);
    }

    public static T NextInteger<T>(IRandomSource source, T minInclusive, T maxExclusive)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minInclusive),
                minInclusive,
                "The lower bound must be less than the exclusive upper bound."
            );
        }

        var bits = T.Zero.GetByteCount() * 8;
        var width = bits >= 128 ? UInt128.MaxValue : (UInt128.One << bits) - UInt128.One;
        var difference = maxExclusive - minInclusive;
        var range = UInt128.CreateTruncating(difference) & width;
        var offset = NextBelow(source, range);
        return minInclusive + T.CreateTruncating(offset);
    }

    /// <summary>
    /// Returns a uniform value in [0, <paramref name="range"/>) by bitmask rejection: draw the fewest whole bytes that
    /// cover range - 1, mask them to its bit length, and draw again while the value is not below the range. A range of
    /// one consumes no bytes.
    /// </summary>
    public static UInt128 NextBelow(IRandomSource source, UInt128 range)
    {
        if (range == UInt128.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "The range must not be empty.");
        }

        var largest = range - UInt128.One;
        if (largest == UInt128.Zero)
        {
            return UInt128.Zero;
        }

        var bitLength = 128 - (int)UInt128.LeadingZeroCount(largest);
        var byteCount = (bitLength + 7) / 8;
        var mask = bitLength >= 128 ? UInt128.MaxValue : (UInt128.One << bitLength) - UInt128.One;
        while (true)
        {
            var candidate = ReadUInt128(source, byteCount) & mask;
            if (candidate < range)
            {
                return candidate;
            }
        }
    }

    public static int NextIndex(IRandomSource source, int count) =>
        (int)NextBelow(source, (UInt128)(uint)count);

    public static double NextDouble(IRandomSource source)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        source.Fill(buffer);
        var bits = BinaryPrimitives.ReadUInt64LittleEndian(buffer) >> 11;
        return bits * (1.0 / (1UL << 53));
    }

    public static float NextSingle(IRandomSource source)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        source.Fill(buffer);
        var bits = BinaryPrimitives.ReadUInt32LittleEndian(buffer) >> 8;
        return bits * (1.0f / (1U << 24));
    }

    public static bool NextBoolean(IRandomSource source)
    {
        Span<byte> buffer = stackalloc byte[1];
        source.Fill(buffer);
        return (buffer[0] & 1) == 1;
    }

    public static Guid NextGuid(IRandomSource source)
    {
        Span<byte> buffer = stackalloc byte[16];
        source.Fill(buffer);
        // RFC 9562 version 4: the version nibble is the high nibble of Data3, which .NET stores little-endian in bytes
        // 6 and 7, and the variant is the two high bits of byte 8.
        buffer[7] = (byte)((buffer[7] & 0x0F) | 0x40);
        buffer[8] = (byte)((buffer[8] & 0x3F) | 0x80);
        return new Guid(buffer);
    }

    public static void Shuffle<T>(IRandomSource source, Span<T> values)
    {
        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = NextIndex(source, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    public static T GetItem<T>(IRandomSource source, ReadOnlySpan<T> choices)
    {
        ThrowIfNoChoices(choices);
        return choices[NextIndex(source, choices.Length)];
    }

    public static void GetItems<T>(
        IRandomSource source,
        ReadOnlySpan<T> choices,
        Span<T> destination
    )
    {
        ThrowIfNoChoices(choices);
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = choices[NextIndex(source, choices.Length)];
        }
    }

    public static string GetString(IRandomSource source, string alphabet, int length)
    {
        ArgumentNullException.ThrowIfNull(alphabet);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ValidateAlphabet(alphabet);
        return string.Create(length, (source, alphabet), static (destination, state) =>
        {
            for (var i = 0; i < destination.Length; i++)
            {
                destination[i] = state.alphabet[NextIndex(state.source, state.alphabet.Length)];
            }
        });
    }

    private static void ThrowIfNoChoices<T>(ReadOnlySpan<T> choices)
    {
        if (choices.IsEmpty)
        {
            throw new ArgumentException("There must be at least one choice.", nameof(choices));
        }
    }

    private static void ValidateAlphabet(string alphabet)
    {
        if (alphabet.Length == 0)
        {
            throw new ArgumentException(
                "The alphabet must hold at least one character.",
                nameof(alphabet)
            );
        }

        // A repeated character would be drawn more often than the others, which biases every string.
        var seen = new HashSet<char>(alphabet.Length);
        foreach (var character in alphabet)
        {
            if (!seen.Add(character))
            {
                throw new ArgumentException(
                    $"The alphabet holds the character U+{(int)character:X4} more than once.",
                    nameof(alphabet)
                );
            }
        }
    }

    private static UInt128 ReadUInt128(IRandomSource source, int byteCount)
    {
        Span<byte> buffer = stackalloc byte[MaxIntegerBytes];
        buffer.Clear();
        source.Fill(buffer[..byteCount]);
        return BinaryPrimitives.ReadUInt128LittleEndian(buffer);
    }
}
