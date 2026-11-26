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

    #region Verify Tests

    [Property(Replay = Replay)]
    public void Verify_WithAnyBytesAsThePulse_ThrowsInvalidPulse(NonNull<byte[]> bytes)
    {
        var predecessor = Encoding.UTF8.GetBytes(NistFixtures.Pulse(NistFixtures.LatestIndex - 1));
        var certificate = NistFixtures.Certificate("5b6386e6");
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
        byte[][] documents =
        [
            Encoding.UTF8.GetBytes(NistFixtures.Pulse(NistFixtures.LatestIndex)),
            Encoding.UTF8.GetBytes(NistFixtures.Pulse(NistFixtures.LatestIndex - 1)),
            NistFixtures.Certificate("5b6386e6"),
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

        Assert.Equal(NistFixtures.LatestOutput, Convert.ToHexString(pulse.Randomness));
    }

    #endregion
}
