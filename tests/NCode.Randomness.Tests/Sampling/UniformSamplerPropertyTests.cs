// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Sampling;

namespace NCode.Randomness.Tests.Sampling;

public class UniformSamplerPropertyTests
{
    private const string Replay = "20261125,1";

    #region NextInteger Tests

    [Property(Replay = Replay)]
    public void NextInteger_WithAnyByteRange_ReturnsAValueInTheRange(int seed, byte a, byte b)
    {
        if (a == b)
        {
            return;
        }

        var (min, max) = a < b ? (a, b) : (b, a);
        var source = new SeededSource(seed);

        var value = UniformSampler.NextInteger(source, min, max);

        Assert.InRange(value, min, (byte)(max - 1));
    }

    [Property(Replay = Replay)]
    public void NextInteger_WithAnyInt64Range_ReturnsAValueInTheRange(int seed, long a, long b)
    {
        if (a == b)
        {
            return;
        }

        var (min, max) = a < b ? (a, b) : (b, a);
        var source = new SeededSource(seed);

        var value = UniformSampler.NextInteger(source, min, max);

        Assert.InRange(value, min, max - 1);
    }

    [Property(Replay = Replay)]
    public void NextInteger_WithTheSameBytes_ReturnsTheSameValue(int seed, long a, long b)
    {
        if (a == b)
        {
            return;
        }

        var (min, max) = a < b ? (a, b) : (b, a);

        var firstSource = new SeededSource(seed);
        var secondSource = new SeededSource(seed);

        var first = UniformSampler.NextInteger(firstSource, min, max);
        var second = UniformSampler.NextInteger(secondSource, min, max);

        Assert.Equal(first, second);
    }

    #endregion

    #region NextDouble Tests

    [Property(Replay = Replay)]
    public void NextDouble_WithAnySource_ReturnsAValueInTheUnitInterval(int seed)
    {
        var source = new SeededSource(seed);

        var value = UniformSampler.NextDouble(source);

        var upper = Math.BitDecrement(1.0);

        Assert.InRange(value, 0.0, upper);
    }

    #endregion

    #region Shuffle Tests

    [Property(Replay = Replay)]
    public void Shuffle_WithAnyValues_ReturnsAPermutation(int seed, NonNull<int[]> values)
    {
        var source = new SeededSource(seed);
        var shuffled = values.Get.ToArray();

        var span = shuffled.AsSpan();

        UniformSampler.Shuffle(source, span);

        var expected = values.Get.Order();
        var actual = shuffled.Order();

        Assert.Equal(expected, actual);
    }

    #endregion

    #region GetString Tests

    [Property(Replay = Replay)]
    public void GetString_WithAnyLength_UsesOnlyTheAlphabet(int seed, NonNegativeInt length)
    {
        const string alphabet = "abcdef0123";
        var count = length.Get % 256;
        var source = new SeededSource(seed);

        var text = UniformSampler.GetString(source, alphabet, count);

        Assert.Equal(count, text.Length);
        Assert.All(text, character => Assert.Contains(character, alphabet));
    }

    #endregion

    private sealed class SeededSource(int seed) : IRandomSource
    {
        private Random Random { get; } = new(seed);

        public void Fill(Span<byte> destination) => Random.NextBytes(destination);
    }
}
