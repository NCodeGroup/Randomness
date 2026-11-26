// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Drand.Tests.Fixtures;

namespace NCode.Randomness.Drand.Tests;

public class DrandRoundVerifierPropertyTests
{
    private const string Replay = "20261125,1";

    #region Verify Tests

    [Property(Replay = Replay)]
    public void Verify_WithAnyBytes_ThrowsInvalidPulse(NonNull<byte[]> bytes)
    {
        var verifier = new DrandRoundVerifier();

        var exception = Assert.Throws<BeaconException>(() => verifier.Verify(bytes.Get));

        Assert.Equal(BeaconError.InvalidPulse, exception.Error);
    }

    [Property(Replay = Replay, MaxTest = 500)]
    public void Verify_WithOneByteChanged_ThrowsInvalidPulseOrKeepsTheRandomness(
        NonNegativeInt position,
        byte value
    )
    {
        var roundJson = DrandFixtures.Round(DrandFixtures.LatestRound);
        var round = Encoding.UTF8.GetBytes(roundJson);
        round[position.Get % round.Length] = value;
        var verifier = new DrandRoundVerifier();

        BeaconPulse pulse;
        try
        {
            pulse = verifier.Verify(round);
        }
        catch (BeaconException exception)
        {
            Assert.Equal(BeaconError.InvalidPulse, exception.Error);
            return;
        }

        var randomness = Convert.ToHexStringLower(pulse.Randomness);

        Assert.Equal(DrandFixtures.LatestRandomness, randomness);
    }

    #endregion
}
