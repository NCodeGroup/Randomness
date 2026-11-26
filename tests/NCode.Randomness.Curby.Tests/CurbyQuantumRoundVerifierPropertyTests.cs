// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Curby.Tests.Fixtures;

namespace NCode.Randomness.Curby.Tests;

public class CurbyQuantumRoundVerifierPropertyTests
{
    private const string Replay = "20261125,1";

    private static byte[][] ServedDocuments()
    {
        var bellResponse = CurbyQuantumFixtures.BellPulse(CurbyQuantumFixtures.BellResponseIndex);
        var firstSeed = CurbyQuantumFixtures.SeedPulse(CurbyQuantumFixtures.FirstSeedPulseIndex);
        var secondSeed = CurbyQuantumFixtures.SeedPulse(CurbyQuantumFixtures.SecondSeedPulseIndex);
        var thirdSeed = CurbyQuantumFixtures.SeedPulse(CurbyQuantumFixtures.ThirdSeedPulseIndex);

        return
        [
            Encoding.UTF8.GetBytes(CurbyQuantumFixtures.RoundJson),
            Encoding.UTF8.GetBytes(CurbyQuantumFixtures.Parameters),
            Encoding.UTF8.GetBytes(CurbyQuantumFixtures.Chain),
            Encoding.UTF8.GetBytes(CurbyQuantumFixtures.BellChain),
            Encoding.UTF8.GetBytes(bellResponse),
            Encoding.UTF8.GetBytes(CurbyQuantumFixtures.SeedChain),
            Encoding.UTF8.GetBytes(firstSeed),
            Encoding.UTF8.GetBytes(secondSeed),
            Encoding.UTF8.GetBytes(thirdSeed),
        ];
    }

    private static BeaconPulse VerifyRound(byte[][] documents)
    {
        List<ReadOnlyMemory<byte>> linked =
        [
            documents[2],
            documents[3],
            documents[4],
            documents[5],
            documents[6],
            documents[7],
            documents[8],
        ];
        var verifier = new CurbyQuantumRoundVerifier();

        return verifier.Verify(documents[0], documents[1], linked);
    }

    #region Verify Tests

    [Property(Replay = Replay)]
    public void Verify_WithAnyBytesAsTheRound_ThrowsInvalidPulse(NonNull<byte[]> bytes)
    {
        var documents = ServedDocuments();
        documents[0] = bytes.Get;

        var exception = Assert.Throws<BeaconException>(() => VerifyRound(documents));

        Assert.Equal(BeaconError.InvalidPulse, exception.Error);
    }

    [Property(Replay = Replay, MaxTest = 500)]
    public void Verify_WithOneByteChanged_ThrowsInvalidPulseOrKeepsTheRandomness(
        NonNegativeInt document,
        NonNegativeInt position,
        byte value
    )
    {
        var documents = ServedDocuments();
        var changed = documents[document.Get % documents.Length];
        changed[position.Get % changed.Length] = value;

        BeaconPulse pulse;
        try
        {
            pulse = VerifyRound(documents);
        }
        catch (BeaconException exception)
        {
            Assert.Equal(BeaconError.InvalidPulse, exception.Error);
            return;
        }

        var randomness = Convert.ToHexStringLower(pulse.Randomness);

        Assert.Equal(CurbyQuantumFixtures.Randomness, randomness);
    }

    #endregion
}
