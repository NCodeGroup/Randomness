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

    private static TwineChain FixtureSeedChain()
    {
        var document = TwineVerificationTests.Document(CurbyQuantumFixtures.SeedChain);
        var chainCid = Cid.Parse(CurbyQuantumFixtures.SeedChainId);
        return TwineChain.Verify(document, chainCid, null);
    }

    private static TwineSignedPulse FixtureSeedPulse(TwineChain seedChain, long index)
    {
        var json = CurbyQuantumFixtures.SeedPulse(index);
        var document = TwineVerificationTests.Document(json);
        return TwineSignedPulse.Verify(document, seedChain);
    }

    private static TwineSignedPulse BuiltSeedPulse(
        QuantumRoundBuilder builder,
        BuiltQuantumRound built,
        int index
    )
    {
        var chainDocument = TwineVerificationTests.Document(builder.Seed.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.Seed.ChainCid, null);
        var pulseDocument = TwineVerificationTests.Document(built.SeedPulses[index].Json);
        return TwineSignedPulse.Verify(pulseDocument, chain);
    }

    private static QuantumResult FixtureResult()
    {
        var chainDocument = TwineVerificationTests.Document(CurbyQuantumFixtures.Chain);
        var chainCid = Cid.Parse(CurbyQuantumFixtures.ChainId);
        using var chain = TwineChain.Verify(chainDocument, chainCid, "diqrng");
        var documents = Documents(CurbyQuantumFixtures.RoundJson);
        var round = QuantumRound.Verify(documents, chain);
        return Assert.IsType<QuantumResult>(round.Result);
    }

    private static TwineChain FixtureBellChain()
    {
        var document = TwineVerificationTests.Document(CurbyQuantumFixtures.BellChain);
        var chainCid = Cid.Parse(CurbyQuantumFixtures.BellChainId);
        return TwineChain.Verify(document, chainCid, "bell");
    }

    private static QuantumResult BuiltResult(QuantumRoundBuilder builder, BuiltQuantumRound built)
    {
        using var chain = VerifyChain(builder);
        var documents = Documents(built.RoundJson);
        var round = QuantumRound.Verify(documents, chain);
        return Assert.IsType<QuantumResult>(round.Result);
    }

    private static TwineSignedPulse BuiltBellPulse(
        QuantumRoundBuilder builder,
        BuiltQuantumRound built,
        int index
    )
    {
        var chainDocument = TwineVerificationTests.Document(builder.Bell.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.Bell.ChainCid, "bell");
        var pulseDocument = TwineVerificationTests.Document(built.BellPulses[index].Json);
        return TwineSignedPulse.Verify(pulseDocument, chain);
    }

    private static void AssertRefused(QuantumRoundBuilder builder, string expected)
    {
        var built = builder.Build();
        var documents = Documents(built.RoundJson);
        using var chain = VerifyChain(builder);

        var exception = Assert.Throws<TwineFormatException>(() =>
            QuantumRound.Verify(documents, chain)
        );

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    private static List<TwineDocument> Documents(string json) =>
        [.. JsonElement.Parse(json).EnumerateArray().Select(TwineDocument.Parse)];

    private static TwineChain VerifyChain(QuantumRoundBuilder builder)
    {
        var document = TwineVerificationTests.Document(builder.Quantum.ChainJson);
        return TwineChain.Verify(document, builder.Quantum.ChainCid, "diqrng");
    }

    #region Verify Tests

    [Fact]
    public void Verify_WithCurbyQRound_ReadsEachField()
    {
        var document = TwineVerificationTests.Document(CurbyQuantumFixtures.Chain);
        var chainCid = Cid.Parse(CurbyQuantumFixtures.ChainId);
        using var chain = TwineChain.Verify(document, chainCid, "diqrng");
        var documents = Documents(CurbyQuantumFixtures.RoundJson);

        var round = QuantumRound.Verify(documents, chain);
        var result = Assert.IsType<QuantumResult>(round.Result);
        var requestId = result.Request.Cid.ToString();
        var precommitId = result.Precommit.Cid.ToString();
        var resultId = result.ResultPulse.Cid.ToString();
        var randomness = Convert.ToHexStringLower(result.Randomness);
        var seedChainId = result.SeedChain.ToString();

        Assert.Equal(CurbyQuantumFixtures.Round, round.Number);
        Assert.Null(round.ErrorReason);
        Assert.Equal(CurbyQuantumFixtures.RequestId, requestId);
        Assert.Equal(CurbyQuantumFixtures.PrecommitId, precommitId);
        Assert.Equal(CurbyQuantumFixtures.ResultId, resultId);
        Assert.Equal(CurbyQuantumFixtures.Randomness, randomness);
        Assert.Equal(CurbyQuantumFixtures.Timestamp, result.Timestamp);
        Assert.Equal(CurbyQuantumFixtures.SeedChainId, seedChainId);
        Assert.Equal(249224, result.SeedLength);
        Assert.Equal(1, result.SeedCount);
    }

    [Fact]
    public void Verify_WithBuiltRound_ReadsItsResult()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        using var chain = VerifyChain(builder);
        var documents = Documents(built.RoundJson);

        var round = QuantumRound.Verify(documents, chain);

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
        var documents = Documents(json);

        var round = QuantumRound.Verify(documents, chain);

        Assert.Equal(7, round.Number);
        Assert.Equal("the Bell test failed", round.ErrorReason);
        Assert.Null(round.Result);
    }

    [Fact]
    public void Verify_WithRequestAndPrecommitOnly_HasNoResult()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var documents = Documents(built.RoundJson);
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
        var built = builder.Build();
        var documents = Documents(built.RoundJson);
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
        var built = builder.Build();
        var documents = Documents(built.RoundJson);
        using var chain = VerifyChain(builder);
        IReadOnlyList<TwineDocument> four = [.. documents, documents[2]];

        var exception = Assert.Throws<TwineFormatException>(() => QuantumRound.Verify(four, chain));

        Assert.Contains("1 to 3 pulses, not 4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithPulsesThatSkipOne_Throws()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var documents = Documents(built.RoundJson);
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
            EditPrecommit = static payload =>
            {
                var round = Ipld.Integer(8);
                return payload.With("round", round);
            },
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
        var built = builder.Build();
        var documents = Documents(built.RoundJson);
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
            EditPrecommit = static payload =>
            {
                var stage = Ipld.Text("randomness");
                return payload.With("stage", stage);
            },
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
            EditPrecommit = static payload =>
            {
                var stage = Ipld.Text("error");
                return payload.With("stage", stage);
            },
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
            EditResult = static payload =>
            {
                var stage = Ipld.Text("request");
                return payload.With("stage", stage);
            },
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
            {
                var uppercase = new string('A', 128);
                var dataHash = Ipld.Text(uppercase);
                return payload.With("dataHash", dataHash);
            },
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
            EditResult = static payload =>
            {
                var dataHash = Ipld.Text("abc");
                return payload.With("dataHash", dataHash);
            },
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
            EditResult = static payload =>
            {
                var paramsHash = Ipld.Text("abc");
                return payload.With("paramsHash", paramsHash);
            },
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
            {
                var lowercase = new string('a', 128);
                var dataHash = Ipld.Text(lowercase);
                return payload.With("dataHash", dataHash);
            },
        };

        AssertRefused(builder, "carries another dataHash than the precommit");
    }

    [Fact]
    public void Verify_WithAnotherSeedAlgorithm_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = static payload =>
            {
                var algorithm = Ipld.Text("sha256");
                return payload.With("seedAlgorithm", algorithm);
            },
        };

        AssertRefused(builder, "The seed algorithm 'sha256' is not shake256.");
    }

    [Fact]
    public void Verify_WithRuleThatDoesNotCountAfterSelf_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditPrecommit = static payload =>
            {
                var after = Ipld.Text("other");
                var count = Ipld.Integer(1);
                var rule = Ipld.Map(("after", after), ("count", count));
                return payload.With("rule", rule);
            },
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
            {
                var after = Ipld.Text("self");
                var ruleCount = Ipld.Integer(count);
                var rule = Ipld.Map(("after", after), ("count", ruleCount));
                return payload.With("rule", rule);
            },
        };

        AssertRefused(builder, $"The seed rule count {count} is not between 1 and 8.");
    }

    [Fact]
    public void Verify_WithSeedCountOfEight_ReadsIt()
    {
        using var builder = new QuantumRoundBuilder { SeedCount = 8 };
        var built = builder.Build();
        using var chain = VerifyChain(builder);
        var documents = Documents(built.RoundJson);

        var round = QuantumRound.Verify(documents, chain);

        var result = Assert.IsType<QuantumResult>(round.Result);
        Assert.Equal(8, result.SeedCount);
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
            EditPrecommit = payload =>
            {
                var length = Ipld.Integer(seedLength);
                return payload.With("seedLength", length);
            },
        };

        AssertRefused(builder, $"The seed length {seedLength} is not a multiple of 8 bits");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(8388608)]
    public void Verify_WithSeedLengthAtItsBounds_ReadsIt(int seedLength)
    {
        using var builder = new QuantumRoundBuilder { SeedLength = seedLength };
        var built = builder.Build();
        using var chain = VerifyChain(builder);
        var documents = Documents(built.RoundJson);

        var round = QuantumRound.Verify(documents, chain);

        var result = Assert.IsType<QuantumResult>(round.Result);
        Assert.Equal(seedLength, result.SeedLength);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(248)]
    [InlineData(260)]
    public void Verify_WithOutputLengthOutOfRange_Throws(int outputBits)
    {
        using var builder = new QuantumRoundBuilder
        {
            EditRequest = payload =>
            {
                var bits = Ipld.Integer(outputBits);
                var parameters = Ipld.Map(("nBitsOut", bits));
                return payload.With("parameters", parameters);
            },
        };

        AssertRefused(
            builder,
            $"The output length {outputBits} is not a multiple of 8 bits of at least 256"
        );
    }

    [Fact]
    public void Verify_WithOutputLengthOf256_ReadsIt()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditRequest = static payload =>
            {
                var bits = Ipld.Integer(256);
                var parameters = Ipld.Map(("nBitsOut", bits));
                return payload.With("parameters", parameters);
            },
            EditResult = static payload =>
            {
                var randomness = Ipld.Bytes(new byte[32]);
                return payload.With("randomness", randomness);
            },
        };
        var built = builder.Build();
        using var chain = VerifyChain(builder);
        var documents = Documents(built.RoundJson);

        var round = QuantumRound.Verify(documents, chain);

        var result = Assert.IsType<QuantumResult>(round.Result);
        Assert.Equal(32, result.Randomness.Length);
    }

    [Fact]
    public void Verify_WithRandomnessOfAnotherLength_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditRequest = static payload =>
            {
                var bits = Ipld.Integer(256);
                var parameters = Ipld.Map(("nBitsOut", bits));
                return payload.With("parameters", parameters);
            },
        };

        AssertRefused(builder, "The randomness has 64 bytes, not 32.");
    }

    [Fact]
    public void Verify_WithMalformedTimestamp_Throws()
    {
        using var builder = new QuantumRoundBuilder
        {
            EditResult = static payload =>
            {
                var timestamp = Ipld.Text("yesterday");
                return payload.With("timestamp", timestamp);
            },
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
        var json = CurbyQuantumFixtures.BellPulse(CurbyQuantumFixtures.BellResponseIndex);
        var document = TwineVerificationTests.Document(json);
        var response = TwineSignedPulse.Verify(document, bell);

        var isResponse = result.IsBellResponse(response);

        Assert.True(isResponse);
    }

    [Fact]
    public void IsBellResponse_WithStatusPulse_ReturnsFalse()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var result = BuiltResult(builder, built);
        var status = BuiltBellPulse(builder, built, 1);

        var isResponse = result.IsBellResponse(status);

        Assert.False(isResponse);
    }

    [Fact]
    public void IsBellResponse_WithResponseToAnotherRequest_ReturnsFalse()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var result = BuiltResult(builder, built);
        using var bell = FixtureBellChain();
        var json = CurbyQuantumFixtures.BellPulse(CurbyQuantumFixtures.BellResponseIndex);
        var document = TwineVerificationTests.Document(json);
        var response = TwineSignedPulse.Verify(document, bell);

        var isResponse = result.IsBellResponse(response);

        Assert.False(isResponse);
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

    [Fact]
    public void IsBellResponse_WithMistypedRef_Throws()
    {
        using var builder = new QuantumRoundBuilder();
        var built = builder.Build();
        var result = BuiltResult(builder, built);
        var dataHash = Ipld.Text(builder.DataHash);
        var requestId = result.Request.Cid.ToString();
        var reference = Ipld.Text(requestId);
        var payload = Ipld.Map(("dataHash", dataHash), ("ref", reference));
        var content = builder.Bell.Content(0, null, payload);
        var sealedPulse = builder.Bell.Seal(content);
        var chainDocument = TwineVerificationTests.Document(builder.Bell.ChainJson);
        using var chain = TwineChain.Verify(chainDocument, builder.Bell.ChainCid, "bell");
        var pulseDocument = TwineVerificationTests.Document(sealedPulse.Json);
        var response = TwineSignedPulse.Verify(pulseDocument, chain);

        var exception = Assert.Throws<TwineFormatException>(() => result.IsBellResponse(response));

        Assert.Contains("'ref' has the wrong type", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region MixesPrecommit Tests

    [Fact]
    public void MixesPrecommit_WithCurbyQSeedPulses_FindsTheMixingOnes()
    {
        var result = FixtureResult();
        using var seedChain = FixtureSeedChain();
        var newest = FixtureSeedPulse(seedChain, CurbyQuantumFixtures.ThirdSeedPulseIndex);
        var first = FixtureSeedPulse(seedChain, CurbyQuantumFixtures.SecondSeedPulseIndex);
        var oldest = FixtureSeedPulse(seedChain, CurbyQuantumFixtures.FirstSeedPulseIndex);

        var mixesNewest = result.MixesPrecommit(newest);
        var mixesFirst = result.MixesPrecommit(first);
        var mixesOldest = result.MixesPrecommit(oldest);

        Assert.True(mixesNewest);
        Assert.True(mixesFirst);
        Assert.False(mixesOldest);
    }

    #endregion

    #region SelectSeed Tests

    [Fact]
    public void SelectSeed_WithCurbyQSeedPulses_ReturnsThePulseAfterTheFirst()
    {
        var result = FixtureResult();
        using var seedChain = FixtureSeedChain();
        var newest = FixtureSeedPulse(seedChain, CurbyQuantumFixtures.ThirdSeedPulseIndex);
        var first = FixtureSeedPulse(seedChain, CurbyQuantumFixtures.SecondSeedPulseIndex);

        var seed = result.SelectSeed([newest, first]);
        var seedId = seed.Cid.ToString();

        Assert.Equal(CurbyQuantumFixtures.ThirdSeedPulseIndex, seed.Index);
        Assert.Equal(CurbyQuantumFixtures.SeedPulseId, seedId);
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
        var first = FixtureSeedPulse(seedChain, CurbyQuantumFixtures.SecondSeedPulseIndex);

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
        var seed = FixtureSeedPulse(seedChain, CurbyQuantumFixtures.ThirdSeedPulseIndex);
        var parameters = Encoding.UTF8.GetBytes(CurbyQuantumFixtures.Parameters);

        result.VerifySeed(seed, parameters);
    }

    [Fact]
    public void VerifySeed_FromTheFirstMixingPulse_Throws()
    {
        var result = FixtureResult();
        using var seedChain = FixtureSeedChain();
        var first = FixtureSeedPulse(seedChain, CurbyQuantumFixtures.SecondSeedPulseIndex);
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
        var seed = FixtureSeedPulse(seedChain, CurbyQuantumFixtures.ThirdSeedPulseIndex);
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
        var parameters = Encoding.UTF8.GetBytes(built.Parameters);

        result.VerifySeed(seed, parameters);
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
}
