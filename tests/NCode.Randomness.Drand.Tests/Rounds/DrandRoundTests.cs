// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Randomness.Drand.Rounds;
using NCode.Randomness.Drand.Tests.Fixtures;
using NCode.Randomness.Drand.Tests.TestDrand;

namespace NCode.Randomness.Drand.Tests.Rounds;

public class DrandRoundTests
{
    private const long FirstFixtureRound = 1000;

    private static JsonElement Parse(string json) =>
        JsonElement.Parse(
            json,
            new JsonDocumentOptions { MaxDepth = 16, AllowDuplicateProperties = false }
        );

    private static string SignatureOf(long round) =>
        Parse(DrandFixtures.Round(round)).GetProperty("signature").GetString() ?? string.Empty;

    private static string RoundJson(string round, string signature) =>
        $$"""{"round":{{round}},"signature":"{{signature}}"}""";

    #region Verify Tests

    [Fact]
    public void Verify_WithFirstFixtureRound_ReturnsRound()
    {
        var json = Parse(DrandFixtures.Round(FirstFixtureRound));

        var round = DrandRound.Verify(json, DrandChain.Quicknet);

        Assert.Equal(FirstFixtureRound, round.Round);
        Assert.Equal(SignatureOf(FirstFixtureRound), Convert.ToHexStringLower(round.Signature));
        Assert.Equal(DrandFixtures.FirstRandomness, Convert.ToHexStringLower(round.Randomness));
        Assert.Equal(DrandChain.Quicknet.TimeOf(FirstFixtureRound), round.Timestamp);
    }

    [Fact]
    public void Verify_WithLatestFixtureRound_ReturnsRound()
    {
        var json = Parse(DrandFixtures.Round(DrandFixtures.LatestRound));

        var round = DrandRound.Verify(json, DrandChain.Quicknet);

        Assert.Equal(DrandFixtures.LatestRound, round.Round);
        Assert.Equal(
            SignatureOf(DrandFixtures.LatestRound),
            Convert.ToHexStringLower(round.Signature)
        );
        Assert.Equal(DrandFixtures.LatestRandomness, Convert.ToHexStringLower(round.Randomness));
        Assert.Equal(DrandFixtures.LatestTimestamp, round.Timestamp);
    }

    [Fact]
    public void Verify_WithoutRandomness_ReturnsSameRandomness()
    {
        var json = Parse(RoundJson("1000", SignatureOf(FirstFixtureRound)));

        var round = DrandRound.Verify(json, DrandChain.Quicknet);

        Assert.Equal(DrandFixtures.FirstRandomness, Convert.ToHexStringLower(round.Randomness));
    }

    [Fact]
    public void Verify_WithWrongRandomness_Throws()
    {
        var json = Parse(
            $$"""{"round":1000,"randomness":"{{new string('0', 64)}}","signature":"{{SignatureOf(
                FirstFixtureRound
            )}}"}"""
        );

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("not the hash", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithNonStringRandomness_Throws()
    {
        var json = Parse(
            $$"""{"round":1000,"randomness":5,"signature":"{{SignatureOf(FirstFixtureRound)}}"}"""
        );

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("not the hash", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithSignatureOfAnotherRound_Throws()
    {
        var json = Parse(RoundJson("1001", SignatureOf(FirstFixtureRound)));

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("does not verify", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithTestChainRound_VerifiesOnTestChainOnly()
    {
        var json = Parse(DrandTestChain.Json(5));

        var round = DrandRound.Verify(json, DrandTestChain.Chain);
        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Equal(5, round.Round);
        Assert.Contains("does not verify", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithIdentitySignature_Throws()
    {
        var json = Parse(RoundJson("1000", "c0" + new string('0', 94)));

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("identity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithPointOutsideG1_Throws()
    {
        var json = Parse(RoundJson("1000", "80" + new string('0', 94)));

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("not a point of G1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithNonHexSignature_Throws()
    {
        var json = Parse(RoundJson("1000", new string('z', 96)));

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("48 bytes of hexadecimal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithShortSignature_Throws()
    {
        var json = Parse(RoundJson("1000", new string('0', 94)));

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("48 bytes of hexadecimal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithoutSignature_Throws()
    {
        var json = Parse("""{"round":1000}""");

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("'signature' is missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithoutRound_Throws()
    {
        var json = Parse($$"""{"signature":"{{SignatureOf(FirstFixtureRound)}}"}""");

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("'round' is missing", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("\"1000\"")]
    [InlineData("9223372036854775807")]
    public void Verify_WithInvalidRound_Throws(string round)
    {
        var json = Parse(RoundJson(round, SignatureOf(FirstFixtureRound)));

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("not a round of the chain", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithArray_Throws()
    {
        var json = Parse("[]");

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("JSON object", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region MessageOf Tests

    [Fact]
    public void MessageOf_WithRound_IsSha256OfBigEndianRound()
    {
        var expected = System.Security.Cryptography.SHA256.HashData([0, 0, 0, 0, 0, 0, 0x03, 0xE8]);

        var actual = DrandRound.MessageOf(1000);

        Assert.Equal(expected, actual);
    }

    #endregion
}
