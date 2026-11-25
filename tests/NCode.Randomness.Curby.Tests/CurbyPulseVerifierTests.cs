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
        var pulseDocument = Utf8(CurbyFixtures.Pulse(1620423));
        var predecessor = Utf8(CurbyFixtures.Pulse(1620422));

        var pulse = new CurbyPulseVerifier().Verify(chain, pulseDocument, predecessor);

        Assert.Equal(CurbyDefaults.BeaconName, pulse.Beacon);
        Assert.Equal(CurbyFixtures.ChainId, pulse.Chain);
        Assert.Equal(CurbyFixtures.LatestRound, pulse.Round);
        Assert.Equal(CurbyFixtures.LatestPulseId, pulse.Id);
        Assert.Equal(CurbyFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal(CurbyFixtures.LatestRandomness, Convert.ToHexStringLower(pulse.Randomness));
    }

    [Fact]
    public void Verify_WithCurbyDocuments_DerivesTheReferenceDraw()
    {
        var chain = Utf8(CurbyFixtures.Chain);
        var pulseDocument = Utf8(CurbyFixtures.Pulse(1620423));
        var predecessor = Utf8(CurbyFixtures.Pulse(1620422));
        var pulse = new CurbyPulseVerifier().Verify(chain, pulseDocument, predecessor);

        using var draw = VerifiableRandom.Create(pulse, "example-raffle-2026-11-25");

        // The value docs/draws.md publishes for this pulse and context.
        Assert.Equal(
            "e43e360c9e2b9cd6a094e04a64e65922a3581ba6864da1cb1490244522f9986e",
            Convert.ToHexStringLower(draw.GetBytes(32))
        );
    }

    [Fact]
    public void Verify_WhenPredecessorIsNotTheLinkedPulse_IsInvalid()
    {
        var chain = Utf8(CurbyFixtures.Chain);
        var pulseDocument = Utf8(CurbyFixtures.Pulse(1620423));
        var predecessor = Utf8(CurbyFixtures.Pulse(1620421));
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
        var pulseDocument = Utf8(CurbyFixtures.Pulse(1620423));
        var predecessor = Utf8(CurbyFixtures.Pulse(1620422));
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
        var pulseDocument = Utf8(
            CurbyFixtures
                .Pulse(1620423)
                .Replace(
                    "2026-11-25T02:44:00.150Z",
                    "2026-11-25T02:44:00.151Z",
                    StringComparison.Ordinal
                )
        );
        var predecessor = Utf8(CurbyFixtures.Pulse(1620422));
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
        var predecessor = Utf8(CurbyFixtures.Pulse(1620422));
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
        var pulses = builder.Pulses(2, new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero));
        var verifier = new CurbyPulseVerifier(builder.ChainCid.ToString());

        var pulse = verifier.Verify(
            Utf8(builder.ChainJson),
            Utf8(pulses[1].Json),
            Utf8(pulses[0].Json)
        );

        Assert.Equal(1, pulse.Round);
        Assert.Equal(pulses[1].Cid.ToString(), pulse.Id);
    }

    #endregion
}
