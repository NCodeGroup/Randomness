// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Curby.Tests.Fixtures;

namespace NCode.Randomness.Curby.Tests;

public class CurbyPulseVerifierPropertyTests
{
    private const string Replay = "20261125,1";

    #region Verify Tests

    [Property(Replay = Replay)]
    public void Verify_WithAnyBytesAsThePulse_ThrowsInvalidPulse(NonNull<byte[]> bytes)
    {
        var chain = Encoding.UTF8.GetBytes(CurbyFixtures.Chain);
        var predecessor = Encoding.UTF8.GetBytes(
            CurbyFixtures.Pulse(CurbyFixtures.LatestRound - 1)
        );
        var verifier = new CurbyPulseVerifier();

        var exception = Assert.Throws<BeaconException>(() =>
            verifier.Verify(chain, bytes.Get, predecessor)
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
            Encoding.UTF8.GetBytes(CurbyFixtures.Chain),
            Encoding.UTF8.GetBytes(CurbyFixtures.Pulse(CurbyFixtures.LatestRound)),
            Encoding.UTF8.GetBytes(CurbyFixtures.Pulse(CurbyFixtures.LatestRound - 1)),
        ];
        var changed = documents[document.Get % documents.Length];
        changed[position.Get % changed.Length] = value;
        var verifier = new CurbyPulseVerifier();

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

        Assert.Equal(CurbyFixtures.LatestRandomness, Convert.ToHexStringLower(pulse.Randomness));
    }

    #endregion
}
