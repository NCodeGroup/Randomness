// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Entropy;
using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests.Entropy;

public class BeaconEntropyPoolTests
{
    #region Mix Tests

    [Fact]
    public void Current_BeforeAnyPulse_IsEmpty()
    {
        var pool = new BeaconEntropyPool();

        Assert.Equal(0, pool.Current.Epoch);
        Assert.Equal(new byte[BeaconEntropySnapshot.DigestLength], pool.Current.Digest.ToArray());
    }

    [Fact]
    public void Mix_WithPulse_AdvancesTheEpochAndDigest()
    {
        var pool = new BeaconEntropyPool();

        var first = pool.Mix(Pulses.Create("a", 1, Pulses.Epoch));
        var second = pool.Mix(Pulses.Create("a", 2, Pulses.Epoch));

        Assert.Equal(2, second.Epoch);
        Assert.Same(second, pool.Current);
        Assert.NotEqual(first.Digest.ToArray(), second.Digest.ToArray());
    }

    [Fact]
    public void Mix_WithSamePulses_IsDeterministic()
    {
        var first = new BeaconEntropyPool();
        var second = new BeaconEntropyPool();

        first.Mix(Pulses.Create("a", 1, Pulses.Epoch));
        second.Mix(Pulses.Create("a", 1, Pulses.Epoch));

        Assert.Equal(first.Current.Digest.ToArray(), second.Current.Digest.ToArray());
    }

    [Fact]
    public void Mix_WithFieldsThatConcatenateAlike_DiffersByLengthPrefix()
    {
        var first = new BeaconEntropyPool();
        var second = new BeaconEntropyPool();

        first.Mix(Pulses.Create("ab", 1, Pulses.Epoch, chain: "c"));
        second.Mix(Pulses.Create("a", 1, Pulses.Epoch, chain: "bc"));

        Assert.NotEqual(first.Current.Digest.ToArray(), second.Current.Digest.ToArray());
    }

    [Fact]
    public void Mix_WithNull_Throws()
    {
        var pool = new BeaconEntropyPool();

        Assert.Throws<ArgumentNullException>(() => pool.Mix(null!));
    }

    #endregion
}
