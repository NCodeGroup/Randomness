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

        var pulse = TwineSignedPulse.Verify(
            TwineVerificationTests.Document(CurbyFixtures.Pulse(1620423)),
            chain
        );

        Assert.Equal(1620423, pulse.Index);
        Assert.Equal(chain.Cid, pulse.Chain);
        Assert.Equal(5, pulse.Links.Length);
        Assert.Equal(CurbyFixtures.CidOf(CurbyFixtures.Pulse(1620422)), pulse.Previous?.ToString());
        Assert.Equal(5, pulse.Mixins.Count);
        Assert.Equal(QuantumPulseId, pulse.MixinOf(quantum)?.ToString());
        Assert.Null(pulse.MixinOf(chain.Cid));
        Assert.Equal(64, pulse.Payload.Get<IpldBytes>("salt").Value.Length);
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
        var content = builder.Content(
            1,
            null,
            Ipld.Map(),
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
        var sealedPulse = builder.Seal(builder.Content(-1, null, Ipld.Map()));

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
        var first = TwineSignedPulse.Verify(
            TwineVerificationTests.Document(CurbyFixtures.Pulse(1620422)),
            chain
        );
        var second = TwineSignedPulse.Verify(
            TwineVerificationTests.Document(CurbyFixtures.Pulse(1620423)),
            chain
        );

        second.VerifyPrecededBy(first);

        Assert.Equal(first.Cid, second.Previous);
    }

    [Fact]
    public void VerifyPrecededBy_WithAPulseTwoRoundsEarlier_Throws()
    {
        using var chain = VerifyCurbyChain();
        var earlier = TwineSignedPulse.Verify(
            TwineVerificationTests.Document(CurbyFixtures.Pulse(1620421)),
            chain
        );
        var pulse = TwineSignedPulse.Verify(
            TwineVerificationTests.Document(CurbyFixtures.Pulse(1620423)),
            chain
        );

        var exception = Assert.Throws<TwineFormatException>(() => pulse.VerifyPrecededBy(earlier));

        Assert.Contains("does not precede", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private static TwineChain VerifyCurbyChain() =>
        TwineChain.Verify(
            TwineVerificationTests.Document(CurbyFixtures.Chain),
            Cid.Parse(CurbyFixtures.ChainId),
            "rng"
        );

    private static TwineChain VerifyBuilderChain(TwineBuilder builder) =>
        TwineChain.Verify(
            TwineVerificationTests.Document(builder.ChainJson),
            builder.ChainCid,
            "rng"
        );
}
