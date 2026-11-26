// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness;

namespace SiteSamples.Guides;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class DrawValuesSamples
{
    [Fact]
    public void SiteDrawValuesRangeSample_Runs()
    {
        ISecureRandom random = SystemRandomSource.Instance;

        // begin-snippet: SiteDrawValuesRange
        var roll = random.NextInteger(1, 7); // int in [1, 7), a die roll
        long offset = random.NextInteger(-1_000L, 1_000L); // long in [-1000, 1000)
        UInt128 id = random.NextInteger<UInt128>(); // all 128 bits random
        var index = random.NextInteger(10); // int in [0, 10)
        // end-snippet

        var otherId = random.NextInteger<UInt128>();
        Assert.InRange(roll, 1, 6);
        Assert.InRange(offset, -1_000L, 999L);
        Assert.NotEqual(otherId, id); // two 128-bit draws match with probability 2^-128
        Assert.InRange(index, 0, 9);
    }

    [Fact]
    public void SiteDrawValuesFractionsSample_Runs()
    {
        ISecureRandom random = SystemRandomSource.Instance;

        // begin-snippet: SiteDrawValuesFractions
        double ratio = random.NextDouble(); // in [0, 1), 53 random bits
        float share = random.NextSingle(); // in [0, 1), 24 random bits
        bool heads = random.NextBoolean(); // true or false, equally likely
        // end-snippet

        var flips = new bool[64];
        flips[0] = heads;
        for (var i = 1; i < flips.Length; i++)
        {
            flips[i] = random.NextBoolean();
        }

        Assert.True(ratio is >= 0.0 and < 1.0);
        Assert.True(share is >= 0.0f and < 1.0f);
        // 64 flips land alike with probability 2^-63.
        Assert.Contains(true, flips);
        Assert.Contains(false, flips);
    }

    [Fact]
    public void SiteDrawValuesShuffleSample_Runs()
    {
        ISecureRandom random = SystemRandomSource.Instance;

        // begin-snippet: SiteDrawValuesShuffle
        int[] deck = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        random.Shuffle<int>(deck); // the same ten numbers, reordered by Fisher–Yates

        string[] colors = ["red", "green", "blue"];
        var color = random.GetItem<string>(colors); // one of the three colors
        var picks = new string[5];
        random.GetItems<string>(colors, picks); // five colors, repeats allowed
        // end-snippet

        var expected = Enumerable.Range(1, 10);
        var sorted = deck.Order();
        Assert.Equal(expected, sorted);
        Assert.Contains(color, colors);
        Assert.Equal(5, picks.Length);
        Assert.All(picks, pick => Assert.Contains(pick, colors));
    }

    [Fact]
    public void SiteDrawValuesAsRandomSample_Runs()
    {
        ISecureRandom random = SystemRandomSource.Instance;

        // begin-snippet: SiteDrawValuesAsRandom
        Random legacy = random.AsRandom();
        var next = legacy.Next(10); // int in [0, 10), drawn from the same source
        // end-snippet

        Assert.InRange(next, 0, 9);
    }
}
