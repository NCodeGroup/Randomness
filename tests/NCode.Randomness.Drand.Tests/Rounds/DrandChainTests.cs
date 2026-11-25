// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Randomness.Drand.Rounds;
using NCode.Randomness.Drand.Tests.Fixtures;

namespace NCode.Randomness.Drand.Tests.Rounds;

public class DrandChainTests
{
    #region Constants Tests

    [Fact]
    public void Info_WithSavedDocument_MatchesConstants()
    {
        var info = JsonElement.Parse(DrandFixtures.Info);
        var publicKey = info.GetProperty("public_key").GetString();
        var hash = info.GetProperty("hash").GetString();
        var period = info.GetProperty("period").GetInt32();
        var genesisTime = info.GetProperty("genesis_time").GetInt64();
        var scheme = info.GetProperty("schemeID").GetString();

        Assert.Equal(DrandDefaults.PublicKey, publicKey);
        Assert.Equal(DrandDefaults.ChainHash, hash);
        Assert.Equal(DrandDefaults.Period, period);
        Assert.Equal(DrandDefaults.GenesisTime, genesisTime);
        Assert.Equal("bls-unchained-g1-rfc9380", scheme);
    }

    #endregion

    #region Quicknet Tests

    [Fact]
    public void Quicknet_PublicKey_IsNotIdentityAndIsInSubgroup()
    {
        var key = DrandChain.Quicknet.PublicKey;

        Assert.False(key.IsIdentity);
        Assert.True(key.IsTorsionFree);
    }

    [Fact]
    public void Quicknet_Constants_MatchDefaults()
    {
        var chain = DrandChain.Quicknet;

        Assert.Equal(DrandDefaults.ChainHash, chain.Hash);
        Assert.Equal(DrandDefaults.GenesisTime, chain.GenesisTime);
        Assert.Equal(DrandDefaults.Period, chain.Period);
    }

    #endregion

    #region TimeOf Tests

    [Fact]
    public void TimeOf_WithFirstRound_ReturnsGenesis()
    {
        var expected = new DateTimeOffset(2023, 8, 23, 15, 9, 27, TimeSpan.Zero);

        var actual = DrandChain.Quicknet.TimeOf(1);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TimeOf_WithLatestRound_ReturnsLatestTimestamp()
    {
        var actual = DrandChain.Quicknet.TimeOf(DrandFixtures.LatestRound);

        Assert.Equal(DrandFixtures.LatestTimestamp, actual);
    }

    [Fact]
    public void TimeOf_WithMaxRound_Succeeds()
    {
        var chain = DrandChain.Quicknet;

        var actual = chain.TimeOf(chain.MaxRound);

        Assert.True(actual <= DateTimeOffset.MaxValue);
    }

    [Fact]
    public void TimeOf_WithZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DrandChain.Quicknet.TimeOf(0));
    }

    [Fact]
    public void TimeOf_WithRoundAfterMax_Throws()
    {
        var chain = DrandChain.Quicknet;

        Assert.Throws<ArgumentOutOfRangeException>(() => chain.TimeOf(chain.MaxRound + 1));
    }

    #endregion
}
