// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class TwineSignedPulseTests
{
    private const string QuantumChainId =
        "bafyriqhijw5soalbtppuwwjbskfiriqy6swa5qd5trgbzx4nypnu7mp7iyhx7m2iin2nxnglmngvjgirzb7bvlisxq3uygoik7ozbybxnftiw";
    private const string QuantumPulseId =
        "bafyriqeidk4qclt3i7urnujipqmio3oyrvu4exguefe2nhn2jcpsvmaeh7va3gyue2xmsjqu6i6quajljrnzupib7672sgyx57ae2vsklujcm";

    private static DateTimeOffset Start { get; } = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);

    #region Verify Tests

    [Fact]
    public void Verify_WithCurbyPulse_ReadsLinksMixinsAndPayload()
    {
        using var chain = VerifyCurbyChain();
        var quantum = Cid.Parse(QuantumChainId);
        var pulseJson = CurbyFixtures.Pulse(1620423);
        var document = TwineVerificationTests.Document(pulseJson);

        var pulse = TwineSignedPulse.Verify(document, chain);
        var previousCid = CurbyFixtures.CidOf(CurbyFixtures.Pulse(1620422));
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
        using var curby = VerifyCurbyChain();
        using var builder = new TwineBuilder();
        var pulse = builder.Pulses(1, Start)[0];

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineSignedPulse.Verify(TwineVerificationTests.Document(pulse.Json), curby)
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

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineSignedPulse.Verify(TwineVerificationTests.Document(sealedPulse.Json), chain)
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

        var exception = Assert.Throws<TwineFormatException>(() =>
            TwineSignedPulse.Verify(TwineVerificationTests.Document(sealedPulse.Json), chain)
        );

        Assert.Contains("negative", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region VerifyPrecededBy Tests

    [Fact]
    public void VerifyPrecededBy_WithConsecutiveCurbyPulses_Succeeds()
    {
        using var chain = VerifyCurbyChain();
        var firstDocument = TwineVerificationTests.Document(CurbyFixtures.Pulse(1620422));
        var secondDocument = TwineVerificationTests.Document(CurbyFixtures.Pulse(1620423));
        var first = TwineSignedPulse.Verify(firstDocument, chain);
        var second = TwineSignedPulse.Verify(secondDocument, chain);

        second.VerifyPrecededBy(first);

        Assert.Equal(first.Cid, second.Previous);
    }

    [Fact]
    public void VerifyPrecededBy_WithAPulseTwoRoundsEarlier_Throws()
    {
        using var chain = VerifyCurbyChain();
        var earlierDocument = TwineVerificationTests.Document(CurbyFixtures.Pulse(1620421));
        var pulseDocument = TwineVerificationTests.Document(CurbyFixtures.Pulse(1620423));
        var earlier = TwineSignedPulse.Verify(earlierDocument, chain);
        var pulse = TwineSignedPulse.Verify(pulseDocument, chain);

        var exception = Assert.Throws<TwineFormatException>(() => pulse.VerifyPrecededBy(earlier));

        Assert.Contains("does not precede", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private static TwineChain VerifyCurbyChain()
    {
        var document = TwineVerificationTests.Document(CurbyFixtures.Chain);
        var chainCid = Cid.Parse(CurbyFixtures.ChainId);
        return TwineChain.Verify(document, chainCid, "rng");
    }

    private static TwineChain VerifyBuilderChain(TwineBuilder builder)
    {
        var document = TwineVerificationTests.Document(builder.ChainJson);
        return TwineChain.Verify(document, builder.ChainCid, "rng");
    }
}
