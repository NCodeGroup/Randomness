// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;

namespace NCode.Randomness.Curby.Tests;

public class CurbyQuantumRoundVerifierTests
{
    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    private static void AssertInvalid(BeaconException exception, string fragment)
    {
        Assert.Equal(BeaconError.InvalidPulse, exception.Error);
        Assert.Equal(CurbyDefaults.QuantumBeaconName, exception.Beacon);
        Assert.Contains(fragment, exception.Message, StringComparison.Ordinal);
    }

    private static List<ReadOnlyMemory<byte>> CurbyQDocuments()
    {
        var bellResponse = CurbyQuantumFixtures.BellPulse(CurbyQuantumFixtures.BellResponseIndex);
        var firstSeed = CurbyQuantumFixtures.SeedPulse(CurbyQuantumFixtures.FirstSeedPulseIndex);
        var secondSeed = CurbyQuantumFixtures.SeedPulse(CurbyQuantumFixtures.SecondSeedPulseIndex);
        var thirdSeed = CurbyQuantumFixtures.SeedPulse(CurbyQuantumFixtures.ThirdSeedPulseIndex);

        return
        [
            Utf8(CurbyQuantumFixtures.Chain),
            Utf8(CurbyQuantumFixtures.BellChain),
            Utf8(bellResponse),
            Utf8(CurbyQuantumFixtures.SeedChain),
            Utf8(firstSeed),
            Utf8(secondSeed),
            Utf8(thirdSeed),
        ];
    }

    private static List<ReadOnlyMemory<byte>> BuiltDocuments(
        QuantumRoundBuilder builder,
        BuiltQuantumRound round
    )
    {
        List<ReadOnlyMemory<byte>> documents =
        [
            Utf8(builder.Quantum.ChainJson),
            Utf8(builder.Bell.ChainJson),
            Utf8(builder.Seed.ChainJson),
        ];
        foreach (var (json, _) in round.BellPulses)
        {
            var document = Utf8(json);
            documents.Add(document);
        }

        foreach (var (json, _) in round.SeedPulses)
        {
            var document = Utf8(json);
            documents.Add(document);
        }

        return documents;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithInvalidQuantumChainId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new CurbyQuantumRoundVerifier("not-a-cid", CurbyDefaults.BellChainId)
        );
    }

    [Fact]
    public void Constructor_WithInvalidBellChainId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new CurbyQuantumRoundVerifier(CurbyDefaults.QuantumChainId, "not-a-cid")
        );
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WithCurbyQDocuments_ReturnsTheVerifiedRound()
    {
        var round = Utf8(CurbyQuantumFixtures.RoundJson);
        var parameters = Utf8(CurbyQuantumFixtures.Parameters);
        var documents = CurbyQDocuments();

        var pulse = new CurbyQuantumRoundVerifier().Verify(round, parameters, documents);

        Assert.Equal(CurbyDefaults.QuantumBeaconName, pulse.Beacon);
        Assert.Equal(CurbyQuantumFixtures.ChainId, pulse.Chain);
        Assert.Equal(CurbyQuantumFixtures.Round, pulse.Round);
        Assert.Equal(CurbyQuantumFixtures.ResultId, pulse.Id);
        Assert.Equal(CurbyQuantumFixtures.Timestamp, pulse.Timestamp);
        var randomness = Convert.ToHexStringLower(pulse.Randomness);
        Assert.Equal(CurbyQuantumFixtures.Randomness, randomness);
    }

    [Fact]
    public void Verify_WithBuiltRound_ReturnsItsRandomness()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var quantumChainId = builder.Quantum.ChainCid.ToString();
        var bellChainId = builder.Bell.ChainCid.ToString();
        var verifier = new CurbyQuantumRoundVerifier(quantumChainId, bellChainId);
        var round = Utf8(built.RoundJson);
        var parameters = Utf8(built.Parameters);
        var documents = BuiltDocuments(builder, built);

        var pulse = verifier.Verify(round, parameters, documents);

        var resultId = built.Result.Cid.ToString();
        Assert.Equal(builder.Round, pulse.Round);
        Assert.Equal(quantumChainId, pulse.Chain);
        Assert.Equal(resultId, pulse.Id);
        Assert.Equal(built.Randomness, pulse.Randomness);
    }

    [Fact]
    public void Verify_WhenTheRoundIsNotOnThePinnedChain_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var round = Utf8(built.RoundJson);
        var parameters = Utf8(built.Parameters);
        var documents = BuiltDocuments(builder, built);
        var curbyQChain = Utf8(CurbyQuantumFixtures.Chain);
        documents.Add(curbyQChain);
        var verifier = new CurbyQuantumRoundVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(round, parameters, documents)
        );

        AssertInvalid(exception, "belongs to chain");
    }

    [Fact]
    public void Verify_WhenTheBellChainIsAnotherFamily_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var quantumChainId = builder.Quantum.ChainCid.ToString();
        var seedChainId = builder.Seed.ChainCid.ToString();
        var verifier = new CurbyQuantumRoundVerifier(quantumChainId, seedChainId);
        var round = Utf8(built.RoundJson);
        var parameters = Utf8(built.Parameters);
        var documents = BuiltDocuments(builder, built);

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(round, parameters, documents)
        );

        AssertInvalid(exception, "not twine/1.x/bell/1.x");
    }

    [Fact]
    public void Verify_WhenABellPulseIsMissing_IsInvalid()
    {
        var round = Utf8(CurbyQuantumFixtures.RoundJson);
        var parameters = Utf8(CurbyQuantumFixtures.Parameters);
        var documents = CurbyQDocuments();
        documents.RemoveAt(2);
        var verifier = new CurbyQuantumRoundVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(round, parameters, documents)
        );

        AssertInvalid(exception, "hold no pulse");
    }

    [Fact]
    public void Verify_WhenTheBellChainHasNoResponse_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder { OmitBellResponse = true };
        var built = builder.Build();
        var quantumChainId = builder.Quantum.ChainCid.ToString();
        var bellChainId = builder.Bell.ChainCid.ToString();
        var verifier = new CurbyQuantumRoundVerifier(quantumChainId, bellChainId);
        var round = Utf8(built.RoundJson);
        var parameters = Utf8(built.Parameters);
        var documents = BuiltDocuments(builder, built);

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(round, parameters, documents)
        );

        AssertInvalid(exception, "The Bell chain starts before");
    }

    [Fact]
    public void Verify_WithTamperedRound_IsInvalid()
    {
        var tampered = CurbyQuantumFixtures.RoundJson.Replace(
            "2025-08-22T19:42:38.156Z",
            "2025-08-22T19:42:38.157Z",
            StringComparison.Ordinal
        );
        var round = Utf8(tampered);
        var parameters = Utf8(CurbyQuantumFixtures.Parameters);
        var documents = CurbyQDocuments();
        var verifier = new CurbyQuantumRoundVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(round, parameters, documents)
        );

        AssertInvalid(exception, "hashes to");
    }

    [Fact]
    public void Verify_WithTamperedParameters_IsInvalid()
    {
        var round = Utf8(CurbyQuantumFixtures.RoundJson);
        var parameters = Utf8(CurbyQuantumFixtures.Parameters + " ");
        var documents = CurbyQDocuments();
        var verifier = new CurbyQuantumRoundVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(round, parameters, documents)
        );

        AssertInvalid(exception, "do not hash to its paramsHash");
    }

    [Fact]
    public void Verify_WhenTheRoundIsIncomplete_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var quantumChainId = builder.Quantum.ChainCid.ToString();
        var bellChainId = builder.Bell.ChainCid.ToString();
        var verifier = new CurbyQuantumRoundVerifier(quantumChainId, bellChainId);
        var round = Utf8($"[{built.Request.Json},{built.Precommit.Json}]");
        var parameters = Utf8(built.Parameters);
        var documents = BuiltDocuments(builder, built);

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(round, parameters, documents)
        );

        AssertInvalid(exception, "has not completed");
    }

    [Fact]
    public void Verify_WithMalformedJson_IsInvalid()
    {
        var round = "["u8.ToArray();
        var parameters = Utf8(CurbyQuantumFixtures.Parameters);
        var documents = CurbyQDocuments();
        var verifier = new CurbyQuantumRoundVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(round, parameters, documents)
        );

        AssertInvalid(exception, "not valid JSON");
    }

    #endregion
}
