// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class TwineSignedPulseTests
{
    private const string QuantumPulseId =
        "bafyriqeidk4qclt3i7urnujipqmio3oyrvu4exguefe2nhn2jcpsvmaeh7va3gyue2xmsjqu6i6quajljrnzupib7672sgyx57ae2vsklujcm";

    private static DateTimeOffset Start { get; } = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);

    private static TwineChain VerifyBuilderChain(TwineBuilder builder)
    {
        var document = TwineVerificationTests.Document(builder.ChainJson);
        return TwineChain.Verify(document, builder.ChainCid, "rng");
    }

    #region Verify Tests

    [Fact]
    public void Verify_WithCurbyPulse_ReadsLinksMixinsAndPayload()
    {
        using var chain = TwineVerificationTests.VerifyCurbyChain();
        var quantum = Cid.Parse(CurbyQuantumFixtures.ChainId);
        var pulseJson = CurbyFixtures.Pulse(1620423);
        var document = TwineVerificationTests.Document(pulseJson);

        var pulse = TwineSignedPulse.Verify(document, chain);
        var previousJson = CurbyFixtures.Pulse(1620422);
        var previousCid = CurbyFixtures.CidOf(previousJson);
        var previous = pulse.Previous?.ToString();
        var quantumMixin = pulse.MixinOf(quantum)?.ToString();
        var chainMixin = pulse.MixinOf(chain.Cid);
        var salt = pulse.Payload.Get<IpldBytes>("salt");

        Assert.Equal(1620423, pulse.Index);
        Assert.Equal(chain.Cid, pulse.Chain);
        Assert.Equal(5, pulse.Links.Length);
        Assert.Equal(previousCid, previous);
        Assert.Equal(5, pulse.Mixins.Count);
        Assert.Equal(QuantumPulseId, quantumMixin);
        Assert.Null(chainMixin);
        Assert.Equal(64, salt.Value.Length);
    }

    [Fact]
    public void Verify_WithPulseOfAnotherChain_Throws()
    {
        using var curby = TwineVerificationTests.VerifyCurbyChain();
        using var builder = new TwineBuilder();
        var pulse = builder.Pulses(1, Start)[0];
        var document = TwineVerificationTests.Document(pulse.Json);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineSignedPulse.Verify(document, curby)
        );

        Assert.Contains("belongs to chain", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithOneChainMixedInTwice_Throws()
    {
        using var builder = new TwineBuilder();
        using var chain = VerifyBuilderChain(builder);
        var first = builder.Pulses(1, Start)[0];
        var payload = Ipld.Map();
        var content = builder.Content(
            1,
            null,
            payload,
            (builder.ChainCid, first.Cid),
            (builder.ChainCid, builder.ChainCid)
        );
        var sealedPulse = builder.Seal(content);
        var document = TwineVerificationTests.Document(sealedPulse.Json);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineSignedPulse.Verify(document, chain)
        );

        Assert.Contains("twice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_WithNegativeIndex_Throws()
    {
        using var builder = new TwineBuilder();
        using var chain = VerifyBuilderChain(builder);
        var payload = Ipld.Map();
        var content = builder.Content(-1, null, payload);
        var sealedPulse = builder.Seal(content);
        var document = TwineVerificationTests.Document(sealedPulse.Json);

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineSignedPulse.Verify(document, chain)
        );

        Assert.Contains("negative", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region VerifyPrecededBy Tests

    [Fact]
    public void VerifyPrecededBy_WithConsecutiveCurbyPulses_Succeeds()
    {
        using var chain = TwineVerificationTests.VerifyCurbyChain();
        var firstJson = CurbyFixtures.Pulse(1620422);
        var firstDocument = TwineVerificationTests.Document(firstJson);
        var secondJson = CurbyFixtures.Pulse(1620423);
        var secondDocument = TwineVerificationTests.Document(secondJson);
        var first = TwineSignedPulse.Verify(firstDocument, chain);
        var second = TwineSignedPulse.Verify(secondDocument, chain);

        second.VerifyPrecededBy(first);

        Assert.Equal(first.Cid, second.Previous);
    }

    [Fact]
    public void VerifyPrecededBy_WithAPulseTwoRoundsEarlier_Throws()
    {
        using var chain = TwineVerificationTests.VerifyCurbyChain();
        var earlierJson = CurbyFixtures.Pulse(1620421);
        var earlierDocument = TwineVerificationTests.Document(earlierJson);
        var pulseJson = CurbyFixtures.Pulse(1620423);
        var pulseDocument = TwineVerificationTests.Document(pulseJson);
        var earlier = TwineSignedPulse.Verify(earlierDocument, chain);
        var pulse = TwineSignedPulse.Verify(pulseDocument, chain);

        var exception = Assert.Throws<TwineFormatException>(() => pulse.VerifyPrecededBy(earlier));

        Assert.Contains("does not precede", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
