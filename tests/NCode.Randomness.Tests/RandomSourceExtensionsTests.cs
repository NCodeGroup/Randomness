// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests;

public class RandomSourceExtensionsTests
{
    #region GetBytes Tests

    [Fact]
    public void GetBytes_WithCount_ReturnsTheSourceBytes()
    {
        var source = new ScriptedRandomSource(1, 2, 3);

        var bytes = source.GetBytes(3);

        Assert.Equal([1, 2, 3], bytes);
    }

    [Fact]
    public void GetBytes_WithNegativeCount_Throws()
    {
        var source = new ScriptedRandomSource();

        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetBytes(-1));
    }

    [Fact]
    public void GetBytes_WhenSourceIsNull_Throws()
    {
        IRandomSource source = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => source.GetBytes(1));

        Assert.Equal("source", exception.ParamName);
    }

    #endregion

    #region NextInteger Tests

    [Fact]
    public void NextInteger_WithoutBounds_ReadsLittleEndianBytes()
    {
        var source = new ScriptedRandomSource(0x01, 0x02, 0x03, 0x84);

        var value = source.NextInteger<int>();

        Assert.Equal(unchecked((int)0x84030201), value);
    }

    [Fact]
    public void NextInteger_WithoutBoundsForUInt128_ReadsSixteenBytes()
    {
        var source = new ScriptedRandomSource([.. Enumerable.Repeat((byte)0xFF, 16)]);

        var value = source.NextInteger<UInt128>();

        Assert.Equal(UInt128.MaxValue, value);
        Assert.Equal(0, source.Remaining);
    }

    [Fact]
    public void NextInteger_WhenCandidateIsOutOfRange_RejectsItAndDrawsAgain()
    {
        // A range of 6 needs 3 bits: 0x07 masks to 7, which is rejected; 0xFD masks to 5, which is kept.
        var source = new ScriptedRandomSource(0x07, 0xFD);

        var value = source.NextInteger(1, 7);

        Assert.Equal(6, value);
        Assert.Equal(2, source.Requests);
    }

    [Fact]
    public void NextInteger_WithRangeOfOne_ConsumesNoBytes()
    {
        var source = new ScriptedRandomSource();

        var value = source.NextInteger(41, 42);

        Assert.Equal(41, value);
    }

    [Fact]
    public void NextInteger_WithFullSignedRange_WrapsIntoTheRange()
    {
        var source = new ScriptedRandomSource(0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00);

        var value = source.NextInteger(int.MinValue, int.MaxValue);

        // 0xFFFFFFFF equals the range, so it is rejected and the value comes from the next four bytes.
        Assert.Equal(int.MinValue, value);
        Assert.Equal(2, source.Requests);
    }

    [Fact]
    public void NextInteger_WithNegativeBounds_StaysInRange()
    {
        var source = new ScriptedRandomSource(0x02);

        var value = source.NextInteger((sbyte)-10, (sbyte)-5);

        Assert.Equal((sbyte)-8, value);
    }

    [Fact]
    public void NextInteger_WithInt128Range_UsesOnlyTheBytesTheRangeNeeds()
    {
        // A range of 300 needs 9 bits, so two bytes: 0x012B is 299.
        var source = new ScriptedRandomSource(0x2B, 0x01);
        var minimum = Int128.MinValue;
        var maximum = Int128.MinValue + 300;

        var value = source.NextInteger(minimum, maximum);

        Assert.Equal(Int128.MinValue + 299, value);
        Assert.Equal(0, source.Remaining);
    }

    [Fact]
    public void NextInteger_WithMaxOnly_StartsAtZero()
    {
        var source = new ScriptedRandomSource(0x03);

        var value = source.NextInteger(10L);

        Assert.Equal(3L, value);
    }

    [Fact]
    public void NextInteger_WhenRangeIsEmpty_Throws()
    {
        var source = new ScriptedRandomSource();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => source.NextInteger(5, 5));

        Assert.Equal("minInclusive", exception.ParamName);
    }

    [Fact]
    public void NextInteger_WhenMaxIsZero_Throws()
    {
        var source = new ScriptedRandomSource();

        Assert.Throws<ArgumentOutOfRangeException>(() => source.NextInteger(0u));
    }

    #endregion

    #region Floating-Point Tests

    [Fact]
    public void NextDouble_WithAllBitsSet_IsBelowOne()
    {
        var source = new ScriptedRandomSource([.. Enumerable.Repeat((byte)0xFF, 8)]);

        var value = source.NextDouble();

        Assert.Equal(1.0 - Math.Pow(2, -53), value);
    }

    [Fact]
    public void NextDouble_WithNoBitsSet_IsZero()
    {
        var source = new ScriptedRandomSource(new byte[8]);

        Assert.Equal(0.0, source.NextDouble());
    }

    [Fact]
    public void NextSingle_WithAllBitsSet_IsBelowOne()
    {
        var source = new ScriptedRandomSource(0xFF, 0xFF, 0xFF, 0xFF);

        var value = source.NextSingle();

        Assert.Equal(1.0f - MathF.Pow(2, -24), value);
    }

    #endregion

    #region NextBoolean Tests

    [Theory]
    [InlineData(0x00, false)]
    [InlineData(0x01, true)]
    [InlineData(0xFE, false)]
    public void NextBoolean_WithByte_ReadsTheLowBit(byte input, bool expected)
    {
        var source = new ScriptedRandomSource(input);

        Assert.Equal(expected, source.NextBoolean());
    }

    #endregion

    #region NextGuid Tests

    [Fact]
    public void NextGuid_Always_IsVersionFourWithTheRfcVariant()
    {
        var source = new ScriptedRandomSource([.. Enumerable.Repeat((byte)0xFF, 16)]);

        var guid = source.NextGuid();

        Assert.Equal(4, guid.Version);
        Assert.Equal(0b10, guid.Variant >> 2);
    }

    #endregion

    #region Shuffle Tests

    [Fact]
    public void Shuffle_WithScriptedSwaps_MovesEachElementOnce()
    {
        // i = 2 picks j in [0, 3): 0x00 -> 0; i = 1 picks j in [0, 2): 0x00 -> 0.
        var source = new ScriptedRandomSource(0x00, 0x00);
        int[] values = [1, 2, 3];

        source.Shuffle<int>(values);

        Assert.Equal([2, 3, 1], values);
    }

    [Fact]
    public void Shuffle_WithOneElement_ConsumesNoBytes()
    {
        var source = new ScriptedRandomSource();
        int[] values = [1];

        source.Shuffle<int>(values);

        Assert.Equal([1], values);
    }

    #endregion

    #region GetItem Tests

    [Fact]
    public void GetItem_WithChoices_ReturnsTheIndexedChoice()
    {
        var source = new ScriptedRandomSource(0x02);

        var item = source.GetItem<string>(["a", "b", "c"]);

        Assert.Equal("c", item);
    }

    [Fact]
    public void GetItem_WithNoChoices_Throws()
    {
        var source = new ScriptedRandomSource();

        var exception = Assert.Throws<ArgumentException>(() => source.GetItem<string>([]));

        Assert.Equal("choices", exception.ParamName);
    }

    [Fact]
    public void GetItems_WithChoices_FillsEverySlot()
    {
        var source = new ScriptedRandomSource(0x01, 0x00, 0x01);
        var destination = new char[3];

        source.GetItems<char>(['x', 'y'], destination);

        Assert.Equal(['y', 'x', 'y'], destination);
    }

    [Fact]
    public void GetItems_WithNoChoices_Throws()
    {
        var source = new ScriptedRandomSource();

        Assert.Throws<ArgumentException>(() => source.GetItems<char>([], new char[1]));
    }

    #endregion

    #region GetString Tests

    [Fact]
    public void GetString_WithAlphabet_ChoosesEachCharacter()
    {
        var source = new ScriptedRandomSource(0x00, 0x03, 0x01, 0x02);

        var value = source.GetString("abcd", 4);

        Assert.Equal("adbc", value);
    }

    [Fact]
    public void GetString_WithRepeatedCharacter_Throws()
    {
        var source = new ScriptedRandomSource();

        var exception = Assert.Throws<ArgumentException>(() => source.GetString("abca", 4));

        Assert.Equal("alphabet", exception.ParamName);
        Assert.Contains("U+0061", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetString_WithEmptyAlphabet_Throws()
    {
        var source = new ScriptedRandomSource();

        Assert.Throws<ArgumentException>(() => source.GetString("", 4));
    }

    [Fact]
    public void GetString_WithNegativeLength_Throws()
    {
        var source = new ScriptedRandomSource();

        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetString("ab", -1));
    }

    #endregion

    #region AsRandom Tests

    [Fact]
    public void AsRandom_Next_DrawsFromTheSource()
    {
        var source = new ScriptedRandomSource(0x05);
        var random = source.AsRandom();

        Assert.Equal(5, random.Next(10));
    }

    [Fact]
    public void AsRandom_NextWithEqualBounds_ReturnsTheBoundWithoutDrawing()
    {
        var random = new ScriptedRandomSource().AsRandom();

        Assert.Equal(7, random.Next(7, 7));
        Assert.Equal(7L, random.NextInt64(7, 7));
        Assert.Equal(0, random.Next(0));
        Assert.Equal(0L, random.NextInt64(0));
    }

    [Fact]
    public void AsRandom_NextWithBounds_DrawsInRange()
    {
        var source = new ScriptedRandomSource([0x01, 0x01, .. new byte[4], .. new byte[8]]);
        var random = source.AsRandom();

        Assert.Equal(-4, random.Next(-5, 5));
        Assert.Equal(1L, random.NextInt64(0, 1L << 40));
        Assert.Equal(0L, random.NextInt64());
        Assert.Equal(0, source.Remaining);
    }

    [Fact]
    public void AsRandom_WithNegativeBound_Throws()
    {
        var random = new ScriptedRandomSource().AsRandom();

        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt64(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt64(2, 1));
    }

    [Fact]
    public void AsRandom_FloatingPointAndBytes_DrawFromTheSource()
    {
        var source = new ScriptedRandomSource([
            .. new byte[8],
            .. new byte[4],
            .. new byte[4],
            9,
            8,
            7,
            6,
            5,
            4,
        ]);
        var random = source.AsRandom();
        var bytes = new byte[3];
        Span<byte> span = stackalloc byte[3];

        Assert.Equal(0.0, random.NextDouble());
        Assert.Equal(0.0f, random.NextSingle());
        Assert.Equal(0, random.Next());
        random.NextBytes(bytes);
        random.NextBytes(span);

        Assert.Equal([9, 8, 7], bytes);
        Assert.Equal([6, 5, 4], span.ToArray());
    }

    [Fact]
    public void AsRandom_Shuffle_UsesTheSource()
    {
        var random = SystemRandomSource.Instance.AsRandom();
        int[] values = [.. Enumerable.Range(0, 100)];

        random.Shuffle(values);

        Assert.Equal(Enumerable.Range(0, 100), values.Order());
    }

    #endregion

    #region Uniformity Tests

    [Fact]
    public void NextInteger_OverManyDraws_IsUniform()
    {
        // A deterministic stream, so the statistic is the same on every run: no flaky failures.
        var pulse = Pulses.Create("test", 1, Pulses.Epoch);
        using var stream = VerifiableRandom.Create(pulse, "uniformity");
        var counts = new int[6];
        const int draws = 60_000;

        for (var i = 0; i < draws; i++)
        {
            counts[stream.NextInteger(0, 6)]++;
        }

        const double expected = draws / 6.0;
        var chiSquare = counts.Sum(count => Math.Pow(count - expected, 2) / expected);
        // The 0.999 quantile of the chi-square distribution with 5 degrees of freedom.
        Assert.True(
            chiSquare < 20.515,
            $"chi-square {chiSquare} for counts {string.Join(", ", counts)}"
        );
    }

    #endregion
}
