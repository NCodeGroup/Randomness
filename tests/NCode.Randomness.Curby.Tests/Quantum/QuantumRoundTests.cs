// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using NCode.Randomness.Curby.Quantum;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Tests.Twine;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Quantum;

public class QuantumRoundTests
{
    #region Verify Tests

    [Fact]
    public void Verify_WithCurbyQRound_ReadsEachField()
    {
        using var chain = TwineChain.Verify(
            TwineVerificationTests.Document(CurbyQuantumFixtures.Chain),
            Cid.Parse(CurbyQuantumFixtures.ChainId),
            "diqrng"
        );

        var round = QuantumRound.Verify(Documents(CurbyQuantumFixtures.RoundJson), chain);

        Assert.Equal(CurbyQuantumFixtures.Round, round.Number);
        Assert.Null(round.ErrorReason);
        var result = Assert.IsType<QuantumResult>(round.Result);
        Assert.Equal(CurbyQuantumFixtures.Round, result.Round);
        Assert.Equal(CurbyQuantumFixtures.RequestId, result.Request.Cid.ToString());
        Assert.Equal(CurbyQuantumFixtures.PrecommitId, result.Precommit.Cid.ToString());
        Assert.Equal(CurbyQuantumFixtures.ResultId, result.ResultPulse.Cid.ToString());
        Assert.Equal(CurbyQuantumFixtures.Randomness, Convert.ToHexStringLower(result.Randomness));
        Assert.Equal(CurbyQuantumFixtures.Timestamp, result.Timestamp);
        Assert.Equal(CurbyQuantumFixtures.SeedChainId, result.SeedChain.ToString());
        Assert.Equal(249224, result.SeedLength);
        Assert.Equal(1, result.SeedCount);
    }

    [Fact]
    public void Verify_WithBuiltRound_ReadsItsResult()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        using var chain = VerifyChain(builder);

        var round = QuantumRound.Verify(Documents(built.RoundJson), chain);

        Assert.Equal(7, round.Number);
        var result = Assert.IsType<QuantumResult>(round.Result);
        Assert.Equal(built.Result.Cid, result.ResultPulse.Cid);
        Assert.Equal(builder.DataHash, result.DataHash);
        Assert.Equal(builder.Seed.ChainCid, result.SeedChain);
        Assert.Equal(2048, result.SeedLength);
        Assert.Equal(1, result.SeedCount);
        Assert.Equal(built.Randomness, result.Randomness);
        Assert.Equal(128, result.ParamsHash.Length);
    }

    [Fact]
    public void Verify_WithErrorRound_ReadsTheReason()
    {
        using var builder = new QuantumRoundBuilder();
        var json = builder.ErrorRoundJson("the Bell test failed");
        using var chain = VerifyChain(builder);

        var round = QuantumRound.Verify(Documents(json), chain);

        Assert.Equal(7, round.Number);
        Assert.Equal("the Bell test failed", round.ErrorReason);
        Assert.Null(round.Result);
    }

    [Fact]
    public void Verify_WithRequestAndPrecommitOnly_HasNoResult()
    {
        using var builder = new QuantumRoundBuilder();
        var documents = Documents(builder.Build().RoundJson);
        using var chain = VerifyChain(builder);

        var round = QuantumRound.Verify(documents[..2], chain);

        Assert.Equal(7, round.Number);
        Assert.Null(round.ErrorReason);
        Assert.Null(round.Result);
    }

    [Fact]
    public void Verify_WithRequestOnly_HasNoResult()
    {
        using var builder = new QuantumRoundBuilder();
        var documents = Documents(builder.Build().RoundJson);
        using var chain = VerifyChain(builder);

        var round = QuantumRound.Verify(documents[..1], chain);

        Assert.Null(round.ErrorReason);
        Assert.Null(round.Result);
    }

    [Fact]
    public void Verify_WithNoPulses_Throws()
    {
        using var builder = new QuantumRoundBuilder();
        using var chain = VerifyChain(builder);

        var exception = Assert.Throws<TwineFormatException>(() => QuantumRound.Verify([], chain));

        Assert.Contains("1 to 3 pulses, not 0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithFourPulses_Throws()
    {
        using var builder = new QuantumRoundBuilder();
        var documents = Documents(builder.Build().RoundJson);
        using var chain = VerifyChain(builder);
        IReadOnlyList<TwineDocument> four = [.. documents, documents[2]];

        var exception = Assert.Throws<TwineFormatException>(() => QuantumRound.Verify(four, chain));

        Assert.Contains("1 to 3 pulses, not 4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithPulsesThatSkipOne_Throws()
    {
        using var builder = new QuantumRoundBuilder();
        var documents = Documents(builder.Build().RoundJson);
        using var chain = VerifyChain(builder);
        IReadOnlyList<TwineDocument> skipping = [documents[0], documents[2]];

        var exception = Assert.Throws<TwineFormatException>(() =>
            QuantumRound.Verify(skipping, chain)
        );

        Assert.Contains("does not precede", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithPulsesOfTwoRounds_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = static payload => payload.With("round", Ipld.Integer(8)),
        };

        AssertRefused(builder, "The pulses belong to rounds 7 and 8.");
    }

    [Fact]
    public void Verify_WithNegativeRound_Throws()
    {
        using var builder = new QuantumRoundBuilder { Round = -1 };

        AssertRefused(builder, "The round number -1 is negative.");
    }

    [Fact]
    public void Verify_WithoutARequest_Throws()
    {
        using var builder = new QuantumRoundBuilder();
        var documents = Documents(builder.Build().RoundJson);
        using var chain = VerifyChain(builder);
        IReadOnlyList<TwineDocument> withoutRequest = [documents[1], documents[2]];

        var exception = Assert.Throws<TwineFormatException>(() =>
            QuantumRound.Verify(withoutRequest, chain)
        );

        Assert.Contains(
            "The round's stages are precommit, randomness, which CURBy-Q does not publish.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Verify_WithRandomnessBeforeThePrecommit_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = static payload => payload.With("stage", Ipld.Text("randomness")),
        };

        AssertRefused(
            builder,
            "The round's stages are request, randomness, randomness, which CURBy-Q does not publish."
        );
    }

    [Fact]
    public void Verify_WithAnErrorBeforeTheLastPulse_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = static payload => payload.With("stage", Ipld.Text("error")),
        };

        AssertRefused(
            builder,
            "The round's stages are request, error, randomness, which CURBy-Q does not publish."
        );
    }

    [Fact]
    public void Verify_WithAThirdStageOfRequest_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditResult = static payload => payload.With("stage", Ipld.Text("request")),
        };

        AssertRefused(
            builder,
            "The round's stages are request, precommit, request, which CURBy-Q does not publish."
        );
    }

    [Fact]
    public void Verify_WithUppercasePrecommitDataHash_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = static payload =>
                payload.With("dataHash", Ipld.Text(new string('A', 128))),
        };

        AssertRefused(
            builder,
            "The precommit dataHash is not 128 lowercase hexadecimal characters."
        );
    }

    [Fact]
    public void Verify_WithShortResultDataHash_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditResult = static payload => payload.With("dataHash", Ipld.Text("abc")),
        };

        AssertRefused(
            builder,
            "The randomness pulse dataHash is not 128 lowercase hexadecimal characters."
        );
    }

    [Fact]
    public void Verify_WithShortParamsHash_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditResult = static payload => payload.With("paramsHash", Ipld.Text("abc")),
        };

        AssertRefused(
            builder,
            "The randomness pulse paramsHash is not 128 lowercase hexadecimal characters."
        );
    }

    [Fact]
    public void Verify_WithAnotherResultDataHash_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditResult = static payload =>
                payload.With("dataHash", Ipld.Text(new string('a', 128))),
        };

        AssertRefused(builder, "carries another dataHash than the precommit");
    }

    [Fact]
    public void Verify_WithAnotherSeedAlgorithm_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = static payload => payload.With("seedAlgorithm", Ipld.Text("sha256")),
        };

        AssertRefused(builder, "The seed algorithm 'sha256' is not shake256.");
    }

    [Fact]
    public void Verify_WithRuleThatDoesNotCountAfterSelf_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = static payload =>
                payload.With(
                    "rule",
                    Ipld.Map(("after", Ipld.Text("other")), ("count", Ipld.Integer(1)))
                ),
        };

        AssertRefused(builder, "The seed rule counts after 'other', not 'self'.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Verify_WithRuleCountOutOfRange_Throws(int count)
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = payload =>
                payload.With(
                    "rule",
                    Ipld.Map(("after", Ipld.Text("self")), ("count", Ipld.Integer(count)))
                ),
        };

        AssertRefused(builder, $"The seed rule count {count} is not between 1 and 8.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8)]
    [InlineData(12)]
    [InlineData(8388616)]
    public void Verify_WithSeedLengthOutOfRange_Throws(int seedLength)
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = payload => payload.With("seedLength", Ipld.Integer(seedLength)),
        };

        AssertRefused(builder, $"The seed length {seedLength} is not a multiple of 8 bits");
    }

    [Theory]
    [InlineData(128)]
    [InlineData(260)]
    public void Verify_WithOutputLengthOutOfRange_Throws(int outputBits)
    {
        using var builder = new QuantumRoundBuilder
        {
            EditRequest = payload =>
                payload.With("parameters", Ipld.Map(("nBitsOut", Ipld.Integer(outputBits)))),
        };

        AssertRefused(builder, $"The output length {outputBits} is not a multiple of 8 bits");
    }

    [Fact]
    public void Verify_WithRandomnessOfAnotherLength_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditRequest = static payload =>
                payload.With("parameters", Ipld.Map(("nBitsOut", Ipld.Integer(256)))),
        };

        AssertRefused(builder, "The randomness has 64 bytes, not 32.");
    }

    [Fact]
    public void Verify_WithMalformedTimestamp_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditResult = static payload => payload.With("timestamp", Ipld.Text("yesterday")),
        };

        AssertRefused(builder, "'yesterday' is not an ISO 8601 UTC time");
    }

    #endregion

    #region IsBellResponse Tests

    [Fact]
    public void IsBellResponse_WithCurbyQResponse_ReturnsTrue()
    {
        var result = FixtureResult();
        using var bell = FixtureBellChain();
        var response = TwineSignedPulse.Verify(
            TwineVerificationTests.Document(CurbyQuantumFixtures.BellPulse(59313)),
            bell
        );

        Assert.True(result.IsBellResponse(response));
    }

    [Fact]
    public void IsBellResponse_WithStatusPulse_ReturnsFalse()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var result = BuiltResult(builder, built);
        var status = BuiltBellPulse(builder, built, 1);

        Assert.False(result.IsBellResponse(status));
    }

    [Fact]
    public void IsBellResponse_WithResponseToAnotherRequest_ReturnsFalse()
    {
        using var builder = new QuantumRoundBuilder();
        var result = BuiltResult(builder, builder.Build());
        using var bell = FixtureBellChain();
        var response = TwineSignedPulse.Verify(
            TwineVerificationTests.Document(CurbyQuantumFixtures.BellPulse(59313)),
            bell
        );

        Assert.False(result.IsBellResponse(response));
    }

    [Fact]
    public void IsBellResponse_WithAnotherDataHash_Throws()
    {
        using var builder = new QuantumRoundBuilder { BellResponseDataHash = new string('b', 128) };
        var built = builder.Build();
        var result = BuiltResult(builder, built);
        var response = BuiltBellPulse(builder, built, 0);

        var exception = Assert.Throws<TwineFormatException>(() => result.IsBellResponse(response));

        Assert.Contains(
            "carries another data hash than the precommit",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region MixesPrecommit Tests

    [Fact]
    public void MixesPrecommit_WithCurbyQSeedPulses_FindsTheMixingOnes()
    {
        var result = FixtureResult();
        using var seedChain = FixtureSeedChain();

        Assert.True(result.MixesPrecommit(FixtureSeedPulse(seedChain, 1914068)));
        Assert.True(result.MixesPrecommit(FixtureSeedPulse(seedChain, 1914067)));
        Assert.False(result.MixesPrecommit(FixtureSeedPulse(seedChain, 1914066)));
    }

    #endregion

    #region SelectSeed Tests

    [Fact]
    public void SelectSeed_WithCurbyQSeedPulses_ReturnsThePulseAfterTheFirst()
    {
        var result = FixtureResult();
        using var seedChain = FixtureSeedChain();
        var newest = FixtureSeedPulse(seedChain, 1914068);
        var first = FixtureSeedPulse(seedChain, 1914067);

        var seed = result.SelectSeed([newest, first]);

        Assert.Equal(1914068, seed.Index);
        Assert.Equal(CurbyQuantumFixtures.SeedPulseId, seed.Cid.ToString());
    }

    [Fact]
    public void SelectSeed_WithSeedCountOfTwo_ReturnsThePulseTwoAfterTheFirst()
    {
        using var builder = new QuantumRoundBuilder { SeedCount = 2 };
        var built = builder.Build();
        var result = BuiltResult(builder, built);
        var newest = BuiltSeedPulse(builder, built, 3);
        var middle = BuiltSeedPulse(builder, built, 2);
        var first = BuiltSeedPulse(builder, built, 1);

        var seed = result.SelectSeed([newest, middle, first]);

        Assert.Equal(newest.Cid, seed.Cid);
    }

    [Fact]
    public void SelectSeed_WithTooFewPulses_Throws()
    {
        var result = FixtureResult();
        using var seedChain = FixtureSeedChain();
        var first = FixtureSeedPulse(seedChain, 1914067);

        var exception = Assert.Throws<TwineFormatException>(() => result.SelectSeed([first]));

        Assert.Contains(
            "Only 1 seed pulse(s) mix in the precommit",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region VerifySeed Tests

    [Fact]
    public void VerifySeed_WithCurbyQParameters_Succeeds()
    {
        var result = FixtureResult();
        using var seedChain = FixtureSeedChain();
        var seed = FixtureSeedPulse(seedChain, 1914068);

        result.VerifySeed(seed, Encoding.UTF8.GetBytes(CurbyQuantumFixtures.Parameters));
    }

    [Fact]
    public void VerifySeed_FromTheFirstMixingPulse_Throws()
    {
        var result = FixtureResult();
        using var seedChain = FixtureSeedChain();
        var first = FixtureSeedPulse(seedChain, 1914067);
        var parameters = Encoding.UTF8.GetBytes(CurbyQuantumFixtures.Parameters);

        var exception = Assert.Throws<TwineFormatException>(() =>
            result.VerifySeed(first, parameters)
        );

        Assert.Contains("does not expand", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifySeed_WithTamperedParameters_Throws()
    {
        var result = FixtureResult();
        using var seedChain = FixtureSeedChain();
        var seed = FixtureSeedPulse(seedChain, 1914068);
        var parameters = Encoding.UTF8.GetBytes(CurbyQuantumFixtures.Parameters + " ");

        var exception = Assert.Throws<TwineFormatException>(() =>
            result.VerifySeed(seed, parameters)
        );

        Assert.Contains(
            "do not hash to its paramsHash",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void VerifySeed_WithBuiltRound_Succeeds()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var result = BuiltResult(builder, built);
        var seed = BuiltSeedPulse(builder, built, built.SeedPulses.Count - 1);

        result.VerifySeed(seed, Encoding.UTF8.GetBytes(built.Parameters));
    }

    [Fact]
    public void VerifySeed_WithDuplicateSeedProperty_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            Parameters = static seed =>
                $$"""{"seed":"{{Convert.ToBase64String(seed)}}","seed":"{{Convert.ToBase64String(seed)}}"}""",
        };

        AssertSeedRefused(builder, "have 'seed' twice");
    }

    [Fact]
    public void VerifySeed_WithoutSeedProperty_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            Parameters = static _ => """{"nBitsOut":512}""",
        };

        AssertSeedRefused(builder, "have no 'seed'");
    }

    [Fact]
    public void VerifySeed_WithSeedThatIsNotAString_Throws()
    {
        using var builder = new QuantumRoundBuilder { Parameters = static _ => """{"seed":1}""" };

        AssertSeedRefused(builder, "'seed' is not a string");
    }

    [Fact]
    public void VerifySeed_WithUnpaddedBase64_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            Parameters = static seed =>
                $$"""{"seed":"{{Convert.ToBase64String(seed).TrimEnd('=')}}"}""",
        };

        AssertSeedRefused(builder, "not canonical base64");
    }

    #endregion

    private static void AssertSeedRefused(QuantumRoundBuilder builder, string expected)
    {
        var built = builder.Build();
        var result = BuiltResult(builder, built);
        var seed = BuiltSeedPulse(builder, built, built.SeedPulses.Count - 1);
        var parameters = Encoding.UTF8.GetBytes(built.Parameters);

        var exception = Assert.Throws<TwineFormatException>(() =>
            result.VerifySeed(seed, parameters)
        );

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    private static TwineChain FixtureSeedChain() =>
        TwineChain.Verify(
            TwineVerificationTests.Document(CurbyQuantumFixtures.SeedChain),
            Cid.Parse(CurbyQuantumFixtures.SeedChainId),
            null
        );

    private static TwineSignedPulse FixtureSeedPulse(TwineChain seedChain, long index) =>
        TwineSignedPulse.Verify(
            TwineVerificationTests.Document(CurbyQuantumFixtures.SeedPulse(index)),
            seedChain
        );

    private static TwineSignedPulse BuiltSeedPulse(
        QuantumRoundBuilder builder,
        BuiltQuantumRound built,
        int index
    )
    {
        using var chain = TwineChain.Verify(
            TwineVerificationTests.Document(builder.Seed.ChainJson),
            builder.Seed.ChainCid,
            null
        );
        return TwineSignedPulse.Verify(
            TwineVerificationTests.Document(built.SeedPulses[index].Json),
            chain
        );
    }

    private static QuantumResult FixtureResult()
    {
        using var chain = TwineChain.Verify(
            TwineVerificationTests.Document(CurbyQuantumFixtures.Chain),
            Cid.Parse(CurbyQuantumFixtures.ChainId),
            "diqrng"
        );
        var round = QuantumRound.Verify(Documents(CurbyQuantumFixtures.RoundJson), chain);
        return Assert.IsType<QuantumResult>(round.Result);
    }

    private static TwineChain FixtureBellChain() =>
        TwineChain.Verify(
            TwineVerificationTests.Document(CurbyQuantumFixtures.BellChain),
            Cid.Parse(CurbyQuantumFixtures.BellChainId),
            "bell"
        );

    private static QuantumResult BuiltResult(QuantumRoundBuilder builder, BuiltQuantumRound built)
    {
        using var chain = VerifyChain(builder);
        var round = QuantumRound.Verify(Documents(built.RoundJson), chain);
        return Assert.IsType<QuantumResult>(round.Result);
    }

    private static TwineSignedPulse BuiltBellPulse(
        QuantumRoundBuilder builder,
        BuiltQuantumRound built,
        int index
    )
    {
        using var chain = TwineChain.Verify(
            TwineVerificationTests.Document(builder.Bell.ChainJson),
            builder.Bell.ChainCid,
            "bell"
        );
        return TwineSignedPulse.Verify(
            TwineVerificationTests.Document(built.BellPulses[index].Json),
            chain
        );
    }

    private static void AssertRefused(QuantumRoundBuilder builder, string expected)
    {
        var documents = Documents(builder.Build().RoundJson);
        using var chain = VerifyChain(builder);

        var exception = Assert.Throws<TwineFormatException>(() =>
            QuantumRound.Verify(documents, chain)
        );

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    private static List<TwineDocument> Documents(string json) =>
        [.. JsonElement.Parse(json).EnumerateArray().Select(TwineDocument.Parse)];

    private static TwineChain VerifyChain(QuantumRoundBuilder builder) =>
        TwineChain.Verify(
            TwineVerificationTests.Document(builder.Quantum.ChainJson),
            builder.Quantum.ChainCid,
            "diqrng"
        );
}
