// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests;

public class BeaconPulseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithRandomness_KeepsACopy()
    {
        var randomness = new byte[32];

        var pulse = new BeaconPulse("a", "chain", 1, "id", Pulses.Epoch, randomness);
        randomness[0] = 1;

        Assert.Equal(0, pulse.Randomness[0]);
        Assert.Equal("a round 1 (id)", pulse.ToString());
    }

    [Fact]
    public void Constructor_WithTooLittleRandomness_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new BeaconPulse("a", "chain", 1, "id", Pulses.Epoch, new byte[31])
        );

        Assert.Equal("randomness", exception.ParamName);
    }

    [Theory]
    [InlineData("", "chain", "id")]
    [InlineData("a", " ", "id")]
    [InlineData("a", "chain", "")]
    public void Constructor_WithEmptyIdentity_Throws(string beacon, string chain, string id)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new BeaconPulse(beacon, chain, 1, id, Pulses.Epoch, new byte[32])
        );
    }

    [Fact]
    public void Constructor_WithNegativeRound_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BeaconPulse("a", "chain", -1, "id", Pulses.Epoch, new byte[32])
        );
    }

    #endregion
}
