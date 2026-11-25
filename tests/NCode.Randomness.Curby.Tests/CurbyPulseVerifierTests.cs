// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;

namespace NCode.Randomness.Curby.Tests;

public class CurbyPulseVerifierTests
{
    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    private static void AssertInvalid(BeaconException exception, string fragment)
    {
        Assert.Equal(BeaconError.InvalidPulse, exception.Error);
        Assert.Equal(CurbyDefaults.BeaconName, exception.Beacon);
        Assert.Contains(fragment, exception.Message, StringComparison.Ordinal);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithInvalidChainId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CurbyPulseVerifier("not-a-cid"));
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WithCurbyDocuments_ReturnsTheVerifiedPulse()
    {
        var chain = Utf8(CurbyFixtures.Chain);
        var pulseJson = CurbyFixtures.Pulse(1620423);
        var pulseDocument = Utf8(pulseJson);
        var predecessorJson = CurbyFixtures.Pulse(1620422);
        var predecessor = Utf8(predecessorJson);

        var pulse = new CurbyPulseVerifier().Verify(chain, pulseDocument, predecessor);

        var randomness = Convert.ToHexStringLower(pulse.Randomness);
        Assert.Equal(CurbyDefaults.BeaconName, pulse.Beacon);
        Assert.Equal(CurbyFixtures.ChainId, pulse.Chain);
        Assert.Equal(CurbyFixtures.LatestRound, pulse.Round);
        Assert.Equal(CurbyFixtures.LatestPulseId, pulse.Id);
        Assert.Equal(CurbyFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal(CurbyFixtures.LatestRandomness, randomness);
    }

    [Fact]
    public void Verify_WithCurbyDocuments_DerivesTheReferenceDraw()
    {
        var chain = Utf8(CurbyFixtures.Chain);
        var pulseJson = CurbyFixtures.Pulse(1620423);
        var pulseDocument = Utf8(pulseJson);
        var predecessorJson = CurbyFixtures.Pulse(1620422);
        var predecessor = Utf8(predecessorJson);
        var pulse = new CurbyPulseVerifier().Verify(chain, pulseDocument, predecessor);

        using var draw = VerifiableRandom.Create(pulse, "example-raffle-2026-11-25");

        var bytes = draw.GetBytes(32);
        var hex = Convert.ToHexStringLower(bytes);
        // The value docs/draws.md publishes for this pulse and context.
        Assert.Equal("e43e360c9e2b9cd6a094e04a64e65922a3581ba6864da1cb1490244522f9986e", hex);
    }

    [Fact]
    public void Verify_WhenPredecessorIsNotTheLinkedPulse_IsInvalid()
    {
        var chain = Utf8(CurbyFixtures.Chain);
        var pulseJson = CurbyFixtures.Pulse(1620423);
        var pulseDocument = Utf8(pulseJson);
        var predecessorJson = CurbyFixtures.Pulse(1620421);
        var predecessor = Utf8(predecessorJson);
        var verifier = new CurbyPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(chain, pulseDocument, predecessor)
        );

        AssertInvalid(exception, "does not precede");
    }

    [Fact]
    public void Verify_WhenTheChainIsNotPinned_IsInvalid()
    {
        using var impostor = new TwineBuilder();
        var chain = Utf8(impostor.ChainJson);
        var pulseJson = CurbyFixtures.Pulse(1620423);
        var pulseDocument = Utf8(pulseJson);
        var predecessorJson = CurbyFixtures.Pulse(1620422);
        var predecessor = Utf8(predecessorJson);
        var verifier = new CurbyPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(chain, pulseDocument, predecessor)
        );

        AssertInvalid(exception, "pinned");
    }

    [Fact]
    public void Verify_WithTamperedPulse_IsInvalid()
    {
        var chain = Utf8(CurbyFixtures.Chain);
        var tampered = CurbyFixtures
            .Pulse(1620423)
            .Replace(
                "2026-11-25T02:44:00.150Z",
                "2026-11-25T02:44:00.151Z",
                StringComparison.Ordinal
            );
        var pulseDocument = Utf8(tampered);
        var predecessorJson = CurbyFixtures.Pulse(1620422);
        var predecessor = Utf8(predecessorJson);
        var verifier = new CurbyPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(chain, pulseDocument, predecessor)
        );

        AssertInvalid(exception, "hashes to");
    }

    [Fact]
    public void Verify_WithMalformedJson_IsInvalid()
    {
        var chain = Utf8(CurbyFixtures.Chain);
        var pulseDocument = "{"u8.ToArray();
        var predecessorJson = CurbyFixtures.Pulse(1620422);
        var predecessor = Utf8(predecessorJson);
        var verifier = new CurbyPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(chain, pulseDocument, predecessor)
        );

        AssertInvalid(exception, "not valid JSON");
    }

    [Fact]
    public void Verify_WithAnotherPinnedChain_AcceptsItsPulses()
    {
        using var builder = new TwineBuilder();
        var start = new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);
        var pulses = builder.Pulses(2, start);
        var chainId = builder.ChainCid.ToString();
        var verifier = new CurbyPulseVerifier(chainId);
        var chain = Utf8(builder.ChainJson);
        var pulseDocument = Utf8(pulses[1].Json);
        var predecessor = Utf8(pulses[0].Json);

        var pulse = verifier.Verify(chain, pulseDocument, predecessor);

        var expectedId = pulses[1].Cid.ToString();
        Assert.Equal(1, pulse.Round);
        Assert.Equal(expectedId, pulse.Id);
    }

    #endregion
}
