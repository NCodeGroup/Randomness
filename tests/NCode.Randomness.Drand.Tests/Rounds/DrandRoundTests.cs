// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
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

    private static string SignatureOf(long round)
    {
        var text = DrandFixtures.Round(round);
        var document = Parse(text);
        var signature = document.GetProperty("signature");
        return signature.GetString() ?? string.Empty;
    }

    private static string RoundJson(string round, string signature) =>
        $$"""{"round":{{round}},"signature":"{{signature}}"}""";

    #region Verify Tests

    [Fact]
    public void Verify_WithFirstFixtureRound_ReturnsRound()
    {
        var text = DrandFixtures.Round(FirstFixtureRound);
        var json = Parse(text);

        var round = DrandRound.Verify(json, DrandChain.Quicknet);
        var signature = Convert.ToHexStringLower(round.Signature);
        var randomness = Convert.ToHexStringLower(round.Randomness);
        var timestamp = DrandChain.Quicknet.TimeOf(FirstFixtureRound);

        Assert.Equal(FirstFixtureRound, round.Round);
        var expectedSignature = SignatureOf(FirstFixtureRound);
        Assert.Equal(expectedSignature, signature);
        Assert.Equal(DrandFixtures.FirstRandomness, randomness);
        Assert.Equal(timestamp, round.Timestamp);
    }

    [Fact]
    public void Verify_WithLatestFixtureRound_ReturnsRound()
    {
        var text = DrandFixtures.Round(DrandFixtures.LatestRound);
        var json = Parse(text);

        var round = DrandRound.Verify(json, DrandChain.Quicknet);
        var signature = Convert.ToHexStringLower(round.Signature);
        var randomness = Convert.ToHexStringLower(round.Randomness);

        Assert.Equal(DrandFixtures.LatestRound, round.Round);
        var expectedSignature = SignatureOf(DrandFixtures.LatestRound);
        Assert.Equal(expectedSignature, signature);
        Assert.Equal(DrandFixtures.LatestRandomness, randomness);
        Assert.Equal(DrandFixtures.LatestTimestamp, round.Timestamp);
    }

    [Fact]
    public void Verify_WithoutRandomness_ReturnsSameRandomness()
    {
        var signature = SignatureOf(FirstFixtureRound);
        var text = RoundJson("1000", signature);
        var json = Parse(text);

        var round = DrandRound.Verify(json, DrandChain.Quicknet);
        var randomness = Convert.ToHexStringLower(round.Randomness);

        Assert.Equal(DrandFixtures.FirstRandomness, randomness);
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
        var signature = SignatureOf(FirstFixtureRound);
        var text = RoundJson("1001", signature);
        var json = Parse(text);

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("does not verify", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithTestChainRound_VerifiesOnItsChain()
    {
        var text = DrandTestChain.Json(5);
        var json = Parse(text);

        var round = DrandRound.Verify(json, DrandTestChain.Chain);

        Assert.Equal(5, round.Round);
    }

    [Fact]
    public void Verify_WithTestChainRoundOnQuicknet_Throws()
    {
        var text = DrandTestChain.Json(5);
        var json = Parse(text);

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("does not verify", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithIdentitySignature_Throws()
    {
        var text = RoundJson("1000", "c0" + new string('0', 94));
        var json = Parse(text);

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("identity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithPointOutsideG1_Throws()
    {
        var text = RoundJson("1000", "80" + new string('0', 94));
        var json = Parse(text);

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("not a point of G1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithNonHexSignature_Throws()
    {
        var text = RoundJson("1000", new string('z', 96));
        var json = Parse(text);

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("48 bytes of hexadecimal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithShortSignature_Throws()
    {
        var text = RoundJson("1000", new string('0', 94));
        var json = Parse(text);

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
        var signature = SignatureOf(FirstFixtureRound);
        var text = RoundJson(round, signature);
        var json = Parse(text);

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

    [Fact]
    public void Verify_WithLatestFixtureRoundBytes_ReturnsRound()
    {
        var text = DrandFixtures.Round(DrandFixtures.LatestRound);
        var bytes = Encoding.UTF8.GetBytes(text);

        var round = DrandRound.Verify(bytes, DrandChain.Quicknet);
        var randomness = Convert.ToHexStringLower(round.Randomness);

        Assert.Equal(DrandFixtures.LatestRound, round.Round);
        Assert.Equal(DrandFixtures.LatestRandomness, randomness);
    }

    [Fact]
    public void Verify_WithMalformedJsonBytes_Throws()
    {
        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify("{"u8, DrandChain.Quicknet)
        );

        Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithDuplicateMemberBytes_Throws()
    {
        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify("""{"round":1000,"round":1000}"""u8, DrandChain.Quicknet)
        );

        Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithInvalidUtf8InAString_Throws()
    {
        byte[] json = [.. "{\"round\":1000,\"signature\":\""u8, 0x80, .. "\"}"u8];

        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify(json, DrandChain.Quicknet)
        );

        Assert.Contains("not well-formed Unicode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithLoneSurrogateEscapeInAName_Throws()
    {
        var exception = Assert.Throws<DrandFormatException>(() =>
            DrandRound.Verify("""{"\uD800":1}"""u8, DrandChain.Quicknet)
        );

        Assert.Contains("not well-formed Unicode", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region MessageOf Tests

    [Fact]
    public void MessageOf_WithRound_IsSha256OfBigEndianRound()
    {
        var expected = SHA256.HashData([0, 0, 0, 0, 0, 0, 0x03, 0xE8]);

        var actual = new byte[32];
        DrandRound.MessageOf(1000, actual);

        Assert.Equal(expected, actual);
    }

    #endregion
}
