// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using NCode.Randomness.Drand.Tests.Fixtures;
using NCode.Randomness.Drand.Tests.TestDrand;

namespace NCode.Randomness.Drand.Tests;

public class DrandRoundVerifierTests
{
    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    private static void AssertInvalid(BeaconException exception, string fragment)
    {
        Assert.Equal(BeaconError.InvalidPulse, exception.Error);
        Assert.Equal(DrandDefaults.BeaconName, exception.Beacon);
        Assert.Contains(fragment, exception.Message, StringComparison.Ordinal);
    }

    #region Verify Tests

    [Fact]
    public void Verify_WithDrandRound_ReturnsTheVerifiedPulse()
    {
        var json = DrandFixtures.Round(DrandFixtures.LatestRound);
        var round = Utf8(json);
        var signature = JsonElement.Parse(json).GetProperty("signature").GetString();

        var pulse = new DrandRoundVerifier().Verify(round);

        Assert.Equal(DrandDefaults.BeaconName, pulse.Beacon);
        Assert.Equal(DrandDefaults.ChainHash, pulse.Chain);
        Assert.Equal(DrandFixtures.LatestRound, pulse.Round);
        Assert.Equal(signature, pulse.Id);
        Assert.Equal(DrandFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal(DrandFixtures.LatestRandomness, Convert.ToHexStringLower(pulse.Randomness));
    }

    [Fact]
    public void Verify_WithTamperedRound_IsInvalid()
    {
        var round = Utf8(
            DrandFixtures
                .Round(DrandFixtures.LatestRound)
                .Replace("\"round\":34271222", "\"round\":34271223", StringComparison.Ordinal)
        );
        var verifier = new DrandRoundVerifier();

        var exception = Assert.Throws<BeaconException>(() => verifier.Verify(round));

        AssertInvalid(exception, "does not verify");
    }

    [Fact]
    public void Verify_WhenTheRoundIsNotTheChains_IsInvalid()
    {
        var round = Utf8(DrandTestChain.Json(5));
        var verifier = new DrandRoundVerifier();

        var exception = Assert.Throws<BeaconException>(() => verifier.Verify(round));

        AssertInvalid(exception, "does not verify");
    }

    [Fact]
    public void Verify_WithMalformedJson_IsInvalid()
    {
        var round = "{"u8.ToArray();
        var verifier = new DrandRoundVerifier();

        var exception = Assert.Throws<BeaconException>(() => verifier.Verify(round));

        AssertInvalid(exception, "not valid JSON");
    }

    [Fact]
    public void Verify_WithAnotherPinnedChain_AcceptsItsRounds()
    {
        var round = Utf8(DrandTestChain.Json(5));

        var pulse = new DrandRoundVerifier(DrandTestChain.Chain).Verify(round);

        var timestamp = DrandTestChain.Chain.TimeOf(5);
        Assert.Equal(5, pulse.Round);
        Assert.Equal(DrandTestChain.Chain.Hash, pulse.Chain);
        Assert.Equal(timestamp, pulse.Timestamp);
    }

    #endregion
}
