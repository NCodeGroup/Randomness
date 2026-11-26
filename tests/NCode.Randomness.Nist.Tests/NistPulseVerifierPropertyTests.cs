// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Nist.Tests.Fixtures;

namespace NCode.Randomness.Nist.Tests;

public class NistPulseVerifierPropertyTests
{
    private const string Replay = "20261125,1";
    private const string CertificatePrefix = "5b6386e6";

    #region Verify Tests

    [Property(Replay = Replay)]
    public void Verify_WithAnyBytesAsThePulse_ThrowsInvalidPulse(NonNull<byte[]> bytes)
    {
        var predecessorJson = NistFixtures.Pulse(NistFixtures.LatestIndex - 1);
        var predecessor = Encoding.UTF8.GetBytes(predecessorJson);
        var certificate = NistFixtures.Certificate(CertificatePrefix);
        var verifier = new NistPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(bytes.Get, predecessor, certificate)
        );

        Assert.Equal(BeaconError.InvalidPulse, exception.Error);
    }

    [Property(Replay = Replay, MaxTest = 500)]
    public void Verify_WithOneByteChanged_ThrowsInvalidPulseOrKeepsTheRandomness(
        NonNegativeInt document,
        NonNegativeInt position,
        byte value
    )
    {
        var pulseJson = NistFixtures.Pulse(NistFixtures.LatestIndex);
        var predecessorJson = NistFixtures.Pulse(NistFixtures.LatestIndex - 1);
        byte[][] documents =
        [
            Encoding.UTF8.GetBytes(pulseJson),
            Encoding.UTF8.GetBytes(predecessorJson),
            NistFixtures.Certificate(CertificatePrefix),
        ];
        var changed = documents[document.Get % documents.Length];
        changed[position.Get % changed.Length] = value;
        var verifier = new NistPulseVerifier();

        BeaconPulse pulse;
        try
        {
            pulse = verifier.Verify(documents[0], documents[1], documents[2]);
        }
        catch (BeaconException exception)
        {
            Assert.Equal(BeaconError.InvalidPulse, exception.Error);
            return;
        }

        var randomness = Convert.ToHexString(pulse.Randomness);

        Assert.Equal(NistFixtures.LatestOutput, randomness);
    }

    #endregion
}
